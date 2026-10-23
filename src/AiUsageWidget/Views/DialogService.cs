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

        // Antes de criar a janela: este aviso precisa chegar antes do "fechou" da própria janela.
        var result = new TaskCompletionSource<bool>();
        viewModel.CloseRequested += (_, saved) => result.TrySetResult(saved);

        var window = new SettingsWindow(viewModel);
        window.Closed += (_, _) => result.TrySetResult(false);

        // Com o widget na tela, as configurações ficam presas a ele; no modo bandeja, abrem sozinhas.
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is { IsVisible: true })
        {
            await window.ShowDialog(owner);
        }
        else
        {
            window.Show();
            window.Activate();
        }

        return await result.Task;
    }
}
