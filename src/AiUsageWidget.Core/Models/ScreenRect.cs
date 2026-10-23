namespace AiUsageWidget.Models;

/// <summary>Retângulo da tela em pixels físicos (direita e baixo exclusivos).</summary>
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}
