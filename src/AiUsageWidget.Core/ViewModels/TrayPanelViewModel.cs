using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AiUsageWidget.ViewModels;

/// <summary>
/// Janelinha do ícone da bandeja: uma aba por conta visível e os limites da conta escolhida. As leituras,
/// os comandos e o relógio são os do <see cref="WidgetViewModel"/> (o mesmo do widget flutuante).
/// </summary>
public sealed partial class TrayPanelViewModel : ObservableObject
{
    // Conta escolhida pelo usuário: vale mesmo depois de a lista ser refeita (ao salvar as configurações).
    private string? _chosenId;
    private bool _reselecting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ProviderCardViewModel? _selectedCard;

    public TrayPanelViewModel(WidgetViewModel widget)
    {
        Widget = widget;
        widget.Providers.CollectionChanged += Providers_CollectionChanged;
        SelectedCard = widget.Providers.FirstOrDefault();
    }

    public WidgetViewModel Widget { get; }

    public bool HasSelection => SelectedCard is not null;

    /// <summary>A escolhida, se ainda está visível; senão, a primeira.</summary>
    private void Providers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Reselect();

    partial void OnSelectedCardChanged(ProviderCardViewModel? value)
    {
        // A View limpa a seleção quando a lista some por um instante: o "nenhuma" não apaga a escolha.
        if (_reselecting)
            return;

        if (value is not null)
            _chosenId = value.Id;
        else if (Widget.Providers.Count > 0)
            Reselect();
    }

    private void Reselect()
    {
        var target = Widget.Providers.FirstOrDefault(c => c.Id == _chosenId) ?? Widget.Providers.FirstOrDefault();
        if (target == SelectedCard)
            return;

        _reselecting = true;
        try
        {
            SelectedCard = target;
        }
        finally
        {
            _reselecting = false;
        }
    }
}
