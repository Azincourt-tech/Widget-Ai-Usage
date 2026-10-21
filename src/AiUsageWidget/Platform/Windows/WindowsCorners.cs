using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace AiUsageWidget.Platform.Windows;

/// <summary>
/// Controla o arredondamento e o contorno que o Windows 11 aplica à janela.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsCorners
{
    /// <summary>Raio que o Windows 11 usa com <c>DWMWCP_ROUND</c>.</summary>
    public const double SystemCornerRadius = 8;

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpDoNotRound = 1;
    private const int DwmwcpRound = 2;
    private const int DwmwaBorderColor = 34;
    private const uint DwmwaColorNone = 0xFFFFFFFE;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Com o desfoque ligado, o Windows arredonda a janela (o único corte que o desfoque respeita);
    /// desligado, quem desenha os cantos é só a interface. O contorno claro do sistema fica sempre desligado.
    /// </summary>
    public static void Apply(Window window, bool round)
    {
        if (window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
            return;

        var corner = round ? DwmwcpRound : DwmwcpDoNotRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

        var borderColor = unchecked((int)DwmwaColorNone);
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref borderColor, sizeof(int));
    }
}
