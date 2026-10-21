using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Services.Claude;

/// <summary>
/// Limites do plano Claude (Pro, Max, Team...): sessão de 5 horas e semanal, mais os semanais por modelo
/// (Opus, Sonnet) quando o plano tem. Usa o login próprio do
/// widget (<see cref="ClaudeAccount"/>) e consulta o mesmo endereço do comando <c>/usage</c> do Claude Code.
/// O plano vem do perfil da conta, lido uma vez por login.
/// </summary>
public sealed partial class ClaudeUsageProvider(ClaudeAccount account, HttpClient http) : IUsageProvider
{
    // Plano já lido e o login a que ele pertence (se o usuário trocar de conta, lê de novo).
    private string? _plan;
    private string? _planLogin;

    public string Id => ProviderIds.Claude;

    public string DisplayName => "Claude";

    public IProviderAccount Account => account;

    public async Task<ProviderUsage> GetUsageAsync(CancellationToken cancellationToken)
    {
        var credentials = await account.GetValidCredentialsAsync(forceRefresh: false, cancellationToken);
        var (status, body) = await FetchAsync(ClaudeEndpoints.Usage, credentials.AccessToken, cancellationToken);

        // Token recusado mesmo dentro da validade (ex.: revogado): renova uma vez e tenta de novo.
        if (status == HttpStatusCode.Unauthorized)
        {
            credentials = await account.GetValidCredentialsAsync(forceRefresh: true, cancellationToken);
            (status, body) = await FetchAsync(ClaudeEndpoints.Usage, credentials.AccessToken, cancellationToken);
        }

        ThrowIfFailed(status);
        var windows = ParseUsage(body);
        return new ProviderUsage(await GetPlanAsync(credentials, cancellationToken), windows);
    }

    /// <summary>Plano da conta ("Max 20x", "Pro", "Team"...). Se o perfil falhar, segue sem a etiqueta.</summary>
    private async Task<string?> GetPlanAsync(ClaudeCredentials credentials, CancellationToken cancellationToken)
    {
        var login = credentials.RefreshToken ?? credentials.AccessToken;
        if (_plan is not null && _planLogin == login)
            return _plan;

        try
        {
            var (status, body) = await FetchAsync(ClaudeEndpoints.Profile, credentials.AccessToken, cancellationToken);
            if ((int)status is < 200 or >= 300)
                return null;

            _plan = ParsePlan(body);
            _planLogin = login;
            return _plan;
        }
        catch (UsageProviderException)
        {
            return null;
        }
    }

    private async Task<(HttpStatusCode Status, string Body)> FetchAsync(string url, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", accessToken);

        // O Claude Code só manda o cabeçalho beta na consulta de uso (o perfil não usa).
        if (url == ClaudeEndpoints.Usage)
            request.Headers.Add("anthropic-beta", ClaudeEndpoints.OAuthBetaHeader);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException ex)
        {
            throw new UsageProviderException("Sem conexão com a Anthropic.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Tempo limite do HttpClient (não foi o widget que cancelou).
            throw new UsageProviderException("A Anthropic demorou para responder.", ex);
        }
    }

    private static void ThrowIfFailed(HttpStatusCode status)
    {
        if ((int)status is >= 200 and < 300)
            return;

        throw status switch
        {
            HttpStatusCode.Unauthorized => new UsageProviderException("Sua sessão do Claude expirou. Entre de novo.", requiresSignIn: true),
            HttpStatusCode.Forbidden => new UsageProviderException("Esta conta não tem acesso aos limites de uso."),
            HttpStatusCode.TooManyRequests => UsageProviderException.RateLimited(),
            _ => new UsageProviderException($"A Anthropic respondeu com erro ({(int)status})."),
        };
    }

    /// <summary>
    /// Lê a resposta do endereço de uso. Formato esperado:
    /// <c>{"five_hour": {"utilization": 42.0, "resets_at": "..."}, "seven_day": {...}, ...}</c>,
    /// com <c>utilization</c> de 0 a 100. Janelas ausentes ou nulas são ignoradas.
    /// </summary>
    public static IReadOnlyList<UsageWindow> ParseUsage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new UsageProviderException("A resposta do Claude veio num formato desconhecido.");

            List<UsageWindow> windows = [];
            AddWindow(root, "five_hour", UsageWindowKind.FiveHours, windows);
            AddWindow(root, "seven_day", UsageWindowKind.Weekly, windows);

            // Limites semanais por modelo: só alguns planos têm (na conta Team, em outubro de 2026,
            // vêm nulos). Quando vêm, ganham um anel próprio.
            AddWindow(root, "seven_day_opus", UsageWindowKind.Weekly, windows, "Opus semanal", "Opus");
            AddWindow(root, "seven_day_sonnet", UsageWindowKind.Weekly, windows, "Sonnet semanal", "Sonnet");

            return windows.Count > 0
                ? windows
                : throw new UsageProviderException("A resposta do Claude veio sem limites.");
        }
        catch (JsonException ex)
        {
            throw new UsageProviderException("A resposta do Claude veio num formato desconhecido.", ex);
        }
    }

    /// <summary>
    /// Lê o plano do perfil: <c>{"organization": {"organization_type": "claude_max", "rate_limit_tier": "..."}}</c>.
    /// Os tipos são os mesmos que o Claude Code reconhece (max, pro, team, enterprise). No Max, o
    /// multiplicador vem do <c>rate_limit_tier</c> (ex.: <c>default_claude_max_20x</c> vira "Max 20x").
    /// </summary>
    public static string? ParsePlan(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("organization", out var org)
                || org.ValueKind != JsonValueKind.Object
                || !org.TryGetProperty("organization_type", out var type)
                || type.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var plan = type.GetString() switch
            {
                "claude_max" => "Max",
                "claude_pro" => "Pro",
                "claude_team" => "Team",
                "claude_enterprise" => "Enterprise",
                _ => null,
            };

            if (plan == "Max"
                && org.TryGetProperty("rate_limit_tier", out var tier) && tier.ValueKind == JsonValueKind.String
                && MultiplierRegex().Match(tier.GetString() ?? "") is { Success: true } match)
            {
                plan = $"Max {match.Groups[1].Value}x";
            }

            return plan;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"_(\d+)x$")]
    private static partial Regex MultiplierRegex();

    private static void AddWindow(
        JsonElement root, string name, UsageWindowKind kind, List<UsageWindow> windows, string? label = null, string? shortLabel = null)
    {
        if (!root.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object)
            return;
        if (!window.TryGetProperty("utilization", out var utilization) || !utilization.TryGetDouble(out var percent))
            return;

        DateTimeOffset? resetsAt =
            window.TryGetProperty("resets_at", out var reset) && reset.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(reset.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : null;

        windows.Add(new UsageWindow(kind, percent, resetsAt, label, shortLabel));
    }
}
