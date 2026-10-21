namespace AiUsageWidget.Models;

/// <summary>Um limite que acabou de passar de um dos avisos (80% ou 95%).</summary>
/// <param name="ProviderName">Nome da conta ("Claude").</param>
/// <param name="WindowLabel">Nome do limite ("Semanal", "Sessão (5h)", "Gemini").</param>
/// <param name="Threshold">O aviso que foi cruzado (80 ou 95).</param>
public sealed record UsageAlert(string ProviderName, string WindowLabel, double Threshold, double UsedPercent, DateTimeOffset? ResetsAt);
