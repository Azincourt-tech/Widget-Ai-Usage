using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AiUsageWidget.Constants;

namespace AiUsageWidget.Services.Google;

/// <summary>
/// Chamadas ao servidor Code Assist do Google (o mesmo do Antigravity). Todas são
/// <c>POST {base}:{método}</c> com JSON, o token do login e o <c>User-Agent</c> do produto (é por ele
/// que o servidor decide se aceita o cliente).
/// </summary>
public sealed class CodeAssistClient(HttpClient http)
{
    /// <summary>
    /// O que o <c>loadCodeAssist</c> diz da conta: o projeto (exigido nas consultas de cota), o plano e,
    /// sem projeto, o motivo dado pelo Google (ex.: <c>UNSUPPORTED_CLIENT</c>).
    /// </summary>
    public sealed record AccountInfo(string? ProjectId, string? Plan, string? IneligibleReason = null);

    /// <summary>
    /// Corpo do <c>loadCodeAssist</c>. O servidor não olha o <c>ideType</c> (o que conta é o
    /// <c>User-Agent</c>) e devolve o projeto associado à conta (gerenciado pelo Google no plano gratuito).
    /// </summary>
    public static readonly object LoadCodeAssistRequest = new
    {
        metadata = new { ideType = "IDE_UNSPECIFIED", platform = "PLATFORM_UNSPECIFIED", pluginType = "GEMINI" },
    };

    /// <summary>
    /// Chama um método e devolve o status e o corpo (sem lançar erro de HTTP: quem chama decide, para
    /// poder renovar o token num 401). Falha de rede vira <see cref="UsageProviderException"/>.
    /// </summary>
    public async Task<(HttpStatusCode Status, string Body)> PostAsync(
        string method, string accessToken, string userAgent, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GoogleEndpoints.CodeAssist}:{method}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new("Bearer", accessToken);

        // Sem validação, para mandar o User-Agent exatamente como o app manda. O da requisição
        // substitui o padrão do HttpClient.
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException ex)
        {
            throw new UsageProviderException("Sem conexão com o Google.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UsageProviderException("O Google demorou para responder.", ex);
        }
    }

    /// <summary>Transforma um status de erro em mensagem para o card (401 fica com quem chama).</summary>
    public static void ThrowIfFailed(HttpStatusCode status, string expiredMessage)
    {
        if ((int)status is >= 200 and < 300)
            return;

        throw status switch
        {
            HttpStatusCode.Unauthorized => new UsageProviderException(expiredMessage, requiresSignIn: true),
            HttpStatusCode.Forbidden => new UsageProviderException("Esta conta não tem acesso aos limites de uso."),
            HttpStatusCode.TooManyRequests => UsageProviderException.RateLimited(),
            _ => new UsageProviderException($"O Google respondeu com erro ({(int)status})."),
        };
    }

    /// <summary>
    /// Lê a resposta do <c>loadCodeAssist</c>:
    /// <c>{"currentTier": {"id", "name"}, "paidTier": {"id", "name"}?, "cloudaicompanionProject": "id" ou {"id"},
    /// "ineligibleTiers": [{"tierId", "reasonCode"}]?}</c>.
    /// </summary>
    public static AccountInfo ParseLoadCodeAssist(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new AccountInfo(null, null);

            string? project = null;
            if (root.TryGetProperty("cloudaicompanionProject", out var p))
            {
                project = p.ValueKind switch
                {
                    JsonValueKind.String => p.GetString(),
                    JsonValueKind.Object when p.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String => id.GetString(),
                    _ => null,
                };
            }

            if (string.IsNullOrWhiteSpace(project))
                project = null;

            return new AccountInfo(project, PlanFromTiers(root), project is null ? IneligibleReason(root) : null);
        }
        catch (JsonException)
        {
            return new AccountInfo(null, null);
        }
    }

    /// <summary>
    /// Nome curto do plano: o pago ("Google AI Ultra", "Pro", "Plus") tem prioridade; senão o nível
    /// atual ("free-tier" vira "Free", "standard-tier" vira "Standard").
    /// </summary>
    private static string? PlanFromTiers(JsonElement root)
    {
        foreach (var tierName in new[] { "paidTier", "currentTier" })
        {
            if (!root.TryGetProperty(tierName, out var tier) || tier.ValueKind != JsonValueKind.Object)
                continue;

            var name = tier.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
            var id = tier.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() ?? "" : "";

            if (Has(name, id, "Ultra"))
                return "Ultra";
            if (Has(name, id, "Pro"))
                return "Pro";
            if (Has(name, id, "Plus"))
                return "Plus";
            if (id.StartsWith("free", StringComparison.OrdinalIgnoreCase))
                return "Free";
            if (id.StartsWith("standard", StringComparison.OrdinalIgnoreCase))
                return "Standard";
        }

        return null;
    }

    /// <summary>A palavra no nome ("Google AI Plus") ou como parte do id ("g1-plus-tier").</summary>
    private static bool Has(string name, string id, string word) =>
        name.Split(' ').Contains(word, StringComparer.OrdinalIgnoreCase)
        || id.Split('-').Contains(word, StringComparer.OrdinalIgnoreCase);

    /// <summary>Primeiro motivo de recusa (<c>ineligibleTiers[].reasonCode</c>), se houver.</summary>
    private static string? IneligibleReason(JsonElement root)
    {
        if (!root.TryGetProperty("ineligibleTiers", out var tiers) || tiers.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var tier in tiers.EnumerateArray())
        {
            if (tier.ValueKind == JsonValueKind.Object
                && tier.TryGetProperty("reasonCode", out var reason) && reason.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(reason.GetString()))
            {
                return reason.GetString();
            }
        }

        return null;
    }
}
