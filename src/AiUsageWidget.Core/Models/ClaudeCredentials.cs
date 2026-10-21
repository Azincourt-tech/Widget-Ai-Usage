using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Models;

/// <summary>
/// Login do widget na conta do Claude. Fica no cofre do sistema e em memória: nunca vai para o log
/// nem para o config.json.
/// </summary>
/// <param name="RefreshToken">Usado para renovar o <paramref name="AccessToken"/> quando ele vence.</param>
/// <param name="ExpiresAt">Validade do <paramref name="AccessToken"/>.</param>
public sealed record ClaudeCredentials(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt) : IOAuthCredentials
{
    // Não deixa os tokens aparecerem em ToString() (ex.: num log de exceção).
    public override string ToString() => $"ClaudeCredentials {{ ExpiresAt = {ExpiresAt} }}";
}
