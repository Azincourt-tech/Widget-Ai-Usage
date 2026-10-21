using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Platform;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Abstractions;
using AiUsageWidget.Services.Claude;
using AiUsageWidget.Services.Codex;
using AiUsageWidget.Services.Demo;
using AiUsageWidget.Services.Google;
using AiUsageWidget.Services.OAuth;
using AiUsageWidget.Themes;
using AiUsageWidget.ViewModels;
using AiUsageWidget.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace AiUsageWidget;

public partial class App : Application
{
    // Um só HttpClient para o app inteiro (reaproveita conexões).
    private static readonly HttpClient Http = CreateHttpClient();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                // Erro inesperado na interface: registra e mantém o widget aberto.
                ErrorLog.Write("Erro não tratado na interface.", e.Exception);
                e.Handled = true;
            };

            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = CreateMainWindow(StartupOptions.Parse(desktop.Args ?? []));
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Composição: cada serviço é criado uma vez aqui e entregue a quem precisa.
    /// </summary>
    private static MainWindow CreateMainWindow(StartupOptions options)
    {
        var time = TimeProvider.System;

        ISettingsStore store;
        IStartupRegistration startup;
        IReadOnlyList<IUsageProvider> providers;
        if (options.Demo)
        {
            // Contas fictícias, sem tocar no config.json, no registro nem nas contas reais do usuário.
            store = new InMemorySettingsStore(new AppSettings
            {
                Theme = options.Theme ?? ThemeNames.Glass,
                Size = options.Size ?? WidgetSize.Medium,
            });
            startup = new InMemoryStartupRegistration();
            providers = DemoUsageProvider.CreateAll(time);
        }
        else
        {
            store = new SettingsStore(AppConstants.SettingsFile);
            startup = PlatformServices.CreateStartupRegistration();

            var vault = PlatformServices.CreateSecretVault();
            var browser = new BrowserLauncher();
            var claudeAccount = new ClaudeAccount(
                new VaultCredentialStore<ClaudeCredentials>(vault, ClaudeAccount.VaultKey),
                new ClaudeOAuthClient(Http, time),
                browser,
                time);
            var codexAccount = new CodexAccount(
                new VaultCredentialStore<CodexCredentials>(vault, CodexAccount.VaultKey),
                new CodexOAuthClient(Http, time),
                browser,
                time);
            // Antigravity: a mesma cota para o Antigravity IDE e o Antigravity CLI.
            var antigravityAccount = new GoogleAccount(
                "Antigravity",
                new VaultCredentialStore<GoogleCredentials>(vault, GoogleAccount.AntigravityVaultKey),
                new GoogleOAuthClient(Http, time, GoogleOAuthApp.Antigravity),
                browser,
                time);

            providers =
            [
                new ClaudeUsageProvider(claudeAccount, Http),
                new CodexUsageProvider(codexAccount, Http, time),
                new AntigravityUsageProvider(antigravityAccount, new CodeAssistClient(Http), time),
            ];
        }

        var settings = store.Load();
        var themes = new ThemeService();

        // As cores precisam existir antes da primeira janela ser criada.
        themes.Apply(settings.Theme);

        var dialogs = new DialogService(store, startup, themes);
        var viewModel = new WidgetViewModel(
            settings, providers, store, themes, dialogs, PlatformServices.CreateNotificationService(), new AvaloniaUiTimer(), time);
        return new MainWindow(viewModel);
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppConstants.AppName}/{version}");
        return http;
    }
}
