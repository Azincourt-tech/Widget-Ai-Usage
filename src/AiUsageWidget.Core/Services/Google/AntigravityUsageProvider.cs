using System.Globalization;
using System.Net;
using System.Text.Json;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Services.Google;

/// <summary>
/// Cota do Antigravity (a mesma para o Antigravity IDE e o Antigravity CLI: o limite é por conta).
/// Pega o token do login, descobre o projeto da conta pelo <c>loadCodeAssist</c> (uma vez por login) e
/// lê o resumo de cota. O servidor devolve grupos de modelos (ex.: "Gemini", "Claude e GPT"), cada um
/// com seus limites; o card mostra um anel por grupo, pelo limite mais usado.
/// </summary>
public sealed class AntigravityUsageProvider(GoogleAccount account, CodeAssistClient client, TimeProvider time) : IUsageProvider
{
    /// <summary>Mais que isso deixaria o widget alto demais.</summary>
    public const int MaxWindows = 4;

    public const string UnsupportedClientMessage = "O Google não libera o Antigravity para esta conta.";
    public const string NoProjectMessage = "Esta conta ainda não tem o Antigravity liberado pelo Google.";
    public const string NotConfiguredMessage = "Antigravity não configurado nesta versão do widget.";

    // Projeto e plano já lidos e o login a que pertencem (se o usuário trocar de conta, lê de novo).
    private CodeAssistClient.AccountInfo? _info;
    private string? _infoLogin;

    public string Id => ProviderIds.Antigravity;

    public string DisplayName => account.ProviderName;

    public IProviderAccount Account => account;

    public async Task<ProviderUsage> GetUsageAsync(CancellationToken cancellationToken)
    {
        // Build sem o secret (ver Directory.Build.local.props.example): avisa em vez de oferecer um login
        // que o Google recusaria. Não pede login, então o card não mostra o botão "Entrar".
        if (!account.IsConfigured)
            throw new UsageProviderException(NotConfiguredMessage);

        var credentials = await account.GetValidCredentialsAsync(forceRefresh: false, cancellationToken);
        try
        {
            return await ReadAsync(credentials, cancellationToken);
        }
        catch (TokenRejectedException)
        {
            // Token recusado mesmo dentro da validade (ex.: revogado): renova uma vez e tenta de novo.
            credentials = await account.GetValidCredentialsAsync(forceRefresh: true, cancellationToken);
            try
            {
                return await ReadAsync(credentials, cancellationToken);
            }
            catch (TokenRejectedException)
            {
                throw new UsageProviderException(account.ExpiredMessage, requiresSignIn: true);
            }
        }
    }

    private async Task<ProviderUsage> ReadAsync(GoogleCredentials credentials, CancellationToken cancellationToken)
    {
        var info = await GetAccountInfoAsync(credentials, cancellationToken);
        var (status, body) = await client.PostAsync(
            "retrieveUserQuotaSummary", credentials.AccessToken, GoogleEndpoints.AntigravityUserAgent, new { project = info.ProjectId }, cancellationToken);
        ThrowIfFailed(status);

        return new ProviderUsage(info.Plan, ParseQuotaSummary(body, time.GetUtcNow()));
    }

    private async Task<CodeAssistClient.AccountInfo> GetAccountInfoAsync(GoogleCredentials credentials, CancellationToken cancellationToken)
    {
        var login = credentials.RefreshToken ?? credentials.AccessToken;
        if (_info is not null && _infoLogin == login)
            return _info;

        var (status, body) = await client.PostAsync(
            "loadCodeAssist", credentials.AccessToken, GoogleEndpoints.AntigravityUserAgent, CodeAssistClient.LoadCodeAssistRequest, cancellationToken);
        ThrowIfFailed(status);

        var info = CodeAssistClient.ParseLoadCodeAssist(body);
        if (info.ProjectId is null)
        {
            // Não é falta de instalar ou abrir o Antigravity: é o Google que não liberou para a conta.
            throw new UsageProviderException(info.IneligibleReason == "UNSUPPORTED_CLIENT" ? UnsupportedClientMessage : NoProjectMessage);
        }

        _info = info;
        _infoLogin = login;
        return info;
    }

    private void ThrowIfFailed(HttpStatusCode status)
    {
        if (status == HttpStatusCode.Unauthorized)
            throw new TokenRejectedException();
        CodeAssistClient.ThrowIfFailed(status, account.ExpiredMessage);
    }

    /// <summary>401 do Code Assist: o token precisa ser renovado.</summary>
    private sealed class TokenRejectedException : Exception;

