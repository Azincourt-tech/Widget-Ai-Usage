using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;
using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Services.Google;

/// <summary>
/// Login próprio do widget numa conta Google, com o app OAuth do produto (hoje, o do Antigravity).
/// Retorno numa porta livre; no fim, a página de sucesso do próprio produto.
/// </summary>
public sealed class GoogleAccount(
    string providerName,
    ICredentialStore<GoogleCredentials> store,
    GoogleOAuthClient oauth,
    IBrowserLauncher browser,
    TimeProvider time) : OAuthAccount<GoogleCredentials>(store, browser, time)
{
    /// <summary>Chave do login do Antigravity no cofre do sistema.</summary>
    public const string AntigravityVaultKey = "AiUsageWidget/antigravity";

    public override string ProviderName => providerName;

    /// <summary>Falso quando o build não recebeu o secret do app: sem ele o Google recusa o login.</summary>
    public bool IsConfigured => oauth.App.IsConfigured;

    protected override string CallbackPath => oauth.App.CallbackPath;

    protected override Uri SuccessPage => new(oauth.App.SuccessPage);

    protected override Uri BuildAuthorizeUrl(OAuthPkce pkce, string state, Uri redirectUri) =>
        GoogleOAuthClient.BuildAuthorizeUrl(oauth.App, pkce.Challenge, state, redirectUri);

    protected override Task<GoogleCredentials> ExchangeCodeAsync(
        string code, OAuthPkce pkce, string state, Uri redirectUri, CancellationToken cancellationToken) =>
        oauth.ExchangeCodeAsync(code, pkce.Verifier, redirectUri, cancellationToken);

    protected override Task<GoogleCredentials> RefreshAsync(GoogleCredentials current, CancellationToken cancellationToken) =>
        oauth.RefreshAsync(current, cancellationToken);
}
