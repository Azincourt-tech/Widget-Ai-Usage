using System.Text;
using System.Text.Json;

namespace AiUsageWidget.Services.OAuth;

/// <summary>
/// Lê o conteúdo (payload) de um JWT, sem validar a assinatura. Serve só para ler dados que o próprio
/// servidor de login devolveu ao widget (validade, conta), nunca para decidir permissões.
/// </summary>
public static class JwtPayload
{
    /// <summary>O payload como JSON, ou nulo se o token não for um JWT legível.</summary>
    public static JsonElement? Read(string? token)
    {
        var parts = token?.Split('.');
        if (parts is not { Length: 3 })
            return null;

        try
        {
            var base64 = parts[1].Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Validade (<c>exp</c>, em segundos desde 1970), se houver.</summary>
    public static DateTimeOffset? ExpiresAt(string? token) =>
        Read(token) is { } payload && payload.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
        && seconds is > 0 and < MaxUnixSeconds
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    /// <summary>31/12/9999, o maior valor que <see cref="DateTimeOffset"/> aceita.</summary>
    private const long MaxUnixSeconds = 253402300799;
}
