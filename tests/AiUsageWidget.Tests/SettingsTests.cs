using System.IO;
using AiUsageWidget.Models;
using AiUsageWidget.Services;

namespace AiUsageWidget.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "AiUsageWidget.Tests", Guid.NewGuid().ToString("N"));

    private string ConfigPath => Path.Combine(_folder, "config.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Normalize_CorrigeValoresInvalidos()
    {
        var settings = new AppSettings { Theme = "Neon", Left = double.NaN, Top = double.PositiveInfinity }.Normalize();

        Assert.Equal(ThemeNames.Glass, settings.Theme);
        Assert.Null(settings.Left);
        Assert.Null(settings.Top);
    }

    [Fact]
    public void Normalize_TamanhoInvalido_VoltaAoMedio() =>
        Assert.Equal(WidgetSize.Medium, new AppSettings { Size = (WidgetSize)42 }.Normalize().Size);

    [Fact]
    public void Normalize_ContasEscondidas_SoAsConhecidasESemRepetir()
    {
        var settings = new AppSettings { HiddenProviders = [ProviderIds.Codex, "outra", ProviderIds.Codex] }.Normalize();

        Assert.Equal([ProviderIds.Codex], settings.HiddenProviders);
        Assert.False(settings.IsVisible(ProviderIds.Codex));
        Assert.True(settings.IsVisible(ProviderIds.Claude));
    }

    [Fact]
    public void Store_GravaTamanhoComoTextoELe()
    {
        var store = new SettingsStore(ConfigPath);
        store.Save(new AppSettings { Size = WidgetSize.Large, HiddenProviders = [ProviderIds.Claude] });

        Assert.Contains("\"Large\"", File.ReadAllText(ConfigPath));
        var loaded = store.Load();
        Assert.Equal(WidgetSize.Large, loaded.Size);
        Assert.Equal([ProviderIds.Claude], loaded.HiddenProviders);
    }

    [Fact]
    public void Store_ArquivoAntigoSemAvisos_FicamLigados()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(ConfigPath, """{"Theme": "Dark"}""");

        Assert.True(new SettingsStore(ConfigPath).Load().Notifications);
    }

    [Theory]
    [InlineData("--size=small", WidgetSize.Small)]
    [InlineData("--size=Large", WidgetSize.Large)]
    [InlineData("--size=enorme", null)]
    [InlineData("--size=7", null)]
    public void StartupOptions_LeOTamanho(string arg, WidgetSize? expected) =>
        Assert.Equal(expected, StartupOptions.Parse(["--demo", arg]).Size);

    [Fact]
    public void Store_SemArquivo_UsaPadrao()
    {
        var settings = new SettingsStore(ConfigPath).Load();

        Assert.Equal(ThemeNames.Glass, settings.Theme);
        Assert.False(settings.Pinned);
        Assert.True(settings.Notifications);
    }

    [Fact]
    public void Store_GravaELe()
    {
        var store = new SettingsStore(ConfigPath);
        store.Save(new AppSettings { Theme = ThemeNames.Light, Pinned = true, Left = 10, Top = 20 });

        var loaded = store.Load();

        Assert.Equal(ThemeNames.Light, loaded.Theme);
        Assert.True(loaded.Pinned);
        Assert.Equal(10, loaded.Left);
        Assert.Equal(20, loaded.Top);
        Assert.False(File.Exists(ConfigPath + ".tmp"));
    }

    [Fact]
    public void Store_ConfigDeVersaoAntiga_IgnoraCampoQueSaiu()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(ConfigPath, """{ "Theme": "Light", "RefreshMinutes": 15 }""");

        Assert.Equal(ThemeNames.Light, new SettingsStore(ConfigPath).Load().Theme);
    }

    [Fact]
    public void Store_ArquivoCorrompido_UsaPadrao()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(ConfigPath, "{ isto não é json");

        var settings = new SettingsStore(ConfigPath).Load();

        Assert.Equal(ThemeNames.Glass, settings.Theme);
    }
}
