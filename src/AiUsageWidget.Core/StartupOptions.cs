using AiUsageWidget.Models;

namespace AiUsageWidget;

/// <summary>
/// Opções de linha de comando.
/// <list type="bullet">
/// <item><c>--demo</c>: abre com contas e limites fictícios, sem tocar nas configurações reais.</item>
/// <item><c>--theme=Dark|Light|Glass</c>: tema inicial do modo demonstração.</item>
/// <item><c>--size=Small|Medium|Large</c>: tamanho inicial do modo demonstração.</item>
/// <item><c>--mode=Widget|Tray</c>: modo inicial do modo demonstração (widget ou ícone na bandeja).</item>
/// </list>
/// </summary>
public sealed record StartupOptions(bool Demo, string? Theme, WidgetSize? Size = null, DisplayMode? Mode = null)
{
    public static StartupOptions Parse(IEnumerable<string> args)
    {
        var demo = false;
        string? theme = null;
        WidgetSize? size = null;
        DisplayMode? mode = null;

        foreach (var arg in args)
        {
            if (arg.Equals("--demo", StringComparison.OrdinalIgnoreCase))
                demo = true;
            else if (arg.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase))
                theme = ThemeNames.Normalize(arg["--theme=".Length..]);
            else if (arg.StartsWith("--size=", StringComparison.OrdinalIgnoreCase)
                     && Enum.TryParse<WidgetSize>(arg["--size=".Length..], ignoreCase: true, out var parsed)
                     && Enum.IsDefined(parsed))
                size = parsed;
            else if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase)
                     && Enum.TryParse<DisplayMode>(arg["--mode=".Length..], ignoreCase: true, out var parsedMode)
                     && Enum.IsDefined(parsedMode))
                mode = parsedMode;
        }

        return new StartupOptions(demo, theme, size, mode);
    }
}
