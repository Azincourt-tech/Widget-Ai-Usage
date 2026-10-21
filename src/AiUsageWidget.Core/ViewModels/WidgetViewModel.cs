using System.Collections.ObjectModel;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiUsageWidget.ViewModels;

/// <summary>
/// Estado do widget: um card por conta visível, atualização periódica, tamanho, tema, "fixar" e configurações.
/// </summary>
public sealed partial class WidgetViewModel : ObservableObject
{
    /// <summary>De quanto em quanto tempo os limites são relidos (e os textos "renova em..." recalculados).</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Leituras seguidas sem nenhuma mudança antes de pausar uma conta por <see cref="IdlePause"/>.
    /// Evita consultar à toa quando a conta não está sendo usada (os endereços não são oficiais e têm limite).
    /// </summary>
    public const int UnchangedReadsBeforePause = 20;

    /// <summary>Pausa breve de uma conta que não mudou nas últimas leituras.</summary>
    public static readonly TimeSpan IdlePause = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Quanto tempo uma conta fica sem ser consultada depois de a API responder "muitas consultas" (429).
    /// Insistir logo em seguida poderia piorar o bloqueio.
    /// </summary>
    public static readonly TimeSpan RateLimitPause = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Colunas de anéis no mínimo. Se alguma conta tiver mais limites (ex.: Opus semanal no Claude), todas
    /// ganham mais colunas e o widget cresce para o lado, em vez de abrir uma segunda linha (pedido do usuário).
    /// </summary>
    public const int MinRingColumns = 2;

    private readonly AppSettings _settings;
    private readonly ISettingsStore _store;
    private readonly IThemeService _themes;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly IUiTimer _timer;
    private readonly TimeProvider _time;
    private readonly bool _initialized;
    private readonly UsageAlertTracker _alerts = new();

    // Todas as contas, inclusive as escondidas nas configurações (só as visíveis ficam em Providers).
    private readonly List<ProviderCardViewModel> _allCards = [];

    private DateTimeOffset? _lastRefresh;
    private CancellationTokenSource? _refreshCts;

