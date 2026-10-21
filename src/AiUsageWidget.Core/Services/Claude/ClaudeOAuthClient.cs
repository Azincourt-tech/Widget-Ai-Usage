using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Services.Claude;

/// <summary>
/// OAuth do Claude com PKCE: monta o endereço de login, troca o código por tokens e renova o token.
/// </summary>
public sealed class ClaudeOAuthClient(HttpClient http, TimeProvider time)
{
    public static Uri BuildAuthorizeUrl(string challenge, string state, Uri redirectUri)
    {
        var query = OAuthPkce.Query(new Dictionary<string, string>
        {
            ["code"] = "true",
            ["client_id"] = ClaudeEndpoints.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri.ToString(),
            ["scope"] = ClaudeEndpoints.Scopes,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
        });

        return new Uri($"{ClaudeEndpoints.Authorize}?{query}");
    }

    public Task<ClaudeCredentials> ExchangeCodeAsync(string code, string verifier, string state, Uri redirectUri, CancellationToken cancellationToken) =>
        RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri.ToString(),
            ["client_id"] = ClaudeEndpoints.ClientId,
            ["code_verifier"] = verifier,
            ["state"] = state,
        }, previousRefreshToken: null, cancellationToken);

    /// <summary>
    /// Renova o token. O servidor pode devolver um refresh token novo (o antigo deixa de valer),
    /// por isso o resultado precisa ser salvo logo.
    /// </summary>
    public Task<ClaudeCredentials> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = ClaudeEndpoints.ClientId,
        }, refreshToken, cancellationToken);

    private async Task<ClaudeCredentials> RequestTokensAsync(
        Dictionary<string, string> body, string? previousRefreshToken, CancellationToken cancellationToken)
    {
        string json;
        try
        {
            using var response = await http.PostAsJsonAsync(ClaudeEndpoints.Token, body, cancellationToken);

            // 400/401: código ou refresh token inválido/vencido. Só um novo login resolve.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                throw new UsageProviderException("Sua sessão do Claude expirou. Entre de novo.", requiresSignIn: true);
            if (!response.IsSuccessStatusCode)
                throw new UsageProviderException($"O login do Claude respondeu com erro ({(int)response.StatusCode}).");

            json = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new UsageProviderException("Sem conexão com a Anthropic.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UsageProviderException("A Anthropic demorou para responder.", ex);
        }

        return ParseTokenResponse(json, time.GetUtcNow(), previousRefreshToken);
    }

    /// <summary>
    /// Lê a resposta de token: <c>{"access_token", "refresh_token", "expires_in" (segundos), ...}</c>.
    /// Se não vier refresh token novo, mantém o anterior.
    /// </summary>
    public static ClaudeCredentials ParseTokenResponse(string json, DateTimeOffset now, string? previousRefreshToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new UsageProviderException("O login do Claude respondeu num formato desconhecido.");

            var access = GetString(root, "access_token");
            if (string.IsNullOrWhiteSpace(access))
                throw new UsageProviderException("O login do Claude respondeu sem token.");

            DateTimeOffset? expiresAt = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt64(out var seconds) && seconds > 0
                ? now.AddSeconds(seconds)
                : null;

            var refresh = GetString(root, "refresh_token");
            return new ClaudeCredentials(access, string.IsNullOrWhiteSpace(refresh) ? previousRefreshToken : refresh, expiresAt);
        }
        catch (JsonException ex)
        {
            throw new UsageProviderException("O login do Claude respondeu num formato desconhecido.", ex);
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
