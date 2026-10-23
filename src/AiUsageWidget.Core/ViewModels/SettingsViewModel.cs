using System.Collections.ObjectModel;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiUsageWidget.ViewModels;

/// <summary>
/// Tela de configurações. Edita uma cópia dos valores e só grava nas configurações recebidas quando
/// o usuário salva e tudo foi validado. O tema muda na hora, como prévia; quem abriu a tela reaplica
/// o tema salvo ao fechar.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public const string NoProviderMessage = "Escolha pelo menos uma conta para mostrar.";

    private readonly AppSettings _settings;
    private readonly ISettingsStore _store;
    private readonly IStartupRegistration _startup;
    private readonly IThemeService _themes;
    private readonly bool _initialized;

    [ObservableProperty]
    private string _theme;

    [ObservableProperty]
    private WidgetSize _size;

    [ObservableProperty]
    private DisplayMode _mode;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _notifications;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public SettingsViewModel(
        AppSettings settings,
        IEnumerable<IUsageProvider> providers,
        ISettingsStore store,
        IStartupRegistration startup,
        IThemeService themes)
    {
        _settings = settings;
        _store = store;
        _startup = startup;
        _themes = themes;

        _theme = ThemeNames.Normalize(settings.Theme);
        _size = settings.Size;
        _mode = settings.Mode;
        _startWithWindows = startup.IsEnabled;
        _notifications = settings.Notifications;
        foreach (var provider in providers)
            Providers.Add(new ProviderToggleViewModel(provider.Id, provider.DisplayName, settings.IsVisible(provider.Id)));

        _initialized = true;
    }

    /// <summary>A View fecha a janela com o resultado (true = salvou).</summary>
    public event EventHandler<bool>? CloseRequested;

    public ObservableCollection<ProviderToggleViewModel> Providers { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnThemeChanged(string value)
    {
        if (_initialized)
            _themes.Apply(value);
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = null;
        if (Providers.Count > 0 && Providers.All(p => !p.IsVisible))
        {
            ErrorMessage = NoProviderMessage;
            return;
        }

        var updated = _settings.Clone();
        updated.Theme = Theme;
        updated.Size = Size;
        updated.Mode = Mode;
        updated.Notifications = Notifications;
        updated.HiddenProviders = [.. Providers.Where(p => !p.IsVisible).Select(p => p.Id)];
        updated.Normalize();

        try
        {
            _store.Save(updated);
            if (StartWithWindows != _startup.IsEnabled)
                _startup.Set(StartWithWindows);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Falha ao salvar as configurações.", ex);
            ErrorMessage = "Não foi possível salvar as configurações.";
            return;
        }

        CopyTo(updated, _settings);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    private static void CopyTo(AppSettings source, AppSettings target)
    {
        target.Theme = source.Theme;
        target.Size = source.Size;
        target.Mode = source.Mode;
        target.Notifications = source.Notifications;
        target.HiddenProviders = source.HiddenProviders;
    }
}