    [ObservableProperty]
    private string _subtitle = "";

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinToolTip))]
    private bool _isPinned;

    [ObservableProperty]
    private string _theme;

    /// <summary>Colunas de anéis de todas as contas visíveis: as da conta com mais limites (no mínimo 2).</summary>
    [ObservableProperty]
    private int _ringColumns = MinRingColumns;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSmall))]
    [NotifyPropertyChangedFor(nameof(IsLarge))]
    private WidgetSize _size;

    public WidgetViewModel(
        AppSettings settings,
        IEnumerable<IUsageProvider> providers,
        ISettingsStore store,
        IThemeService themes,
        IDialogService dialogs,
        INotificationService notifications,
        IUiTimer timer,
        TimeProvider time)
    {
        _settings = settings;
        _store = store;
        _themes = themes;
        _dialogs = dialogs;
        _notifications = notifications;
        _timer = timer;
        _time = time;

        _isPinned = settings.Pinned;
        _theme = settings.Theme;
        _size = settings.Size;
        foreach (var provider in providers)
        {
            var card = new ProviderCardViewModel(provider, RefreshAfterSignInAsync);

            // Qualquer mudança nos anéis (leitura, erro, sair da conta) pode mudar as colunas.
            card.Windows.CollectionChanged += (_, _) => UpdateRingColumns();
            _allCards.Add(card);
        }
        ShowVisibleProviders();

        _timer.Interval = RefreshInterval;
        _timer.Tick += async (_, _) => await OnTimerTickAsync();

        _initialized = true;
    }

    /// <summary>Pedido para fechar o widget (a janela escuta e fecha).</summary>
    public event EventHandler? CloseRequested;

    public ObservableCollection<ProviderCardViewModel> Providers { get; } = [];

    public bool HasProviders => Providers.Count > 0;

    public bool IsSmall => Size == WidgetSize.Small;

    public bool IsLarge => Size == WidgetSize.Large;

    public double? SavedLeft => _settings.Left;

    public double? SavedTop => _settings.Top;

    public string PinToolTip => IsPinned ? "Desafixar" : "Fixar (sempre visível)";

    public async Task InitializeAsync()
    {
        _timer.Start();
        await RefreshAsync();
    }

    public void Shutdown()
    {
        _timer.Stop();
        _refreshCts?.Cancel();
    }

    /// <summary>Atualização pedida pelo usuário (ou ao abrir): consulta todas as contas, mesmo as pausadas.</summary>
    [RelayCommand]
    private Task RefreshAsync() => RefreshCoreAsync(background: false);

    /// <param name="background">
    /// Atualização automática a cada minuto: silenciosa (sem esmaecer as contas nem "Atualizando...")
    /// e pulando as contas pausadas (sem mudanças recentes ou por excesso de consultas).
    /// </param>
    private async Task RefreshCoreAsync(bool background)
    {
        if (!HasProviders)
        {
            Subtitle = "Nenhuma conta para mostrar";
            return;
        }

        _refreshCts?.Cancel();
        var cts = _refreshCts = new CancellationTokenSource();

        var now = _time.GetUtcNow();
        var cards = Providers.Where(card => !background || card.PausedUntil is null || card.PausedUntil <= now).ToList();

        IsRefreshing = true;
        if (!background)
            Subtitle = "Atualizando...";
        try
        {
            // Cada provedor atualiza o próprio card assim que responde; um lento não segura os outros.
            await Task.WhenAll(cards.Select(card => RefreshCardAsync(card, cts.Token, showLoading: !background)));
        }
        finally
        {
            if (_refreshCts == cts)
            {
                IsRefreshing = false;
                _lastRefresh = _time.GetUtcNow();
                UpdateClock();
            }
        }
    }

    private async Task RefreshCardAsync(ProviderCardViewModel card, CancellationToken cancellationToken, bool showLoading)
    {
        card.IsLoading = showLoading;
        try
        {
            var usage = await card.Provider.GetUsageAsync(cancellationToken);
            var now = _time.GetUtcNow();
            card.PausedUntil = card.CountRead(usage) >= UnchangedReadsBeforePause ? now + IdlePause : null;
            card.Apply(usage, now, _time.LocalTimeZone);
            Notify(_alerts.Check(card.Id, card.Name, usage.Windows), now);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Uma atualização mais nova tomou o lugar desta.
        }
        catch (UsageProviderException ex) when (ex.IsRateLimited)
        {
            // Pausa só esta conta; os últimos limites lidos continuam visíveis.
            card.PausedUntil = _time.GetUtcNow() + RateLimitPause;
            card.ApplyError(ex.Message);
        }
        catch (UsageProviderException ex) when (ex.RequiresSignIn)
        {
            // Saiu da conta: a próxima leitura (talvez de outra conta) volta a ser só a base dos avisos.
            _alerts.Forget(card.Id);
            card.ApplySignInRequired(ex.Message);
        }
        catch (UsageProviderException ex)
        {
            card.ApplyError(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Falha ao ler os limites de {card.Name}.", ex);
            card.ApplyError("Não foi possível ler os limites agora.");
        }
        finally
        {
            card.IsLoading = false;
        }
    }

    /// <summary>
    /// Avisa os limites que passaram de 80% ou 95%. Com os avisos desligados, a leitura continua sendo
    /// registrada (em <see cref="UsageAlertTracker.Check"/>), para não avisar de algo antigo ao religar.
    /// </summary>
    private void Notify(IReadOnlyList<UsageAlert> alerts, DateTimeOffset now)
    {
        if (!_settings.Notifications)
            return;

        foreach (var alert in alerts)
            _notifications.Show(UsageAlertTracker.TitleFor(alert), UsageAlertTracker.MessageFor(alert, now, _time.LocalTimeZone));
    }

    /// <summary>Depois de entrar na conta, relê só aquele card.</summary>
    private async Task RefreshAfterSignInAsync(ProviderCardViewModel card)
    {
        await RefreshCardAsync(card, CancellationToken.None, showLoading: true);
        UpdateClock();
    }

    private async Task OnTimerTickAsync()
    {
        // Se a leitura anterior ainda não terminou (rede lenta), só atualiza os textos e espera a próxima.
        if (IsRefreshing)
        {
            UpdateClock();
            return;
        }

        await RefreshCoreAsync(background: true);
    }

    private void UpdateClock()
    {
        var now = _time.GetUtcNow();
        foreach (var card in Providers)
            card.UpdateClock(now, _time.LocalTimeZone);

        if (!IsRefreshing && HasProviders)
            Subtitle = UsageFormatter.UpdatedText(_lastRefresh, now, _time.LocalTimeZone);
    }

    [RelayCommand]
    private void TogglePin() => IsPinned = !IsPinned;

    partial void OnIsPinnedChanged(bool value)
    {
        if (!_initialized)
            return;

        _settings.Pinned = value;
        Save();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var saved = await _dialogs.ShowSettingsAsync(_settings, [.. _allCards.Select(c => c.Provider)]);

        // A tela mostra o tema como prévia; ao fechar, vale o tema salvo (o novo, se salvou).
        _themes.Apply(_settings.Theme);
        if (!saved)
            return;

        Theme = _settings.Theme;
        Size = _settings.Size;
        ShowVisibleProviders();
        await RefreshAsync();
    }

    /// <summary>Mostra só as contas que não foram escondidas nas configurações, na ordem original.</summary>
    private void ShowVisibleProviders()
    {
        Providers.Clear();
        foreach (var card in _allCards.Where(c => _settings.IsVisible(c.Id)))
            Providers.Add(card);
        OnPropertyChanged(nameof(HasProviders));
        UpdateRingColumns();
    }

    /// <summary>Todas as contas usam as colunas da que tem mais anéis, para ficarem alinhadas.</summary>
    private void UpdateRingColumns()
    {
        var columns = Math.Max(MinRingColumns, Providers.Select(c => c.Windows.Count).DefaultIfEmpty(0).Max());
        foreach (var card in _allCards)
            card.RingColumns = columns;
        RingColumns = columns;
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    public void SaveWindowPosition(double left, double top)
    {
        _settings.Left = left;
        _settings.Top = top;
        Save();
    }

    private void Save()
    {
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write("Falha ao salvar as configurações.", ex);
        }
    }
}
