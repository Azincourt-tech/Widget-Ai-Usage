using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Abstractions;
using AiUsageWidget.Tests.Fakes;
using AiUsageWidget.ViewModels;

namespace AiUsageWidget.Tests.ViewModels;

public class TrayPanelViewModelTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start);
    private readonly FakeUiTimer _timer = new();
    private readonly FakeThemeService _themes = new();
    private readonly FakeSettingsStore _store = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly AppSettings _settings = new();

    private WidgetViewModel Create(params IUsageProvider[] providers) =>
        new(_settings, providers, _store, _themes, _dialogs, _notifications, _timer, _time);

    private static FakeUsageProvider Provider(string id, params double[] percents) =>
        new(id, () => new ProviderUsage("Pro", [.. percents.Select(p => new UsageWindow(UsageWindowKind.Weekly, p, Start.AddDays(2)))]));

    [Fact]
    public async Task Dica_MaiorUsoDeCadaConta()
    {
        var vm = Create(Provider(ProviderIds.Claude, 42, 10), Provider(ProviderIds.Codex, 26.6));

        await vm.InitializeAsync();

        Assert.Equal($"{ProviderIds.Claude}: 42%\n{ProviderIds.Codex}: 27%", vm.TrayToolTip);
    }

    [Fact]
    public async Task Dica_SemLeituraESemLogin()
    {
        var signedOut = new FakeUsageProvider(ProviderIds.Codex, () => throw new UsageProviderException("Entre.", requiresSignIn: true));
        var broken = new FakeUsageProvider(ProviderIds.Antigravity, () => throw new UsageProviderException("Sem rede."));
        var vm = Create(signedOut, broken);

        await vm.InitializeAsync();

        Assert.Equal($"{ProviderIds.Codex}: entrar\n{ProviderIds.Antigravity}: —", vm.TrayToolTip);
    }

    [Fact]
    public void Dica_SemContas() =>
        Assert.Equal(TrayToolTip.Empty, Create().TrayToolTip);

    [Fact]
    public void Dica_LongaECortada()
    {
        var card = new ProviderCardViewModel(new FakeUsageProvider(new string('x', 200), () => new ProviderUsage(null, [])), _ => Task.CompletedTask);

        var text = TrayToolTip.For([card]);

        Assert.Equal(TrayToolTip.MaxLength, text.Length);
        Assert.EndsWith("...", text);
    }

    [Fact]
    public async Task Dica_ContaEscondidaNaoAparece()
    {
        _settings.HiddenProviders = [ProviderIds.Codex];
        var vm = Create(Provider(ProviderIds.Claude, 30), Provider(ProviderIds.Codex, 99));

        await vm.InitializeAsync();

        Assert.Equal($"{ProviderIds.Claude}: 30%", vm.TrayToolTip);
    }

    [Fact]
    public async Task Aba_ComecaNaPrimeiraConta()
    {
        var vm = Create(Provider(ProviderIds.Claude, 1), Provider(ProviderIds.Codex, 2));
        await vm.InitializeAsync();

        var panel = new TrayPanelViewModel(vm);

        Assert.Equal(ProviderIds.Claude, panel.SelectedCard?.Id);
        Assert.True(panel.HasSelection);
    }

    [Fact]
    public async Task Aba_Escolhida_ContinuaAoSalvarConfiguracoes()
    {
        var vm = Create(Provider(ProviderIds.Claude, 1), Provider(ProviderIds.Codex, 2), Provider(ProviderIds.Antigravity, 3));
        await vm.InitializeAsync();
        var panel = new TrayPanelViewModel(vm);
        panel.SelectedCard = vm.Providers[1];
        _dialogs.OnShow = (settings, _) =>
        {
            settings.HiddenProviders = [ProviderIds.Claude];
            return true;
        };

        await vm.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(ProviderIds.Codex, panel.SelectedCard?.Id);
    }

    [Fact]
    public async Task Aba_ContaEscondida_VaiParaAPrimeiraEVoltaQuandoAparece()
    {
        var vm = Create(Provider(ProviderIds.Claude, 1), Provider(ProviderIds.Codex, 2), Provider(ProviderIds.Antigravity, 3));
        await vm.InitializeAsync();
        var panel = new TrayPanelViewModel(vm);
        panel.SelectedCard = vm.Providers[1];

        _dialogs.OnShow = (settings, _) =>
        {
            settings.HiddenProviders = [ProviderIds.Codex];
            return true;
        };
        await vm.OpenSettingsCommand.ExecuteAsync(null);
        Assert.Equal(ProviderIds.Claude, panel.SelectedCard?.Id);

        _dialogs.OnShow = (settings, _) =>
        {
            settings.HiddenProviders = [];
            return true;
        };
        await vm.OpenSettingsCommand.ExecuteAsync(null);
        Assert.Equal(ProviderIds.Codex, panel.SelectedCard?.Id);
    }

    [Fact]
    public async Task Aba_ViewLimpaASelecao_VoltaParaAEscolhida()
    {
        var vm = Create(Provider(ProviderIds.Claude, 1), Provider(ProviderIds.Codex, 2));
        await vm.InitializeAsync();
        var panel = new TrayPanelViewModel(vm);
        panel.SelectedCard = vm.Providers[1];

        panel.SelectedCard = null;

        Assert.Equal(ProviderIds.Codex, panel.SelectedCard?.Id);
    }

    [Fact]
    public void Aba_SemContas_NenhumaSelecionada()
    {
        var panel = new TrayPanelViewModel(Create());

        Assert.Null(panel.SelectedCard);
        Assert.False(panel.HasSelection);
    }

    [Fact]
    public async Task Card_PicoDaConta()
    {
        var vm = Create(Provider(ProviderIds.Claude, 12, 67.4));

        Assert.Equal("—", vm.Providers[0].PeakText);
        await vm.InitializeAsync();

        Assert.Equal(67.4, vm.Providers[0].PeakPercent);
        Assert.Equal("67%", vm.Providers[0].PeakText);
    }

    [Fact]
    public async Task Modo_MudaAoSalvarConfiguracoes()
    {
        var vm = Create(Provider(ProviderIds.Claude, 1));
        Assert.Equal(DisplayMode.Widget, vm.Mode);
        Assert.True(vm.ShowsWidget);
        Assert.False(vm.ShowsTray);
        _dialogs.OnShow = (settings, _) =>
        {
            settings.Mode = DisplayMode.Tray;
            return true;
        };

        await vm.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(DisplayMode.Tray, vm.Mode);
        Assert.False(vm.ShowsWidget);
        Assert.True(vm.ShowsTray);
    }

    [Fact]
    public async Task Configuracoes_UmaTelaSo()
    {
        var gate = new TaskCompletionSource<bool>();
        var dialogs = new GatedDialogService(gate.Task);
        var vm = new WidgetViewModel(_settings, [Provider(ProviderIds.Claude, 1)], _store, _themes, dialogs, _notifications, _timer, _time);

        var first = vm.OpenSettingsCommand.ExecuteAsync(null);
        await vm.OpenSettingsCommand.ExecuteAsync(null);
        gate.SetResult(false);
        await first;

        Assert.Equal(1, dialogs.Opened);
    }

    /// <summary>Tela de configurações que só fecha quando o teste mandar.</summary>
    private sealed class GatedDialogService(Task<bool> result) : IDialogService
    {
        public int Opened { get; private set; }

        public Task<bool> ShowSettingsAsync(AppSettings settings, IReadOnlyList<IUsageProvider> providers)
        {
            Opened++;
            return result;
        }
    }
}
