using System.Globalization;
using AiUsageWidget.Models;

namespace AiUsageWidget.Services;

/// <summary>
/// Textos e faixas exibidos no widget. Métodos estáticos e puros, para serem testados sem interface.
/// </summary>
public static class UsageFormatter
{
    public const double WarningPercent = 75;
    public const double CriticalPercent = 90;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string WindowLabel(UsageWindowKind kind) => kind switch
    {
        UsageWindowKind.FiveHours => "Sessão (5h)",
        UsageWindowKind.Daily => "Diário",
        UsageWindowKind.Weekly => "Semanal",
        _ => kind.ToString(),
    };

    /// <summary>Rótulo curto para o tamanho pequeno: "5h", "24h", "7d".</summary>
    public static string ShortWindowLabel(UsageWindowKind kind) => kind switch
    {
        UsageWindowKind.FiveHours => "5h",
        UsageWindowKind.Daily => "24h",
        UsageWindowKind.Weekly => "7d",
        _ => kind.ToString(),
    };

    /// <summary>Nome do plano como o provedor escreve: "plus" → "Plus", "PRO" → "Pro". Nulo se vazio.</summary>
    public static string? PlanName(string? plan)
    {
        if (string.IsNullOrWhiteSpace(plan))
            return null;

        plan = plan.Trim();
        return char.ToUpperInvariant(plan[0]) + plan[1..].ToLowerInvariant();
    }

    public static double ClampPercent(double percent) =>
        double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) : 0;

    public static string PercentText(double percent) =>
        $"{Math.Round(ClampPercent(percent), MidpointRounding.AwayFromZero):0}%";

    public static UsageLevel LevelFor(double percent) => ClampPercent(percent) switch
    {
        >= CriticalPercent => UsageLevel.Critical,
        >= WarningPercent => UsageLevel.Warning,
        _ => UsageLevel.Normal,
    };

    /// <summary>
    /// "renova em 42 min", "renova em 3h 05min" ou, a partir de 24 horas, "renova seg. 09:00".
    /// </summary>
    public static string ResetText(DateTimeOffset? resetsAt, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        if (resetsAt is not { } reset)
            return "";

        var remaining = reset - now;
        if (remaining <= TimeSpan.Zero)
            return "renovando...";

        if (remaining < TimeSpan.FromHours(1))
            return $"renova em {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} min";

        if (remaining < TimeSpan.FromHours(24))
        {
            // Arredonda para cima no minuto, para não mostrar "0min" faltando segundos.
            var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
            return $"renova em {minutes / 60}h {minutes % 60:00}min";
        }

        var local = TimeZoneInfo.ConvertTime(reset, timeZone);
        // Conforme a versão do ICU, a abreviação já vem com ponto ("qui.") ou sem ("qui").
        var day = PtBr.DateTimeFormat.GetAbbreviatedDayName(local.DayOfWeek).TrimEnd('.');
        return $"renova {day}. {local:HH:mm}";
    }

    /// <summary>"Atualizado agora", "Atualizado há 3 min", "Atualizado às 14:05".</summary>
    public static string UpdatedText(DateTimeOffset? updatedAt, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        if (updatedAt is not { } updated)
            return "";

        var elapsed = now - updated;
        if (elapsed < TimeSpan.FromMinutes(1))
            return "Atualizado agora";
        if (elapsed < TimeSpan.FromHours(1))
            return $"Atualizado há {(int)elapsed.TotalMinutes} min";

        return $"Atualizado às {TimeZoneInfo.ConvertTime(updated, timeZone):HH:mm}";
    }
}
