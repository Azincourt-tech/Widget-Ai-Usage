using System.Collections.ObjectModel;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiUsageWidget.ViewModels;

/// <summary>
/// Card de uma conta: nome, plano, um anel por janela de limite e, quando o provedor tem login
/// próprio, os botões de entrar e sair.
/// </summary>
public sealed partial class ProviderCardViewModel : ObservableObject
{
    private readonly Func<ProviderCardViewModel, Task> _refresh;
    private CancellationTokenSource? _signInCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlan))]
    private string? _plan;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSignInButton))]
    private bool _needsSignIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSignInButton))]
    private bool _isSigningIn;

    [ObservableProperty]
    private string? _signInMessage;

    [ObservableProperty]
    private bool _canSignOut;

    /// <summary>
    /// Colunas da grade de anéis. É a mesma em todas as contas (definida pelo <see cref="WidgetViewModel"/>),
    /// para os anéis ficarem alinhados entre as linhas.
    /// </summary>
    [ObservableProperty]
    private int _ringColumns = WidgetViewModel.MinRingColumns;

    /// <param name="refresh">Relê os limites deste card (chamado depois de entrar na conta).</param>
    public ProviderCardViewModel(IUsageProvider provider, Func<ProviderCardViewModel, Task> refresh)
    {
        Provider = provider;
        _refresh = refresh;
        AccentColor = ProviderIds.AccentFor(provider.Id);
        Windows.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PeakPercent));
            OnPropertyChanged(nameof(PeakText));
        };
    }

    /// <summary>O maior percentual usado entre os limites da conta, ou nulo se ainda não há leitura.</summary>
    public double? PeakPercent => Windows.Count > 0 ? Windows.Max(w => w.Percent) : null;

    /// <summary>Texto da aba da bandeja e da dica do ícone: "42%", ou "—" sem leitura.</summary>
    public string PeakText => PeakPercent is double peak ? UsageFormatter.PercentText(peak) : "—";

    public IUsageProvider Provider { get; }

    public string Id => Provider.Id;

    public string Name => Provider.DisplayName;

    /// <summary>Letra do selo colorido ao lado do nome.</summary>
    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

    /// <summary>Cor do provedor (#RRGGBB).</summary>
    public string AccentColor { get; }

    public bool HasPlan => !string.IsNullOrWhiteSpace(Plan);

    public bool HasError => ErrorMessage is not null;

    public bool ShowSignInButton => NeedsSignIn && !IsSigningIn;

    /// <summary>Nome do botão "Entrar" para acessibilidade (na tela ele diz só "Entrar").</summary>
    public string SignInButtonText => $"Entrar com o {Name}";

    public string SignOutMenuText => $"Sair da conta do {Name}";

    public ObservableCollection<UsageWindowViewModel> Windows { get; } = [];

    /// <summary>
    /// Até quando a atualização automática pula esta conta (sem mudanças recentes ou depois de
    /// "muitas consultas"). "Atualizar agora" ignora a pausa.
    /// </summary>
    public DateTimeOffset? PausedUntil { get; set; }

    // Última leitura e quantas leituras seguidas vieram iguais a ela.
    private ProviderUsage? _lastUsage;
    private int _unchangedReads;

    /// <summary>
    /// Registra uma leitura e devolve quantas leituras seguidas vieram sem nenhuma mudança. Ao chegar
    /// a <see cref="WidgetViewModel.UnchangedReadsBeforePause"/>, a contagem recomeça (depois da pausa).
    /// </summary>
    public int CountRead(ProviderUsage usage)
    {
        var unchanged = _lastUsage is { } last && last.Plan == usage.Plan && last.Windows.SequenceEqual(usage.Windows);
        _lastUsage = usage;
        _unchangedReads = unchanged ? _unchangedReads + 1 : 0;

        var count = _unchangedReads;
        if (_unchangedReads >= WidgetViewModel.UnchangedReadsBeforePause)
            _unchangedReads = 0;
        return count;
    }

    public void Apply(ProviderUsage usage, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        Plan = usage.Plan;
        ErrorMessage = null;
        NeedsSignIn = false;
        SignInMessage = null;
        CanSignOut = Provider.Account is not null;

        Windows.Clear();
        foreach (var window in usage.Windows)
        {
            var item = new UsageWindowViewModel(window, AccentColor);
            item.UpdateClock(now, timeZone);
            Windows.Add(item);
        }
    }

    /// <summary>
    /// Mostra o erro no card. Os últimos limites lidos continuam visíveis. Se a conta está logada, o
    /// problema não é o login: o convite para entrar some e o botão de sair aparece.
    /// </summary>
    public void ApplyError(string message)
    {
        ErrorMessage = message;
        if (Provider.Account is { IsSignedIn: true })
            ShowSignedIn();
    }

    private void ShowSignedIn()
    {
        NeedsSignIn = false;
        SignInMessage = null;
        CanSignOut = true;
    }

    /// <summary>Sem login (ou login vencido): troca os anéis pelo convite para entrar.</summary>
    public void ApplySignInRequired(string message)
    {
        Windows.Clear();
        Plan = null;
        ErrorMessage = null;
        SignInMessage = message;
        NeedsSignIn = true;
        CanSignOut = false;
    }

    public void UpdateClock(DateTimeOffset now, TimeZoneInfo timeZone)
    {
        foreach (var window in Windows)
            window.UpdateClock(now, timeZone);
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (Provider.Account is not { } account || IsSigningIn)
            return;

        var cts = _signInCts = new CancellationTokenSource();
        IsSigningIn = true;
        ErrorMessage = null;
        SignInMessage = "Conclua o login no navegador...";
        try
        {
            await account.SignInAsync(cts.Token);

            // Entrou: o convite some já, mesmo que a leitura dos limites falhe em seguida.
            ShowSignedIn();
            await _refresh(this);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            SignInMessage = "Login cancelado.";
        }
        catch (UsageProviderException ex) when (ex.RequiresSignIn)
        {
            SignInMessage = ex.Message;
        }
        catch (UsageProviderException ex)
        {
            SignInMessage = null;
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Falha no login do {Name}.", ex);
            SignInMessage = null;
            ErrorMessage = "Não foi possível entrar agora.";
        }
        finally
        {
            IsSigningIn = false;
            _signInCts = null;
            cts.Dispose();
        }
    }

    [RelayCommand]
    private void CancelSignIn() => _signInCts?.Cancel();

    [RelayCommand]
    private void SignOut()
    {
        if (Provider.Account is not { } account)
            return;

        try
        {
            account.SignOut();
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Falha ao sair da conta do {Name}.", ex);
        }

        ApplySignInRequired($"Você saiu da conta. Entre de novo para ver os limites do {Name}.");
    }
}
