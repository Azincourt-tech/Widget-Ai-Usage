using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;
using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Services.Claude;

/// <summary>
/// Login próprio do widget na conta do Claude (Pro, Max, Team...). Retorno numa porta livre e, no fim,
/// a página de sucesso da Anthropic.
/// </summary>
public sealed class ClaudeAccount(
    ICredentialStore<ClaudeCredentials> store,
    ClaudeOAuthClient oauth,
    IBrowserLauncher browser,
    TimeProvider time) : OAuthAccount<ClaudeCredentials>(store, browser, time)
{
    /// <summary>Chave do login no cofre do sistema.</summary>
    public const string VaultKey = "AiUsageWidget/claude";

    public override string ProviderName => "Claude";

    protected override Uri SuccessPage { get; } = new(ClaudeEndpoints.LoginSuccessPage);

    protected override Uri BuildAuthorizeUrl(OAuthPkce pkce, string state, Uri redirectUri) =>
        ClaudeOAuthClient.BuildAuthorizeUrl(pkce.Challenge, state, redirectUri);

    protected override Task<ClaudeCredentials> ExchangeCodeAsync(
        string code, OAuthPkce pkce, string state, Uri redirectUri, CancellationToken cancellationToken) =>
        oauth.ExchangeCodeAsync(code, pkce.Verifier, state, redirectUri, cancellationToken);

    protected override Task<ClaudeCredentials> RefreshAsync(ClaudeCredentials current, CancellationToken cancellationToken) =>
        oauth.RefreshAsync(current.RefreshToken!, cancellationToken);
}
