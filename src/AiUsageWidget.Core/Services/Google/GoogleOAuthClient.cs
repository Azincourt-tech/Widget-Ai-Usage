using System.Net;
using System.Net.Http;
using System.Text.Json;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Services.Google;

/// <summary>
/// OAuth do Google para app instalado, com PKCE: monta o endereço de login, troca o código por tokens
/// e renova o token, com o <see cref="GoogleOAuthApp"/> do produto.
/// </summary>
public sealed class GoogleOAuthClient(HttpClient http, TimeProvider time, GoogleOAuthApp app)
{
    public GoogleOAuthApp App => app;

    /// <summary>
    /// <c>access_type=offline</c> e <c>prompt=consent</c> garantem o refresh token (o Google só o manda
    /// quando o usuário consente).
    /// </summary>
    public static Uri BuildAuthorizeUrl(GoogleOAuthApp app, string challenge, string state, Uri redirectUri)
    {
        var query = OAuthPkce.Query(new Dictionary<string, string>
        {
            ["client_id"] = app.ClientId,
            ["redirect_uri"] = redirectUri.ToString(),
            ["response_type"] = "code",
            ["scope"] = GoogleEndpoints.Scopes,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
        });

        return new Uri($"{GoogleEndpoints.Authorize}?{query}");
    }

    public Task<GoogleCredentials> ExchangeCodeAsync(string code, string verifier, Uri redirectUri, CancellationToken cancellationToken) =>
        RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri.ToString(),
            ["client_id"] = app.ClientId,
            ["client_secret"] = app.ClientSecret,
            ["code_verifier"] = verifier,
        }, previous: null, cancellationToken);

    public Task<GoogleCredentials> RefreshAsync(GoogleCredentials current, CancellationToken cancellationToken) =>
        RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = current.RefreshToken ?? "",
            ["client_id"] = app.ClientId,
            ["client_secret"] = app.ClientSecret,
        }, current, cancellationToken);

    private async Task<GoogleCredentials> RequestTokensAsync(
        Dictionary<string, string> form, GoogleCredentials? previous, CancellationToken cancellationToken)
    {
        string json;
        try
        {
            using var body = new FormUrlEncodedContent(form);
            using var response = await http.PostAsync(GoogleEndpoints.Token, body, cancellationToken);

            // 400/401 (invalid_grant): código ou refresh token inválido/revogado. Só um novo login resolve.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                throw new UsageProviderException("Sua sessão do Google expirou. Entre de novo.", requiresSignIn: true);
            if (!response.IsSuccessStatusCode)
                throw new UsageProviderException($"O login do Google respondeu com erro ({(int)response.StatusCode}).");

            json = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new UsageProviderException("Sem conexão com o Google.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UsageProviderException("O Google demorou para responder.", ex);
        }

        return ParseTokenResponse(json, time.GetUtcNow(), previous);
    }

    /// <summary>
    /// Lê a resposta de token: <c>{"access_token", "expires_in" (segundos), "refresh_token"?}</c>.
    /// Na renovação o Google em geral não manda refresh token novo: mantém o anterior.
    /// </summary>
    public static GoogleCredentials ParseTokenResponse(string json, DateTimeOffset now, GoogleCredentials? previous)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new UsageProviderException("O login do Google respondeu num formato desconhecido.");

            var access = GetString(root, "access_token");
            if (string.IsNullOrWhiteSpace(access))
                throw new UsageProviderException("O login do Google respondeu sem token.");

            DateTimeOffset? expiresAt = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt64(out var seconds) && seconds > 0
                ? now.AddSeconds(seconds)
                : null;

            var refresh = GetString(root, "refresh_token");
            return new GoogleCredentials(access, string.IsNullOrWhiteSpace(refresh) ? previous?.RefreshToken : refresh, expiresAt);
        }
        catch (JsonException ex)
        {
            throw new UsageProviderException("O login do Google respondeu num formato desconhecido.", ex);
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
