using CommunityToolkit.Mvvm.ComponentModel;

namespace AiUsageWidget.ViewModels;

/// <summary>Uma conta na tela de configurações, com o interruptor de mostrar no widget.</summary>
public sealed partial class ProviderToggleViewModel(string id, string name, bool isVisible) : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible = isVisible;

    public string Id => id;

    public string Name => name;
}
