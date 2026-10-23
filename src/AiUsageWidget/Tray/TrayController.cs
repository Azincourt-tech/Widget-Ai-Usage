using System.ComponentModel;
using AiUsageWidget.ViewModels;
using AiUsageWidget.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AiUsageWidget.Tray;

/// <summary>
/// Ícone na bandeja: o ícone do widget, a dica com o uso de cada conta, o menu do clique direito e a
/// janelinha que abre no clique esquerdo.
/// </summary>
internal sealed class TrayController : IDisposable
{
    // O clique no ícone tira o foco da janelinha (ela fecha) antes de chegar aqui: sem esta folga, o mesmo
    // clique que fechou a abriria de novo.
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(400);

    private readonly WidgetViewModel _viewModel;
    private readonly TrayIcon _icon;
    private readonly TrayIcons _icons;
    private TrayPanelWindow? _panel;

    public TrayController(WidgetViewModel viewModel)
    {
        _viewModel = viewModel;

        var menu = new NativeMenu();
        menu.Add(MenuItem("Atualizar agora", () => _viewModel.RefreshCommand.Execute(null)));
        menu.Add(MenuItem("Configurações...", OpenSettings));
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(MenuItem("Sair", () => _viewModel.CloseCommand.Execute(null)));

        // O mesmo ícone do .exe e das janelas (o .ico tem de 16 a 256 px: o Windows escolhe o da escala).
        using var iconFile = AssetLoader.Open(new Uri("avares://AiUsageWidget/Assets/icon.ico"));
        _icon = new TrayIcon
        {
            Icon = new WindowIcon(iconFile),
            ToolTipText = _viewModel.TrayToolTip,
            Menu = menu,
            IsVisible = false,
        };
        _icon.Clicked += (_, _) => TogglePanel();
        _icons = new TrayIcons { _icon };
        TrayIcon.SetIcons(Application.Current!, _icons);

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public void Show() => _icon.IsVisible = true;

    public void Hide()
    {
        _panel?.Hide();
        _icon.IsVisible = false;
    }

    public void Dispose()
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _panel?.Close();
        _icon.IsVisible = false;
        _icon.Dispose();
    }

    private static NativeMenuItem MenuItem(string header, Action onClick)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => onClick();
        return item;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetViewModel.TrayToolTip))
            _icon.ToolTipText = _viewModel.TrayToolTip;
    }

    private void TogglePanel()
    {
        _panel ??= new TrayPanelWindow(new TrayPanelViewModel(_viewModel));
        if (_panel.IsVisible)
        {
            _panel.Hide();
            return;
        }

        if (DateTime.UtcNow - _panel.HiddenAt < ReopenGuard)
            return;

        _panel.ShowNearCursor();
    }

    private void OpenSettings()
    {
        _panel?.Hide();
        _viewModel.OpenSettingsCommand.Execute(null);
    }
}
