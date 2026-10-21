using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Services.Demo;

/// <summary>
/// Provedor com limites fictícios, para o modo demonstração e para os prints do README.
/// Os horários de renovação são relativos ao momento da leitura.
/// </summary>
public sealed class DemoUsageProvider(string id, string displayName, string plan, TimeProvider time, params DemoUsageProvider.Window[] windows)
    : IUsageProvider
{
    /// <summary>Uma janela fictícia: tipo, % usado, quanto falta para renovar e, se quiser, um rótulo próprio.</summary>
    public sealed record Window(UsageWindowKind Kind, double Percent, TimeSpan ResetsIn, string? Label = null, string? ShortLabel = null);

    public string Id => id;

    public string DisplayName => displayName;

    public async Task<ProviderUsage> GetUsageAsync(CancellationToken cancellationToken)
    {
        // Pequena espera para a interface mostrar o estado "atualizando" como no uso real.
        await Task.Delay(TimeSpan.FromMilliseconds(350), time, cancellationToken);

        var now = time.GetUtcNow();
        return new ProviderUsage(plan, [.. windows.Select(w => new UsageWindow(w.Kind, w.Percent, now + w.ResetsIn, w.Label, w.ShortLabel))]);
    }

    /// <summary>As contas do modo demonstração.</summary>
    public static IReadOnlyList<IUsageProvider> CreateAll(TimeProvider time) =>
    [
        new DemoUsageProvider(ProviderIds.Claude, "Claude", "Max 5x", time,
            new Window(UsageWindowKind.FiveHours, 42, new TimeSpan(2, 14, 0)),
            new Window(UsageWindowKind.Weekly, 68, new TimeSpan(3, 6, 0, 0))),
        new DemoUsageProvider(ProviderIds.Codex, "Codex", "Plus", time,
            new Window(UsageWindowKind.FiveHours, 91, new TimeSpan(0, 38, 0)),
            new Window(UsageWindowKind.Weekly, 55, new TimeSpan(5, 2, 0, 0))),
        new DemoUsageProvider(ProviderIds.Antigravity, "Antigravity", "Pro", time,
            new Window(UsageWindowKind.Weekly, 64, new TimeSpan(3, 5, 0, 0), "Gemini", "Gemini"),
            new Window(UsageWindowKind.Weekly, 31, new TimeSpan(3, 5, 0, 0), "Claude e GPT", "C+GPT")),
    ];
}
