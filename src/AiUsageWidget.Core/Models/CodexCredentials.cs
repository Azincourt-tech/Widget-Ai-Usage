using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Models;

/// <summary>
/// Login do widget na conta do ChatGPT, para o Codex. Fica no cofre do sistema e em memória:
/// nunca vai para o log nem para o config.json.
/// </summary>
/// <param name="AccountId">Conta (workspace) do ChatGPT, lida do id_token. Vai no cabeçalho da consulta.</param>
/// <param name="PlanType">Plano ("free", "plus", "pro"...), lido do id_token.</param>
public sealed record CodexCredentials(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt,
    string? AccountId,
    string? PlanType) : IOAuthCredentials
{
    // Não deixa os tokens aparecerem em ToString() (ex.: num log de exceção).
    public override string ToString() => $"CodexCredentials {{ ExpiresAt = {ExpiresAt}, PlanType = {PlanType} }}";
}
