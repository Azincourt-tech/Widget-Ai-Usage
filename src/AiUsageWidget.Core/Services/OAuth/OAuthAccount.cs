using System.Net.Sockets;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Services.OAuth;

/// <summary>
/// Login próprio do widget num provedor: OAuth com PKCE no navegador, login guardado no cofre do
/// sistema e renovação automática quando o token vence. Cada provedor diz como montar o endereço de
/// login, trocar o código e renovar.
/// </summary>
public abstract class OAuthAccount<T>(ICredentialStore<T> store, IBrowserLauncher browser, TimeProvider time) : IProviderAccount
    where T : class, IOAuthCredentials
{
    /// <summary>Quanto tempo esperar o usuário concluir o login no navegador.</summary>
    public static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Renova um pouco antes de vencer, para não falhar no meio da consulta.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

    // O refresh token pode ser trocado a cada renovação: duas renovações ao mesmo tempo invalidariam o login.
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    /// <summary>Nome do provedor nas mensagens ("Claude", "Codex").</summary>
    public abstract string ProviderName { get; }

    public string SignInMessage => $"Entre com sua conta do {ProviderName} para ver os limites.";

    public string ExpiredMessage => $"Sua sessão do {ProviderName} expirou. Entre de novo.";

    public bool IsSignedIn => store.Load() is not null;

    /// <summary>Porta do retorno do login: 0 para uma livre; fixa quando o provedor só aceita uma.</summary>
    protected virtual int CallbackPort => 0;

    protected virtual string CallbackPath => LoopbackCallbackListener.DefaultPath;

    /// <summary>Página para onde o navegador vai depois do login. Nula: o widget mostra uma página simples.</summary>
    protected virtual Uri? SuccessPage => null;

    protected abstract Uri BuildAuthorizeUrl(OAuthPkce pkce, string state, Uri redirectUri);

    protected abstract Task<T> ExchangeCodeAsync(string code, OAuthPkce pkce, string state, Uri redirectUri, CancellationToken cancellationToken);

    /// <summary>Renova o login. <paramref name="current"/> sempre tem refresh token.</summary>
    protected abstract Task<T> RefreshAsync(T current, CancellationToken cancellationToken);

    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SignInTimeout);

        var pkce = OAuthPkce.Create();
        var state = OAuthPkce.CreateState();

        LoopbackCallbackListener listener;
        try
        {
            listener = LoopbackCallbackListener.Start(CallbackPort, CallbackPath);
        }
        catch (SocketException ex)
        {
            throw new UsageProviderException(
                $"A porta {CallbackPort} do login está ocupada (talvez por outro login do {ProviderName} aberto). Feche e tente de novo.", ex);
        }

        using (listener)
        {
            browser.Open(BuildAuthorizeUrl(pkce, state, listener.RedirectUri));

            string code;
            try
            {
                code = await listener.WaitForCodeAsync(state, SuccessPage, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new UsageProviderException("O login demorou demais. Tente de novo.", requiresSignIn: true);
            }

            // Alguns retornos trazem "codigo#state"; só a primeira parte é o código.
            code = code.Split('#')[0];

            store.Save(await ExchangeCodeAsync(code, pkce, state, listener.RedirectUri, cancellationToken));
        }
    }

    public void SignOut() => store.Clear();

    /// <summary>
    /// Login válido para consultar os limites, renovando se estiver para vencer (ou se
    /// <paramref name="forceRefresh"/>, depois de um 401).
    /// </summary>
    public async Task<T> GetValidCredentialsAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var current = store.Load() ?? throw new UsageProviderException(SignInMessage, requiresSignIn: true);
        if (!forceRefresh && !IsExpiring(current))
            return current;

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            // Outra consulta pode ter renovado enquanto esta esperava.
            var latest = store.Load() ?? throw new UsageProviderException(SignInMessage, requiresSignIn: true);
            if (latest.AccessToken != current.AccessToken && !IsExpiring(latest))
                return latest;

            if (string.IsNullOrEmpty(latest.RefreshToken))
                throw ExpiredAndForget();

            try
            {
                var renewed = await RefreshAsync(latest, cancellationToken);
                store.Save(renewed);
                return renewed;
            }
            catch (UsageProviderException ex) when (ex.RequiresSignIn)
            {
                throw ExpiredAndForget();
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private bool IsExpiring(T credentials) =>
        credentials.ExpiresAt is { } expiresAt && expiresAt - RefreshMargin <= time.GetUtcNow();

    private UsageProviderException ExpiredAndForget()
    {
        store.Clear();
        return new UsageProviderException(ExpiredMessage, requiresSignIn: true);
    }
}
