using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;
using AiUsageWidget.ViewModels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace AiUsageWidget.Views;

/// <summary>
/// Abre as janelas pedidas pelos ViewModels, que assim não dependem das telas.
/// </summary>
public sealed class DialogService(ISettingsStore store, IStartupRegistration startup, IThemeService themes) : IDialogService
{
    public async Task<bool> ShowSettingsAsync(AppSettings settings, IReadOnlyList<IUsageProvider> providers)
    {
        var viewModel = new SettingsViewModel(settings, providers, store, startup, themes);
        var window = new SettingsWindow(viewModel);

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        return owner is not null && await window.ShowDialog<bool>(owner);
    }
}