    /// <summary>
    /// Lê a resposta do <c>retrieveUserQuotaSummary</c> (campos tirados do protobuf do Antigravity):
    /// <c>{"groups": [{"displayName", "description", "buckets": [...]}], "buckets": [...], "description"}</c>,
    /// cada bucket com <c>displayName</c>, <c>window</c>, <c>remainingFraction</c> (0 a 1),
    /// <c>remainingAmount</c>, <c>disabled</c> e <c>resetTime</c> (ISO). Um anel por grupo (o bucket mais
    /// usado dele); sem grupos, um anel por bucket.
    /// </summary>
    public static IReadOnlyList<UsageWindow> ParseQuotaSummary(string json, DateTimeOffset now)
    {
        List<UsageWindow> windows = [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new UsageProviderException("A resposta do Antigravity veio num formato desconhecido.");

            if (root.TryGetProperty("groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
            {
                foreach (var group in groups.EnumerateArray())
                {
                    if (group.ValueKind != JsonValueKind.Object || !group.TryGetProperty("buckets", out var buckets))
                        continue;

                    var mostUsed = ReadBuckets(buckets).OrderBy(b => b.Remaining).FirstOrDefault();
                    if (mostUsed.Label is null)
                        continue;

                    var label = GroupLabel(GetString(group, "displayName") ?? mostUsed.Label);
                    windows.Add(Window(mostUsed, label, now));
                }
            }

            if (windows.Count == 0 && root.TryGetProperty("buckets", out var topBuckets))
                windows.AddRange(ReadBuckets(topBuckets).Select(b => Window(b, b.Label!, now)));
        }
        catch (JsonException ex)
        {
            throw new UsageProviderException("A resposta do Antigravity veio num formato desconhecido.", ex);
        }

        return windows.Count > 0
            ? windows.Take(MaxWindows).ToList()
            : throw new UsageProviderException("A resposta do Antigravity veio sem limites.");
    }

    private static IEnumerable<(string? Label, string? Window, double Remaining, DateTimeOffset? ResetsAt)> ReadBuckets(JsonElement buckets)
    {
        if (buckets.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var bucket in buckets.EnumerateArray())
        {
            if (bucket.ValueKind != JsonValueKind.Object
                || (bucket.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True)
                || !bucket.TryGetProperty("remainingFraction", out var fraction) || !fraction.TryGetDouble(out var remaining))
            {
                continue;
            }

            yield return (GetString(bucket, "displayName") ?? GetString(bucket, "bucketId") ?? "Limite",
                GetString(bucket, "window"), remaining, ReadTime(bucket, "resetTime"));
        }
    }

    private static UsageWindow Window(
        (string? Label, string? Window, double Remaining, DateTimeOffset? ResetsAt) bucket, string label, DateTimeOffset now) =>
        new(KindFor(bucket.Window, bucket.ResetsAt, now),
            (1 - Math.Clamp(bucket.Remaining, 0, 1)) * 100,
            bucket.ResetsAt,
            label,
            ShortLabelFor(label));

    /// <summary>
    /// Tipo da janela pelo texto do campo <c>window</c> ("5h", "daily", "weekly"...). Se o texto não
    /// disser, usa quanto falta para renovar.
    /// </summary>
    public static UsageWindowKind KindFor(string? window, DateTimeOffset? resetsAt, DateTimeOffset now)
    {
        var text = window?.ToLowerInvariant() ?? "";
        if (text.Contains("week") || text.Contains("7d") || text.Contains("seman"))
            return UsageWindowKind.Weekly;
        if (text.Contains("day") || text.Contains("daily") || text.Contains("24h") || text.Contains("dia"))
            return UsageWindowKind.Daily;
        if (text.Contains("hour") || text.Contains('h'))
            return UsageWindowKind.FiveHours;

        var remaining = resetsAt - now;
        return remaining switch
        {
            null => UsageWindowKind.Daily,
            { } r when r <= TimeSpan.FromHours(6) => UsageWindowKind.FiveHours,
            { } r when r <= TimeSpan.FromHours(36) => UsageWindowKind.Daily,
            _ => UsageWindowKind.Weekly,
        };
    }

    /// <summary>
    /// Nome do grupo mais curto e em português: "Gemini Models" vira "Gemini" e "Claude and GPT models"
    /// vira "Claude e GPT" (nomes reais do Antigravity em outubro de 2026). Outros nomes passam como vêm.
    /// </summary>
    public static string GroupLabel(string name)
    {
        var label = name.Trim();
        if (label.EndsWith(" models", StringComparison.OrdinalIgnoreCase))
            label = label[..^" models".Length].TrimEnd();
        return label.Replace(" and ", " e ", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rótulo do tamanho pequeno, com no máximo 6 letras: a primeira palavra ("Gemini Pro" vira "Gemini").
    /// Grupo de vários modelos usa as iniciais e o último nome ("Claude e GPT" vira "C+GPT"), para não
    /// parecer a conta do Claude.
    /// </summary>
    public static string ShortLabelFor(string label)
    {
        var parts = label.Split(" e ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1)
        {
            var combined = string.Concat(parts[..^1].Select(p => p[0])) + "+" + parts[^1];
            if (combined.Length <= 6)
                return combined;
        }

        var first = label.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? label;
        return first.Length <= 6 ? first : first[..6];
    }

    private static DateTimeOffset? ReadTime(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;
}
