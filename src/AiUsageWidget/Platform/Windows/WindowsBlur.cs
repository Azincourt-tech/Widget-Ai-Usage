using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace AiUsageWidget.Platform.Windows;

/// <summary>
/// Desfoque acrílico atrás da janela (tema Vidro), pedido direto ao Windows. Com o desenho por software
/// (Program.cs), o Avalonia não oferece o <c>AcrylicBlur</c> (só <c>Transparent</c>); o efeito do DWM não
/// depende de quem desenha a janela.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsBlur
{
    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0;
    private const int AccentEnableAcrylicBlurBehind = 4;

    // Cor do acrílico (ABGR): quase transparente, para a cor do painel (tema) decidir o tom.
    private const uint AcrylicTint = 0x01000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    public static void Apply(Window window, bool enabled)
    {
        if (window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
            return;

        var accent = new AccentPolicy
        {
            AccentState = enabled ? AccentEnableAcrylicBlurBehind : AccentDisabled,
            GradientColor = AcrylicTint,
        };
        var size = Marshal.SizeOf<AccentPolicy>();
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, pointer, false);
            var data = new WindowCompositionAttributeData { Attribute = WcaAccentPolicy, Data = pointer, SizeOfData = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
