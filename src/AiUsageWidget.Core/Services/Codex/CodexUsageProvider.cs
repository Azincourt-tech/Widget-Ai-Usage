using System.Net;
using System.Net.Http;
using System.Text.Json;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Services.Codex;

/// <summary>
/// Limites do Codex na conta do ChatGPT (Free, Plus, Pro...): em geral sessão de 5 horas e semanal;
/// alguns planos têm só a semanal. Usa o login próprio do widget (<see cref="CodexAccount"/>) e consulta
/// o mesmo endereço que o Codex CLI usa.
/// </summary>
public sealed class CodexUsageProvider(CodexAccount account, HttpClient http, TimeProvider time) : IUsageProvider
{
    public string Id => ProviderIds.Codex;

    public string DisplayName => "Codex";

    public IProviderAccount Account => account;

    public async Task<ProviderUsage> GetUsageAsync(CancellationToken cancellationToken)
    {
        var credentials = await account.GetValidCredentialsAsync(forceRefresh: false, cancellationToken);
        var (status, body) = await FetchAsync(credentials, cancellationToken);

        // Token recusado mesmo dentro da validade (ex.: revogado): renova uma vez e tenta de novo.
        if (status == HttpStatusCode.Unauthorized)
        {
            credentials = await account.GetValidCredentialsAsync(forceRefresh: true, cancellationToken);
            (status, body) = await FetchAsync(credentials, cancellationToken);
        }

        ThrowIfFailed(status, account.ExpiredMessage);
        var usage = ParseUsage(body, time.GetUtcNow());
        return usage with { Plan = usage.Plan ?? UsageFormatter.PlanName(credentials.PlanType) };
    }

    private async Task<(HttpStatusCode Status, string Body)> FetchAsync(CodexCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CodexEndpoints.Usage);
        request.Headers.Authorization = new("Bearer", credentials.AccessToken);
        if (!string.IsNullOrEmpty(credentials.AccountId))
            request.Headers.Add(CodexEndpoints.AccountIdHeader, credentials.AccountId);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException ex)
        {
            throw new UsageProviderException("Sem conexão com a OpenAI.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Tempo limite do HttpClient (não foi o widget que cancelou).
            throw new UsageProviderException("A OpenAI demorou para responder.", ex);
        }
    }

    private static void ThrowIfFailed(HttpStatusCode status, string expiredMessage)
    {
        if ((int)status is >= 200 and < 300)
            return;

        throw status switch
        {
            HttpStatusCode.Unauthorized => new UsageProviderException(expiredMessage, requiresSignIn: true),
            HttpStatusCode.Forbidden => new UsageProviderException("Esta conta não tem acesso ao Codex."),
            HttpStatusCode.TooManyRequests => UsageProviderException.RateLimited(),
            _ => new UsageProviderException($"A OpenAI respondeu com erro ({(int)status})."),
        };
    }

    /// <summary>
    /// Lê a resposta do endereço de uso. Formato esperado:
    /// <c>{"plan_type": "plus", "rate_limit": {"primary_window": {...}, "secondary_window": {...}}}</c>,
    /// cada janela com <c>used_percent</c> (0 a 100), <c>limit_window_seconds</c> e <c>reset_at</c>
    /// (segundos desde 1970) ou <c>reset_after_seconds</c>. Janelas nulas são ignoradas.
    /// </summary>
    public static ProviderUsage ParseUsage(string json, DateTimeOffset now)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new UsageProviderException("A resposta do Codex veio num formato desconhecido.");

            List<UsageWindow> windows = [];
            if (root.TryGetProperty("rate_limit", out var limits) && limits.ValueKind == JsonValueKind.Object)
            {
                AddWindow(limits, "primary_window", now, windows);
                AddWindow(limits, "secondary_window", now, windows);
            }

            if (windows.Count == 0)
                throw new UsageProviderException("A resposta do Codex veio sem limites.");

            // Sessão antes da semanal, como no card do Claude.
            windows.Sort((a, b) => a.Kind.CompareTo(b.Kind));

            var plan = root.TryGetProperty("plan_type", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            return new ProviderUsage(UsageFormatter.PlanName(plan), windows);
        }
        catch (JsonException ex)
        {
            throw new UsageProviderException("A resposta do Codex veio num formato desconhecido.", ex);
        }
    }

    /// <summary>Tipo da janela pela duração: até 6 horas é sessão; até 36 horas, diária; acima, semanal.</summary>
    public static UsageWindowKind KindFor(long windowSeconds) => windowSeconds switch
    {
        <= 6 * 3600 => UsageWindowKind.FiveHours,
        <= 36 * 3600 => UsageWindowKind.Daily,
        _ => UsageWindowKind.Weekly,
    };

    private static void AddWindow(JsonElement limits, string name, DateTimeOffset now, List<UsageWindow> windows)
    {
        if (!limits.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object)
            return;
        if (!window.TryGetProperty("used_percent", out var used) || !used.TryGetDouble(out var percent))
            return;
        if (!window.TryGetProperty("limit_window_seconds", out var length) || !length.TryGetInt64(out var seconds))
            return;

        DateTimeOffset? resetsAt = null;
        if (window.TryGetProperty("reset_at", out var at) && at.TryGetInt64(out var unix) && unix is > 0 and < 253402300799)
            resetsAt = DateTimeOffset.FromUnixTimeSeconds(unix);
        else if (window.TryGetProperty("reset_after_seconds", out var after) && after.TryGetInt64(out var wait) && wait >= 0)
            resetsAt = now.AddSeconds(wait);

        windows.Add(new UsageWindow(KindFor(seconds), percent, resetsAt));
    }
}
