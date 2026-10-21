using AiUsageWidget.Models;

namespace AiUsageWidget.Services;

/// <summary>
/// Decide quando avisar que um limite passou de 80% ou de 95% (decisão do usuário: só ao cruzar, com o
/// widget aberto). A primeira leitura de cada limite só serve de base: abrir o widget com um limite já
/// acima de 80% não avisa, porque o anel já mostra. Quando o limite renova e volta a subir, avisa de novo.
/// </summary>
public sealed class UsageAlertTracker
{
    /// <summary>Percentuais que geram aviso, em ordem crescente.</summary>
    public static readonly IReadOnlyList<double> Thresholds = [80, 95];

    // Último percentual lido de cada limite, por conta ("claude/Semanal").
    private readonly Dictionary<string, double> _lastPercent = [];

    /// <summary>
    /// Registra uma leitura e devolve os avisos a mostrar: no máximo um por limite, o mais alto cruzado
    /// (se pulou de 70% para 97%, avisa só os 95%).
    /// </summary>
    public IReadOnlyList<UsageAlert> Check(string providerId, string providerName, IEnumerable<UsageWindow> windows)
    {
        List<UsageAlert> alerts = [];
        foreach (var window in windows)
        {
            var label = window.Label ?? UsageFormatter.WindowLabel(window.Kind);
            var key = $"{providerId}/{label}";
            var percent = UsageFormatter.ClampPercent(window.UsedPercent);

            if (_lastPercent.TryGetValue(key, out var previous))
            {
                // O mais alto cruzado desde a leitura anterior (0 se nenhum).
                var crossed = Thresholds.LastOrDefault(t => previous < t && percent >= t);
                if (crossed > 0)
                    alerts.Add(new UsageAlert(providerName, label, crossed, percent, window.ResetsAt));
            }

            _lastPercent[key] = percent;
        }

        return alerts;
    }

    /// <summary>Esquece as leituras de uma conta (saiu da conta): a próxima leitura volta a ser só a base.</summary>
    public void Forget(string providerId)
    {
        foreach (var key in _lastPercent.Keys.Where(k => k.StartsWith(providerId + "/", StringComparison.Ordinal)).ToList())
            _lastPercent.Remove(key);
    }

    /// <summary>Título da notificação: "Claude: Semanal em 82%".</summary>
    public static string TitleFor(UsageAlert alert) =>
        $"{alert.ProviderName}: {alert.WindowLabel} em {UsageFormatter.PercentText(alert.UsedPercent)}";

    /// <summary>Texto da notificação: "Passou de 80% do limite; renova sex. 21:22."</summary>
    public static string MessageFor(UsageAlert alert, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        var start = alert.Threshold >= Thresholds[^1] ? "Quase no fim: passou" : "Passou";
        var reset = UsageFormatter.ResetText(alert.ResetsAt, now, timeZone);
        return string.IsNullOrEmpty(reset)
            ? $"{start} de {alert.Threshold:0}% do limite."
            : $"{start} de {alert.Threshold:0}% do limite; {reset}.";
    }
}
