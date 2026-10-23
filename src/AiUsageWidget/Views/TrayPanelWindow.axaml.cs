using System.ComponentModel;
using AiUsageWidget.Models;
using AiUsageWidget.Platform.Windows;
using AiUsageWidget.Services;
using AiUsageWidget.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AiUsageWidget.Views;

/// <summary>
/// Só comportamento visual da janelinha da bandeja: abrir junto ao cursor, fechar ao perder o foco ou com
/// Esc e o visual de cada tema. Estado e ações ficam no <see cref="TrayPanelViewModel"/>.
/// </summary>
public partial class TrayPanelWindow : Window
{
    private const double PanelRadius = 14;
    private const double ShadowMargin = 10;

    /// <summary>Folga visível entre o painel e a barra de tarefas (ou a borda da tela).</summary>
    private const double Gap = 12;

    /// <summary>
    /// Ao abrir, o Windows pode tirar o foco por um instante (a barra de tarefas se ativa sozinha às vezes):
    /// nesse começo, perder o foco não fecha.
    /// </summary>
    private static readonly TimeSpan DeactivateGrace = TimeSpan.FromMilliseconds(300);

    private readonly TrayPanelViewModel _viewModel = null!;
    private DateTime _shownAt;

    // Onde a janelinha foi aberta (cursor e área livre daquele monitor), para reposicionar quando a altura muda.
    private (PixelPoint Cursor, ScreenRect WorkArea, double Scaling)? _anchor;

    /// <summary>Usado pelo designer visual do Avalonia.</summary>
    public TrayPanelWindow() => InitializeComponent();

    public TrayPanelWindow(TrayPanelViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.Widget.PropertyChanged += Widget_PropertyChanged;
        Closed += (_, _) => viewModel.Widget.PropertyChanged -= Widget_PropertyChanged;
        // Como no widget: o desfoque e os cantos do sistema só pegam com a janela já aberta.
        Opened += (_, _) => ApplyThemeVisuals(_viewModel.Widget.Theme);

        Deactivated += (_, _) =>
        {
            if (DateTime.UtcNow - _shownAt > DeactivateGrace)
                Hide();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Hide();
        };
    }

    /// <summary>Quando foi escondida pela última vez (o ícone usa para o clique que a fechou não a reabrir).</summary>
    public DateTime HiddenAt { get; private set; }

    public void ShowNearCursor()
    {
        var cursor = (OperatingSystem.IsWindows() ? WindowsShell.CursorPosition() : null)
                     ?? Screens.Primary?.WorkingArea.BottomRight
                     ?? default;
        var screen = Screens.ScreenFromPoint(cursor) ?? Screens.Primary;
        _anchor = screen is null
            ? null
            : (cursor, new ScreenRect(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Right, screen.WorkingArea.Bottom), screen.Scaling);

        _shownAt = DateTime.UtcNow;
        Show();

        // Só depois de visível: movida ainda escondida para um monitor com outra escala, a janela não é
        // redimensionada pelo Windows e o conteúdo sai cortado.
        Place();
        Activate();
    }

    public override void Hide()
    {
        HiddenAt = DateTime.UtcNow;
        base.Hide();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // A altura muda com a aba (mais ou menos limites): a janelinha continua encostada na barra de tarefas.
        if (change.Property == ClientSizeProperty && IsVisible)
            Place();
    }

    private void Place()
    {
        if (_anchor is not var (cursor, workArea, scaling))
            return;

        // Antes do primeiro layout o tamanho ainda não é o do conteúdo (vem o da tela): usa uma estimativa e
        // corrige assim que o tamanho real chega (OnPropertyChanged).
        var size = ClientSize.Width == Width ? ClientSize : new Size(Width, 320);
        var margin = PanelHost.Margin.Left;
        var (x, y) = TrayPanelPlacement.Place(
            workArea,
            cursor.X,
            cursor.Y,
            (int)Math.Ceiling(size.Width * scaling),
            (int)Math.Ceiling(size.Height * scaling),
            (int)Math.Round((Gap - margin) * scaling));
        Position = new PixelPoint(x, y);
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        // As configurações abrem como janela própria: a janelinha sai da frente.
        Hide();
        _viewModel.Widget.OpenSettingsCommand.Execute(null);
    }

    private void Widget_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetViewModel.Theme))
            ApplyThemeVisuals(_viewModel.Widget.Theme);
    }

    private void ApplyThemeVisuals(string theme)
    {
        PanelHost.Margin = new Thickness(PanelChrome.IsGlass(theme) ? 0 : ShadowMargin);
        PanelChrome.Apply(this, PanelView, theme, PanelRadius);
    }
}
