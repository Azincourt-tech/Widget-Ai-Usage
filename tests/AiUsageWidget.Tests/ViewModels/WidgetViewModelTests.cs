using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Abstractions;
using AiUsageWidget.Services.Demo;
using AiUsageWidget.Tests.Fakes;
using AiUsageWidget.ViewModels;

namespace AiUsageWidget.Tests.ViewModels;

public class WidgetViewModelTests
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

    private static ProviderUsage Usage(params UsageWindow[] windows) => new("Max", windows);

    [Fact]
    public async Task Initialize_PreencheOsCardsELigaORelogio()
    {
        var claude = new FakeUsageProvider(ProviderIds.Claude, () => Usage(
            new UsageWindow(UsageWindowKind.FiveHours, 42, Start.AddMinutes(30)),
            new UsageWindow(UsageWindowKind.Weekly, 80, Start.AddDays(3))));
        var vm = Create(claude);

        await vm.InitializeAsync();

        Assert.True(_timer.IsEnabled);
        var card = Assert.Single(vm.Providers);
        Assert.Equal("Max", card.Plan);
        Assert.False(card.HasError);
        Assert.Equal(["42%", "80%"], card.Windows.Select(w => w.PercentText));
        Assert.Equal("renova em 30 min", card.Windows[0].ResetText);
        Assert.Equal("Atualizado agora", vm.Subtitle);
    }

    [Fact]
    public async Task Anel_UsaCorDoProvedor_OuAlertaPertoDoLimite()
    {
        var vm = Create(new FakeUsageProvider(ProviderIds.Codex, () => Usage(
            new UsageWindow(UsageWindowKind.FiveHours, 10, null),
            new UsageWindow(UsageWindowKind.Weekly, 80, null),
            new UsageWindow(UsageWindowKind.Daily, 95, null))));

        await vm.InitializeAsync();

        var colors = vm.Providers[0].Windows.Select(w => w.RingColor).ToArray();
        Assert.Equal([ProviderIds.AccentFor(ProviderIds.Codex), "#FF9F0A", "#FF453A"], colors);
    }

    [Fact]
    public async Task ColunasDeAneis_AcompanhamAContaComMaisLimites_EmTodasAsContas()
    {
        var claudeWindows = 3;
        var claude = new FakeUsageProvider(ProviderIds.Claude, () => Usage([.. Enumerable.Range(0, claudeWindows)
            .Select(i => new UsageWindow(UsageWindowKind.Weekly, i, null))]));
        var codex = new FakeUsageProvider(ProviderIds.Codex, () => Usage(new UsageWindow(UsageWindowKind.Weekly, 1, null)));
        var vm = Create(claude, codex);
        Assert.Equal(WidgetViewModel.MinRingColumns, vm.RingColumns);

        await vm.InitializeAsync();

        Assert.Equal(3, vm.RingColumns);
        Assert.All(vm.Providers, card => Assert.Equal(3, card.RingColumns));

        // O limite extra sumiu: volta ao mínimo de 2 colunas (nunca menos, mesmo com um anel só).
        claudeWindows = 1;
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.RingColumns);
        Assert.All(vm.Providers, card => Assert.Equal(2, card.RingColumns));
    }

    [Fact]
    public async Task Aviso_SoAoCruzar80Ou95_ComAvisosLigados()
    {
        var percent = 85.0;
        var claude = new FakeUsageProvider(ProviderIds.Claude, () => Usage(
            new UsageWindow(UsageWindowKind.Weekly, percent, Start.AddMinutes(30))));
        var vm = Create(claude);

        // Abriu com 85%: o anel já mostra, não avisa.
        await vm.InitializeAsync();
        Assert.Empty(_notifications.Shown);

        percent = 96;
        await vm.RefreshCommand.ExecuteAsync(null);
        var (title, message) = Assert.Single(_notifications.Shown);
        Assert.Equal("claude: Semanal em 96%", title);
        Assert.Equal("Quase no fim: passou de 95% do limite; renova em 30 min.", message);

        // Desligado nas configurações: não avisa.
        _settings.Notifications = false;
        percent = 10;
        await vm.RefreshCommand.ExecuteAsync(null);
        percent = 90;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Single(_notifications.Shown);
    }

    [Fact]
    public async Task ColunasDeAneis_ContaEscondida_NaoConta()
    {
        static FakeUsageProvider WithRings(string id, int count) => new(id, () => new ProviderUsage(null,
            [.. Enumerable.Range(0, count).Select(i => new UsageWindow(UsageWindowKind.Weekly, i, null))]));
        var vm = Create(WithRings(ProviderIds.Claude, 4), WithRings(ProviderIds.Codex, 2), WithRings(ProviderIds.Antigravity, 3));
        await vm.InitializeAsync();
        Assert.Equal(4, vm.RingColumns);

        // Escondeu o Claude (o de mais anéis): vale o maior entre os que sobraram.
        _dialogs.OnShow = (settings, _) =>
        {
            settings.HiddenProviders = [ProviderIds.Claude];
            return true;
        };
        await vm.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.RingColumns);
        Assert.All(vm.Providers, card => Assert.Equal(3, card.RingColumns));
    }

    [Fact]
    public async Task ErroEsperado_ApareceNoCard_SemDerrubarOsOutros()
    {
        var failing = new FakeUsageProvider(ProviderIds.Claude, () => throw new UsageProviderException("Faça login no Claude Code."));
        var working = new FakeUsageProvider(ProviderIds.Antigravity, () => Usage(new UsageWindow(UsageWindowKind.Daily, 20, null)));
        var vm = Create(failing, working);

        await vm.InitializeAsync();

        Assert.Equal("Faça login no Claude Code.", vm.Providers[0].ErrorMessage);
        Assert.False(vm.Providers[1].HasError);
        Assert.Single(vm.Providers[1].Windows);
    }

    [Fact]
    public async Task ErroInesperado_MostraMensagemGenerica()
    {
        var vm = Create(new FakeUsageProvider(ProviderIds.Codex, () => throw new InvalidOperationException("detalhe interno")));

        await vm.InitializeAsync();

        Assert.Equal("Não foi possível ler os limites agora.", vm.Providers[0].ErrorMessage);
    }

    [Fact]
    public async Task Erro_MantemOsUltimosLimitesLidos()
    {
        var fail = false;
        var vm = Create(new FakeUsageProvider(ProviderIds.Claude, () => fail
            ? throw new UsageProviderException("Sem conexão.")
            : Usage(new UsageWindow(UsageWindowKind.Weekly, 50, null))));
        await vm.InitializeAsync();

        fail = true;
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Sem conexão.", vm.Providers[0].ErrorMessage);
        Assert.Single(vm.Providers[0].Windows);
    }

    private const int Pause = WidgetViewModel.UnchangedReadsBeforePause;

    /// <summary>Avança o relógio um intervalo e dispara o temporizador, <paramref name="times"/> vezes.</summary>
    private void Tick(int times = 1, Action<int>? before = null)
    {
        for (var i = 1; i <= times; i++)
        {
            before?.Invoke(i);
            _time.Now += WidgetViewModel.RefreshInterval;
            _timer.Fire();
        }
    }

    [Fact]
    public void Constantes_DecididasPeloUsuario()
    {
        _ = Create();

        Assert.Equal(TimeSpan.FromMinutes(1), _timer.Interval);
        Assert.Equal(20, WidgetViewModel.UnchangedReadsBeforePause);
        Assert.Equal(TimeSpan.FromMinutes(2), WidgetViewModel.IdlePause);
    }

    [Fact]
    public async Task VariasLeiturasSemMudanca_PausaBreveEDepoisVolta()
    {
        var claude = new FakeUsageProvider(ProviderIds.Claude, () => Usage(new UsageWindow(UsageWindowKind.Weekly, 10, Start.AddDays(3))));
        var vm = Create(claude);
        await vm.InitializeAsync(); // leitura de referência

        // N leituras iguais à anterior: na N-ésima a conta entra em pausa.
        Tick(Pause);
        Assert.Equal(1 + Pause, claude.Calls);
        Assert.False(vm.Providers[0].HasError);
        var pausedAt = _time.Now;

        // Durante a pausa não consulta.
        Tick();
        Assert.Equal(1 + Pause, claude.Calls);

        // Passada a pausa, volta a consultar.
        _time.Now = pausedAt + WidgetViewModel.IdlePause + TimeSpan.FromSeconds(1);
        _timer.Fire();
        Assert.Equal(2 + Pause, claude.Calls);
    }

    [Fact]
    public async Task UsoMudou_ZeraAContagem()
    {
        var percent = 10.0;
        var claude = new FakeUsageProvider(ProviderIds.Claude, () => Usage(new UsageWindow(UsageWindowKind.Weekly, percent, null)));
        var vm = Create(claude);
        await vm.InitializeAsync();

        // Muda na 4ª leitura; depois dela, N-1 leituras iguais ainda não pausam.
        Tick(3 + Pause, i => { if (i == 4) percent = 11; });
        Assert.Equal(4 + Pause, claude.Calls);

        // A N-ésima igual pausa: a seguinte é pulada.
        Tick(2);
        Assert.Equal(5 + Pause, claude.Calls);
    }

    [Fact]
    public async Task AtualizarAgora_IgnoraAPausaPorFaltaDeMudanca()
    {
        var claude = new FakeUsageProvider(ProviderIds.Claude, () => Usage(new UsageWindow(UsageWindowKind.Weekly, 10, null)));
        var vm = Create(claude);
        await vm.InitializeAsync();
        Tick(Pause);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2 + Pause, claude.Calls);
    }

    [Fact]
    public async Task CadaTique_ReleTudoEAtualizaOsTextos()
    {
        var provider = new FakeUsageProvider(ProviderIds.Claude, () => Usage(new UsageWindow(UsageWindowKind.FiveHours, 42, Start.AddMinutes(30))));
        var vm = Create(provider);
        await vm.InitializeAsync();

        _time.Now = Start.AddMinutes(5);
        _timer.Fire();

        Assert.Equal(2, provider.Calls);
        Assert.Equal("renova em 25 min", vm.Providers[0].Windows[0].ResetText);
        Assert.Equal("Atualizado agora", vm.Subtitle);
    }

    [Fact]
    public async Task AtualizacaoAutomatica_ESilenciosa()
    {
        var vm = Create(new FakeUsageProvider(ProviderIds.Claude, () => Usage(new UsageWindow(UsageWindowKind.Weekly, 10, null))));
        await vm.InitializeAsync();
        var loadingSeen = false;
        var subtitles = new List<string>();
        vm.Providers[0].PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProviderCardViewModel.IsLoading) && vm.Providers[0].IsLoading)
                loadingSeen = true;
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WidgetViewModel.Subtitle))
                subtitles.Add(vm.Subtitle);
        };

        _timer.Fire();

        Assert.False(loadingSeen);
        Assert.DoesNotContain("Atualizando...", subtitles);
    }

    [Fact]
    public async Task MuitasConsultas_PausaSoAquelaConta()
    {
        var limited = false;
        var claude = new FakeUsageProvider(ProviderIds.Claude, () => limited
            ? throw UsageProviderException.RateLimited()
            : Usage(new UsageWindow(UsageWindowKind.Weekly, 10, null)));
        var codex = new FakeUsageProvider(ProviderIds.Codex, () => Usage(new UsageWindow(UsageWindowKind.Weekly, 20, null)));
        var vm = Create(claude, codex);
        await vm.InitializeAsync();

        limited = true;
        _timer.Fire();

        // Mostra o aviso, mantém os últimos limites e não consulta de novo nos próximos tiques.
        Assert.Equal("Muitas consultas seguidas; pausado por alguns minutos.", vm.Providers[0].ErrorMessage);
        Assert.Single(vm.Providers[0].Windows);
        _time.Now = Start.AddMinutes(1);
        _timer.Fire();
        Assert.Equal(2, claude.Calls);
        Assert.Equal(3, codex.Calls);

        // Passada a pausa, volta a consultar.
        limited = false;
        _time.Now = Start + WidgetViewModel.RateLimitPause + TimeSpan.FromSeconds(1);
        _timer.Fire();
        Assert.Equal(3, claude.Calls);
        Assert.False(vm.Providers[0].HasError);
    }

    [Fact]
    public async Task AtualizarAgora_IgnoraAPausa()
    {
        var claude = new FakeUsageProvider(ProviderIds.Claude, () => throw UsageProviderException.RateLimited());
        var vm = Create(claude);
        await vm.InitializeAsync();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, claude.Calls);
    }

    [Fact]
    public async Task SemContas_MostraAviso()
    {
        var vm = Create();

        await vm.InitializeAsync();

        Assert.False(vm.HasProviders);
        Assert.Equal("Nenhuma conta para mostrar", vm.Subtitle);
    }

    [Fact]
    public void ContasEscondidas_NaoAparecem()
    {
        _settings.HiddenProviders = [ProviderIds.Codex];

        var vm = Create(
            new FakeUsageProvider(ProviderIds.Claude, () => Usage()),
            new FakeUsageProvider(ProviderIds.Codex, () => Usage()));

        Assert.Equal([ProviderIds.Claude], vm.Providers.Select(p => p.Id));
    }

    [Fact]
    public async Task ContaEscondida_NaoEConsultada()
    {
        _settings.HiddenProviders = [ProviderIds.Codex];
        var codex = new FakeUsageProvider(ProviderIds.Codex, () => Usage());
        var vm = Create(new FakeUsageProvider(ProviderIds.Claude, () => Usage()), codex);

        await vm.InitializeAsync();

        Assert.Equal(0, codex.Calls);
    }

    [Fact]
    public async Task Configuracoes_Salvas_AplicamTemaTamanhoEContas()
    {
        var codex = new FakeUsageProvider(ProviderIds.Codex, () => Usage());
        var vm = Create(new FakeUsageProvider(ProviderIds.Claude, () => Usage()), codex);
        _dialogs.OnShow = (settings, providers) =>
        {
            Assert.Equal(2, providers.Count);
            settings.Theme = ThemeNames.Light;
            settings.Size = WidgetSize.Large;
            settings.HiddenProviders = [ProviderIds.Claude];
            return true;
        };

        await vm.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(ThemeNames.Light, vm.Theme);
        Assert.Equal(ThemeNames.Light, _themes.Applied);
        Assert.Equal(WidgetSize.Large, vm.Size);
        Assert.True(vm.IsLarge);
        Assert.Equal([ProviderIds.Codex], vm.Providers.Select(p => p.Id));
        Assert.Equal(1, codex.Calls);
    }

    [Fact]
    public async Task Configuracoes_Canceladas_VoltamAoTemaSalvo()
    {
        var vm = Create();
        _dialogs.OnShow = (_, _) => false;

        await vm.OpenSettingsCommand.ExecuteAsync(null);

        // A prévia pode ter trocado as cores; ao cancelar, o tema salvo é reaplicado.
        Assert.Equal(ThemeNames.Glass, _themes.Applied);
        Assert.Equal(ThemeNames.Glass, vm.Theme);
    }

    [Fact]
    public void Tamanho_VemDasConfiguracoes()
    {
        _settings.Size = WidgetSize.Small;

        var vm = Create();

        Assert.True(vm.IsSmall);
        Assert.False(vm.IsLarge);
    }

    [Fact]
    public void Fixar_Salva()
    {
        var vm = Create();

        vm.TogglePinCommand.Execute(null);

        Assert.True(vm.IsPinned);
        Assert.True(_store.Saved?.Pinned);
    }

    [Fact]
    public void Construtor_NaoSalvaNada()
    {
        _ = Create();

        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task Demo_TemTodasAsContas()
    {
        var vm = Create([.. DemoUsageProvider.CreateAll(TimeProvider.System)]);

        await vm.InitializeAsync();

        Assert.Equal(ProviderIds.All, vm.Providers.Select(p => p.Id));
        Assert.All(vm.Providers, p => Assert.NotEmpty(p.Windows));
    }

    [Fact]
    public async Task RotuloProprio_AparecenoAnel()
    {
        var vm = Create(new FakeUsageProvider(ProviderIds.Antigravity, () =>
            Usage(new UsageWindow(UsageWindowKind.Daily, 30, null, "Pro", "Pro"), new UsageWindow(UsageWindowKind.Daily, 5, null))));

        await vm.InitializeAsync();

        Assert.Equal(["Pro", "Diário"], vm.Providers[0].Windows.Select(w => w.Label));
        Assert.Equal(["Pro", "24h"], vm.Providers[0].Windows.Select(w => w.ShortLabel));
    }
}
