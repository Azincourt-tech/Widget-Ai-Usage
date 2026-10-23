using AiUsageWidget.Models;
using AiUsageWidget.Platform.Windows;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AiUsageWidget.Views;

/// <summary>
/// Visual do painel de uma janela sem moldura (widget e janelinha da bandeja) em cada tema: desfoque,
/// moldura do sistema, cantos e sombra.
/// </summary>
internal static class PanelChrome
{
    public static readonly BoxShadows Shadow = BoxShadows.Parse("0 8 30 0 #47000000");

    public static bool IsGlass(string theme) => ThemeNames.Normalize(theme) == ThemeNames.Glass;

    /// <param name="radius">Cantos nos temas Escuro e Claro (no Vidro, o Windows decide).</param>
    public static void Apply(Window window, Border panel, string theme, double radius)
    {
        var glass = IsGlass(theme);
        panel.BoxShadow = glass ? default : Shadow;

        // No Windows, o desfoque só respeita o arredondamento nativo (8 px); os cantos do painel acompanham.
        panel.CornerRadius = new CornerRadius(glass && OperatingSystem.IsWindows() ? WindowsCorners.SystemCornerRadius : radius);

        // Com o desenho por software o Avalonia só entrega "Transparent"; no Windows, o desfoque vem do
        // WindowsBlur logo abaixo (a dica continua pedindo o acrílico para os outros sistemas).
        window.TransparencyLevelHint = glass
            ? [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent]
            : [WindowTransparencyLevel.Transparent];

        // No Vidro, a janela mantém a moldura do sistema (sem barra de título): é ela que arredonda
        // os cantos e corta o desfoque. Nos outros temas, a janela não tem moldura e o painel desenha tudo.
        window.WindowDecorations = glass ? WindowDecorations.BorderOnly : WindowDecorations.None;

        if (OperatingSystem.IsWindows())
        {
            WindowsCorners.Apply(window, round: glass);
            if (glass)
                WindowsBlur.Apply(window, enabled: true);
        }
    }
}
