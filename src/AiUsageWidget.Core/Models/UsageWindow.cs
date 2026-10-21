namespace AiUsageWidget.Models;

/// <summary>
/// Uma janela de limite de um provedor: quanto já foi usado (0 a 100%) e quando renova.
/// </summary>
/// <param name="ResetsAt">Quando a janela renova. Nulo se o provedor não informa.</param>
/// <param name="Label">
/// Rótulo próprio (ex.: "Pro", "Flash", nome do grupo do Antigravity). Nulo: usa o do <paramref name="Kind"/>.
/// </param>
/// <param name="ShortLabel">Rótulo do tamanho pequeno. Nulo: usa o do <paramref name="Kind"/>.</param>
public sealed record UsageWindow(
    UsageWindowKind Kind,
    double UsedPercent,
    DateTimeOffset? ResetsAt,
    string? Label = null,
    string? ShortLabel = null);
