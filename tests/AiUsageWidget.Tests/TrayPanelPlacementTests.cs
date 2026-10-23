using AiUsageWidget.Models;
using AiUsageWidget.Services;

namespace AiUsageWidget.Tests;

public class TrayPanelPlacementTests
{
    // Monitor 1920x1080 com a barra de tarefas de 48 px embaixo.
    private static readonly ScreenRect Work = new(0, 0, 1920, 1032);
    private const int Width = 320;
    private const int Height = 300;
    private const int Gap = 12;

    [Fact]
    public void BarraEmbaixo_AbreAcimaDoCursor()
    {
        var (x, y) = TrayPanelPlacement.Place(Work, 1700, 1056, Width, Height, Gap);

        Assert.Equal(1700 - Width / 2, x);
        Assert.Equal(1032 - Height - Gap, y);
    }

    [Fact]
    public void BarraEmbaixo_NoCanto_FicaDentroDaTela()
    {
        var (x, _) = TrayPanelPlacement.Place(Work, 1910, 1056, Width, Height, Gap);

        Assert.Equal(1920 - Width - Gap, x);
    }

    [Fact]
    public void BarraEmCima_AbreAbaixo()
    {
        var work = new ScreenRect(0, 48, 1920, 1080);

        var (_, y) = TrayPanelPlacement.Place(work, 1700, 20, Width, Height, Gap);

        Assert.Equal(48 + Gap, y);
    }

    [Fact]
    public void BarraADireita_AbreAEsquerdaNaAlturaDoCursor()
    {
        var work = new ScreenRect(0, 0, 1872, 1080);

        var (x, y) = TrayPanelPlacement.Place(work, 1890, 900, Width, Height, Gap);

        Assert.Equal(1872 - Width - Gap, x);
        Assert.Equal(900 - Height / 2, y);
    }

    [Fact]
    public void BarraAEsquerda_AbreADireita()
    {
        var work = new ScreenRect(48, 0, 1920, 1080);

        var (x, y) = TrayPanelPlacement.Place(work, 20, 500, Width, Height, Gap);

        Assert.Equal(48 + Gap, x);
        Assert.Equal(500 - Height / 2, y);
    }

    [Fact]
    public void CursorDentroDaArea_IconesOcultos_AbreAcimaDoCursor()
    {
        var (_, y) = TrayPanelPlacement.Place(Work, 1500, 960, Width, Height, Gap);

        Assert.Equal(960 - Height - Gap, y);
    }

    [Fact]
    public void SegundoMonitor_ComCoordenadasNegativas()
    {
        var work = new ScreenRect(-1920, 0, 0, 1032);

        var (x, y) = TrayPanelPlacement.Place(work, -100, 1056, Width, Height, Gap);

        Assert.Equal(-Width - Gap, x);
        Assert.Equal(1032 - Height - Gap, y);
    }

    [Fact]
    public void JanelaMaiorQueAArea_FicaNoComeco()
    {
        var work = new ScreenRect(0, 0, 300, 200);

        var (x, y) = TrayPanelPlacement.Place(work, 150, 250, Width, Height, Gap);

        Assert.Equal(Gap, x);
        Assert.Equal(Gap, y);
    }
}
