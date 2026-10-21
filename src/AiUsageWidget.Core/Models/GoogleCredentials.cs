using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Models;

/// <summary>
/// Login do widget numa conta Google (para o Antigravity). Fica no cofre do
/// sistema e em memória: nunca vai para o log nem para o config.json.
/// </summary>
public sealed record GoogleCredentials(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt) : IOAuthCredentials
{
    // Não deixa os tokens aparecerem em ToString() (ex.: num log de exceção).
    public override string ToString() => $"GoogleCredentials {{ ExpiresAt = {ExpiresAt} }}";
}
