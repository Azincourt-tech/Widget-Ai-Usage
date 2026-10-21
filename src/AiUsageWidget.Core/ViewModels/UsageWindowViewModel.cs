using AiUsageWidget.Models;
using AiUsageWidget.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AiUsageWidget.ViewModels;

/// <summary>Um anel do card: rótulo, percentual usado e quando renova.</summary>
public sealed partial class UsageWindowViewModel : ObservableObject
{
    private const string WarningColor = "#FF9F0A";
    private const string CriticalColor = "#FF453A";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTip))]
    private string _resetText = "";

    public UsageWindowViewModel(UsageWindow window, string accentColor)
    {
        Window = window;
        Label = window.Label ?? UsageFormatter.WindowLabel(window.Kind);
        ShortLabel = window.ShortLabel ?? UsageFormatter.ShortWindowLabel(window.Kind);
        Percent = UsageFormatter.ClampPercent(window.UsedPercent);
        PercentText = UsageFormatter.PercentText(window.UsedPercent);
        Level = UsageFormatter.LevelFor(window.UsedPercent);
        RingColor = Level switch
        {
            UsageLevel.Critical => CriticalColor,
            UsageLevel.Warning => WarningColor,
            _ => accentColor,
        };
    }

    public UsageWindow Window { get; }

    public string Label { get; }

    /// <summary>Rótulo do tamanho pequeno ("5h", "7d").</summary>
    public string ShortLabel { get; }

    /// <summary>Dica ao passar o mouse no anel: rótulo completo e quando renova.</summary>
    public string ToolTip => string.IsNullOrEmpty(ResetText) ? Label : $"{Label} · {ResetText}";

    /// <summary>Percentual usado, de 0 a 100.</summary>
    public double Percent { get; }

    public string PercentText { get; }

    public UsageLevel Level { get; }

    /// <summary>Cor do anel (#RRGGBB): a do provedor, ou laranja/vermelho perto do limite.</summary>
    public string RingColor { get; }

    /// <summary>Atualiza o texto de renovação (chamado a cada tique do relógio).</summary>
    public void UpdateClock(DateTimeOffset now, TimeZoneInfo timeZone) =>
        ResetText = UsageFormatter.ResetText(Window.ResetsAt, now, timeZone);
}
