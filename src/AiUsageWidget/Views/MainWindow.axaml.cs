using System.ComponentModel;
using AiUsageWidget.Models;
using AiUsageWidget.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AiUsageWidget.Views;

/// <summary>
/// Só comportamento visual: posição, arrastar, efeito vidro e sombra por tema.
/// Estado e ações ficam no <see cref="WidgetViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private const double PanelRadius = 22;
    private const double ShadowMargin = 14;

    private readonly WidgetViewModel _viewModel = null!;

    // Borda direita (em pixels) a manter enquanto a janela muda de tamanho.
    private int? _keepRightEdge;

    /// <summary>Usado pelo designer visual do Avalonia.</summary>
    public MainWindow() => InitializeComponent();

    public MainWindow(WidgetViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        PanelView.Width = PanelWidthFor(viewModel.Size, viewModel.RingColumns);
        PlaceOnScreen(viewModel.SavedLeft, viewModel.SavedTop);
        PanelHost.Margin = new Thickness(PanelMarginFor(viewModel.Theme));

        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Arrastar por qualquer parte do painel, inclusive perto da borda.
        PanelView.AddHandler(PointerPressedEvent, Panel_PointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);

        // As leituras e o relógio são do App (valem também para a bandeja); a janela só cuida do visual.
        Opened += (_, _) => ApplyThemeVisuals(_viewModel.Theme);
        Closing += (_, _) => SavePosition();
        Closed += (_, _) => viewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    public void SavePosition() => _viewModel.SaveWindowPosition(Position.X, Position.Y);

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetViewModel.Theme))
            ApplyThemeVisuals(_viewModel.Theme);
        else if (e.PropertyName is nameof(WidgetViewModel.Size) or nameof(WidgetViewModel.RingColumns))
            ApplySize();
    }

    // ---- Tamanho ----

    /// <summary>
    /// Largura do painel em cada tamanho, com as 2 colunas de anéis padrão. O resto (anéis, fontes) fica nos
    /// estilos do XAML.
    /// </summary>
    private static double PanelWidthFor(WidgetSize size) => size switch
    {
        WidgetSize.Small => 330,
        WidgetSize.Large => 600,
        _ => 500,
    };

    /// <summary>
    /// Largura de uma coluna de anéis em cada tamanho: metade do que sobra no painel padrão depois do
    /// espaçamento (16 + 8), da borda (2), da coluna de botões (6 + 22) e da coluna do nome (StackPanel.info
    /// no XAML: 92/108/128).
    /// </summary>
    private static double RingColumnWidthFor(WidgetSize size) => size switch
    {
        WidgetSize.Small => (330 - 54 - 92) / 2.0,
        WidgetSize.Large => (600 - 54 - 128) / 2.0,
        _ => (500 - 54 - 108) / 2.0,
    };

    /// <summary>Colunas além das 2 padrão (ex.: Opus semanal no Claude) alargam o painel.</summary>
    private static double PanelWidthFor(WidgetSize size, int ringColumns) =>
        PanelWidthFor(size) + Math.Max(0, ringColumns - WidgetViewModel.MinRingColumns) * RingColumnWidthFor(size);

    private void ApplySize()
    {
        var target = PanelWidthFor(_viewModel.Size, _viewModel.RingColumns);
        if (PanelView.Width == target)
            return;

        // Se o widget está do lado direito da tela, a borda direita fica parada ao mudar de tamanho.
        var width = (int)(ClientSize.Width * DesktopScaling);
        if (Screens.ScreenFromWindow(this) is { } screen && Position.X + width / 2 > screen.WorkingArea.Center.X)
        {
            _keepRightEdge = Position.X + width;
            DispatcherTimer.RunOnce(() => _keepRightEdge = null, TimeSpan.FromMilliseconds(400));
        }

        PanelView.Width = target;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // A janela pode mudar de tamanho em mais de um passo; mantém a borda direita a cada mudança.
        if (change.Property == ClientSizeProperty && _keepRightEdge is int right)
            Position = new PixelPoint(right - (int)(ClientSize.Width * DesktopScaling), Position.Y);
    }

    // ---- Posição ----

    private void PlaceOnScreen(double? left, double? top)
    {
        if (left is double x && top is double y && IsOnScreen((int)x, (int)y))
        {
            Position = new PixelPoint((int)x, (int)y);
            return;
        }

        // Primeira execução (ou monitor desconectado): canto superior direito da tela principal.
        if (Screens.Primary is { } screen)
        {
            var area = screen.WorkingArea;
            var width = (int)((PanelView.Width + 2 * ShadowMargin + 24) * screen.Scaling);
            Position = new PixelPoint(area.Right - width, area.Y + (int)(24 * screen.Scaling));
        }
    }

    private bool IsOnScreen(int x, int y) =>
        Screens.All.Any(s => s.WorkingArea.Contains(new PixelPoint(x + 50, y + 50)));

    // ---- Aparência ----

    private static double PanelMarginFor(string theme) => PanelChrome.IsGlass(theme) ? 0 : ShadowMargin;

    private void ApplyThemeVisuals(string theme)
    {
        // A margem da sombra muda entre os temas; desloca a janela para o painel não "pular".
        var oldMargin = PanelHost.Margin.Left;
        var newMargin = PanelMarginFor(theme);
        if (oldMargin != newMargin)
        {
            PanelHost.Margin = new Thickness(newMargin);
            var delta = (int)((oldMargin - newMargin) * DesktopScaling);
            Position = new PixelPoint(Position.X + delta, Position.Y + delta);
        }

        PanelChrome.Apply(this, PanelView, theme, PanelRadius);
    }

    // ---- Arrastar ----

    private void Panel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Botões e barra de rolagem continuam funcionando normalmente.
        for (var node = e.Source as Visual; node is not null && node != PanelView; node = node.GetVisualParent())
        {
            if (node is Button or ScrollBar)
                return;
        }

        BeginMoveDrag(e);
    }
}
