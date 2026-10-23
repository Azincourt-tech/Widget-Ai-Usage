using System.Text.Json.Serialization;

namespace AiUsageWidget.Models;

/// <summary>
/// Preferências do usuário. Use <see cref="Normalize"/> depois de ler de fora (arquivo ou tela)
/// para garantir que todos os valores estão dentro do permitido.
/// </summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = ThemeNames.Glass;

    [JsonConverter(typeof(JsonStringEnumConverter<WidgetSize>))]
    public WidgetSize Size { get; set; } = WidgetSize.Medium;

    /// <summary>Widget na área de trabalho ou ícone na bandeja (padrão: widget).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DisplayMode>))]
    public DisplayMode Mode { get; set; } = DisplayMode.Widget;

    /// <summary>Contas escondidas do widget (valores de <see cref="ProviderIds"/>). Vazio: mostra todas.</summary>
    public string[] HiddenProviders { get; set; } = [];

    /// <summary>Avisar quando um limite passar de 80% e de 95% (ligado por padrão).</summary>
    public bool Notifications { get; set; } = true;

    public bool Pinned { get; set; }
    public double? Left { get; set; }
    public double? Top { get; set; }

    public bool IsVisible(string providerId) => !HiddenProviders.Contains(providerId);

    /// <summary>O widget flutuante aparece neste modo.</summary>
    [JsonIgnore]
    public bool ShowsWidget => Mode == DisplayMode.Widget;

    /// <summary>O ícone na bandeja aparece neste modo.</summary>
    [JsonIgnore]
    public bool ShowsTray => Mode == DisplayMode.Tray;

    /// <summary>
    /// Corrige qualquer valor fora do esperado (arquivo editado à mão, versão antiga, etc.).
    /// </summary>
    public AppSettings Normalize()
    {
        Theme = ThemeNames.Normalize(Theme);
        if (!Enum.IsDefined(Size))
            Size = WidgetSize.Medium;
        if (!Enum.IsDefined(Mode))
            Mode = DisplayMode.Widget;
        HiddenProviders = [.. (HiddenProviders ?? []).Where(ProviderIds.All.Contains).Distinct()];
        if (Left is double left && !double.IsFinite(left))
            Left = null;
        if (Top is double top && !double.IsFinite(top))
            Top = null;
        return this;
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
