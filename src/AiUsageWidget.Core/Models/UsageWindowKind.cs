namespace AiUsageWidget.Models;

/// <summary>
/// Tipo de janela de limite. Cada provedor usa as suas: Claude e Codex têm sessão de 5 horas e
/// semanal; o Gemini tem cota diária.
/// </summary>
public enum UsageWindowKind
{
    FiveHours,
    Daily,
    Weekly,
}
