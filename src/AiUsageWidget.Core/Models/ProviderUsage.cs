namespace AiUsageWidget.Models;

/// <summary>
/// Leitura dos limites de uma conta conectada.
/// </summary>
/// <param name="Plan">Nome do plano (ex.: "Max", "Plus"), se o provedor informar.</param>
public sealed record ProviderUsage(string? Plan, IReadOnlyList<UsageWindow> Windows);
