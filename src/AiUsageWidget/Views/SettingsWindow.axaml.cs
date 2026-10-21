using AiUsageWidget.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AiUsageWidget.Views;

/// <summary>
/// Só comportamento visual: arrastar a janela e fechar com o resultado pedido pelo ViewModel.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>Usado pelo designer visual do Avalonia.</summary>
    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += (_, saved) => Close(saved);

        // Arrastar por qualquer parte da janela (como o widget), inclusive sobre textos e grupos.
        Root.AddHandler(PointerPressedEvent, Root_PointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void Root_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Botões, interruptores e opções do controle segmentado continuam funcionando normalmente.
        for (var node = e.Source as Visual; node is not null && node != Root; node = node.GetVisualParent())
        {
            if (node is Button or ToggleButton)
                return;
        }

        BeginMoveDrag(e);
    }
}
