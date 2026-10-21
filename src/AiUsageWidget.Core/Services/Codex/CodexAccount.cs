using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;
using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Services.Codex;

/// <summary>
/// Login próprio do widget na conta do ChatGPT, para os limites do Codex. O retorno do login só é
/// aceito na porta fixa 1455; no fim, o navegador mostra uma página simples do widget.
/// </summary>
public sealed class CodexAccount(
    ICredentialStore<CodexCredentials> store,
    CodexOAuthClient oauth,
    IBrowserLauncher browser,
    TimeProvider time) : OAuthAccount<CodexCredentials>(store, browser, time)
{
    /// <summary>Chave do login no cofre do sistema.</summary>
    public const string VaultKey = "AiUsageWidget/codex";

    public override string ProviderName => "Codex";

    protected override int CallbackPort => CodexEndpoints.CallbackPort;

    protected override string CallbackPath => CodexEndpoints.CallbackPath;

    protected override Uri BuildAuthorizeUrl(OAuthPkce pkce, string state, Uri redirectUri) =>
        CodexOAuthClient.BuildAuthorizeUrl(pkce.Challenge, state, redirectUri);

    protected override Task<CodexCredentials> ExchangeCodeAsync(
        string code, OAuthPkce pkce, string state, Uri redirectUri, CancellationToken cancellationToken) =>
        oauth.ExchangeCodeAsync(code, pkce.Verifier, redirectUri, cancellationToken);

    protected override Task<CodexCredentials> RefreshAsync(CodexCredentials current, CancellationToken cancellationToken) =>
        oauth.RefreshAsync(current, cancellationToken);
}
