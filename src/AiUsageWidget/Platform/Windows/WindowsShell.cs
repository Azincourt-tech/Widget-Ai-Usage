using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;

namespace AiUsageWidget.Platform.Windows;

/// <summary>Informações do Windows que o Avalonia não expõe (posição do cursor).</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsShell
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Win32Point point);

    /// <summary>Posição do cursor em pixels físicos (a mesma escala de <c>Screen.WorkingArea</c>).</summary>
    public static PixelPoint? CursorPosition() =>
        GetCursorPos(out var point) ? new PixelPoint(point.X, point.Y) : null;
}
