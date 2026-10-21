using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;
using AiUsageWidget.Tests.Fakes;
using AiUsageWidget.ViewModels;

namespace AiUsageWidget.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly AppSettings _settings = new() { Theme = ThemeNames.Dark };
    private readonly FakeSettingsStore _store = new();
    private readonly FakeStartupRegistration _startup = new();
    private readonly FakeThemeService _themes = new();

    private readonly IUsageProvider[] _providers =
    [
        new FakeUsageProvider(ProviderIds.Claude, () => new ProviderUsage(null, [])),
        new FakeUsageProvider(ProviderIds.Codex, () => new ProviderUsage(null, [])),
    ];

    private SettingsViewModel Create() => new(_settings, _providers, _store, _startup, _themes);

    [Fact]
    public void Abre_ComOsValoresAtuais()
    {
        _settings.Size = WidgetSize.Large;
        _settings.HiddenProviders = [ProviderIds.Codex];

        var vm = Create();

        Assert.Equal(ThemeNames.Dark, vm.Theme);
        Assert.Equal(WidgetSize.Large, vm.Size);
        Assert.False(vm.StartWithWindows);
        Assert.Equal([true, false], vm.Providers.Select(p => p.IsVisible));
        Assert.Null(_themes.Applied);
    }

    [Fact]
    public void TrocarTema_MostraNaHora()
    {
        var vm = Create();

        vm.Theme = ThemeNames.Light;

        Assert.Equal(ThemeNames.Light, _themes.Applied);
        Assert.Equal(ThemeNames.Dark, _settings.Theme);
    }

    [Fact]
    public void Salvar_GravaTudoEFecha()
    {
        var vm = Create();
        bool? result = null;
        vm.CloseRequested += (_, saved) => result = saved;

        vm.Theme = ThemeNames.Light;
        vm.Size = WidgetSize.Small;
        vm.StartWithWindows = true;
        Assert.True(vm.Notifications);
        vm.Notifications = false;
        vm.Providers[0].IsVisible = false;
        vm.SaveCommand.Execute(null);

        Assert.True(result);
        Assert.False(_settings.Notifications);
        Assert.False(_store.Saved?.Notifications);
        Assert.Equal(ThemeNames.Light, _settings.Theme);
        Assert.Equal(WidgetSize.Small, _settings.Size);
        Assert.Equal([ProviderIds.Claude], _settings.HiddenProviders);
        Assert.Equal(WidgetSize.Small, _store.Saved?.Size);
        Assert.True(_startup.IsEnabled);
    }

    [Fact]
    public void Salvar_SemNenhumaConta_Recusa()
    {
        var vm = Create();
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        foreach (var provider in vm.Providers)
            provider.IsVisible = false;
        vm.SaveCommand.Execute(null);

        Assert.False(closed);
        Assert.Equal(SettingsViewModel.NoProviderMessage, vm.ErrorMessage);
        Assert.Null(_store.Saved);
    }

    [Fact]
    public void Salvar_SemMudarIniciarComWindows_NaoMexeNoRegistro()
    {
        var vm = Create();

        vm.SaveCommand.Execute(null);

        Assert.Equal(0, _startup.SetCount);
    }

    [Fact]
    public void Cancelar_NaoGravaNada()
    {
        var vm = Create();
        bool? result = null;
        vm.CloseRequested += (_, saved) => result = saved;

        vm.Size = WidgetSize.Small;
        vm.CancelCommand.Execute(null);

        Assert.False(result);
        Assert.Equal(WidgetSize.Medium, _settings.Size);
        Assert.Null(_store.Saved);
    }
}
