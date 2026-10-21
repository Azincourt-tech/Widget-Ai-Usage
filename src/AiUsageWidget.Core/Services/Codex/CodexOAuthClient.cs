using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Services.Codex;

/// <summary>
/// OAuth da conta do ChatGPT (o mesmo do Codex CLI) com PKCE: monta o endereço de login, troca o
/// código por tokens e renova o token.
/// </summary>
public sealed class CodexOAuthClient(HttpClient http, TimeProvider time)
{
    public static Uri BuildAuthorizeUrl(string challenge, string state, Uri redirectUri)
    {
        var query = OAuthPkce.Query(new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = CodexEndpoints.ClientId,
            ["redirect_uri"] = redirectUri.ToString(),
            ["scope"] = CodexEndpoints.Scopes,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["id_token_add_organizations"] = "true",
            ["codex_cli_simplified_flow"] = "true",
            ["state"] = state,
        });

        return new Uri($"{CodexEndpoints.Authorize}?{query}");
    }

    public async Task<CodexCredentials> ExchangeCodeAsync(
        string code, string verifier, Uri redirectUri, CancellationToken cancellationToken)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri.ToString(),
            ["client_id"] = CodexEndpoints.ClientId,
            ["code_verifier"] = verifier,
        });

        return ParseTokenResponse(await PostAsync(body, cancellationToken), time.GetUtcNow(), previous: null);
    }

    /// <summary>
    /// Renova o token. O servidor pode devolver um refresh token novo (o antigo deixa de valer),
    /// por isso o resultado precisa ser salvo logo.
    /// </summary>
    public async Task<CodexCredentials> RefreshAsync(CodexCredentials current, CancellationToken cancellationToken)
    {
        using var body = JsonContent.Create(new Dictionary<string, string>
        {
            ["client_id"] = CodexEndpoints.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = current.RefreshToken ?? "",
            ["scope"] = CodexEndpoints.RefreshScopes,
        });

        return ParseTokenResponse(await PostAsync(body, cancellationToken), time.GetUtcNow(), current);
    }

    private async Task<string> PostAsync(HttpContent body, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsync(CodexEndpoints.Token, body, cancellationToken);

            // 400/401: código ou refresh token inválido/vencido. Só um novo login resolve.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                throw new UsageProviderException("Sua sessão do Codex expirou. Entre de novo.", requiresSignIn: true);
            if (!response.IsSuccessStatusCode)
                throw new UsageProviderException($"O login da OpenAI respondeu com erro ({(int)response.StatusCode}).");

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new UsageProviderException("Sem conexão com a OpenAI.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UsageProviderException("A OpenAI demorou para responder.", ex);
        }
    }

    /// <summary>
    /// Lê a resposta de token: <c>{"access_token", "refresh_token", "id_token", "expires_in"?}</c>.
    /// Conta e plano vêm do id_token (<c>"https://api.openai.com/auth": {"chatgpt_account_id", "chatgpt_plan_type"}</c>).
    /// O que não vier na resposta é mantido do login anterior (<paramref name="previous"/>).
    /// </summary>
    public static CodexCredentials ParseTokenResponse(string json, DateTimeOffset now, CodexCredentials? previous)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new UsageProviderException("O login da OpenAI respondeu num formato desconhecido.");

            var access = GetString(root, "access_token");
            if (string.IsNullOrWhiteSpace(access))
                throw new UsageProviderException("O login da OpenAI respondeu sem token.");

            var expiresAt = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt64(out var seconds) && seconds > 0
                ? now.AddSeconds(seconds)
                : JwtPayload.ExpiresAt(access);

            // O id_token tem a conta e o plano; se não vier (algumas renovações), tenta o próprio access token.
            var auth = AuthClaim(GetString(root, "id_token")) ?? AuthClaim(access);
            var accountId = auth is { } a1 ? GetString(a1, "chatgpt_account_id") : null;
            var planType = auth is { } a2 ? GetString(a2, "chatgpt_plan_type") : null;

            var refresh = GetString(root, "refresh_token");
            return new CodexCredentials(
                access,
                string.IsNullOrWhiteSpace(refresh) ? previous?.RefreshToken : refresh,
                expiresAt,
                accountId ?? previous?.AccountId,
                planType ?? previous?.PlanType);
        }
        catch (JsonException ex)
        {
            throw new UsageProviderException("O login da OpenAI respondeu num formato desconhecido.", ex);
        }
    }

    private static JsonElement? AuthClaim(string? jwt) =>
        JwtPayload.Read(jwt) is { } payload
        && payload.TryGetProperty(CodexEndpoints.AuthClaim, out var auth)
        && auth.ValueKind == JsonValueKind.Object
            ? auth
            : null;

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
