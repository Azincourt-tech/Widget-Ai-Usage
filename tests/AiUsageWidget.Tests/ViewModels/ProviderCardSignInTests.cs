using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Tests.Fakes;
using AiUsageWidget.ViewModels;

namespace AiUsageWidget.Tests.ViewModels;

public class ProviderCardSignInTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    private readonly FakeAccount _account = new();

    private (WidgetViewModel Vm, ProviderCardViewModel Card) Create(FakeAccount account)
    {
        var provider = new FakeUsageProvider(ProviderIds.Claude, () => account.IsSignedIn
            ? new ProviderUsage(null, [new UsageWindow(UsageWindowKind.Weekly, 30, null)])
            : throw new UsageProviderException("Entre com sua conta.", requiresSignIn: true))
        {
            Account = account,
        };
        var vm = new WidgetViewModel(new AppSettings(), [provider], new FakeSettingsStore(), new FakeThemeService(), new FakeDialogService(), new FakeNotificationService(), new FakeUiTimer(), new FakeTimeProvider(Start));
        return (vm, vm.Providers[0]);
    }

    [Fact]
    public async Task SemLogin_MostraConviteSemErroVermelho()
    {
        var (vm, card) = Create(_account);

        await vm.InitializeAsync();

        Assert.True(card.NeedsSignIn);
        Assert.True(card.ShowSignInButton);
        Assert.False(card.HasError);
        Assert.False(card.CanSignOut);
        Assert.Equal("Entre com sua conta.", card.SignInMessage);
        Assert.Equal("Entrar com o claude", card.SignInButtonText);
    }

    [Fact]
    public async Task Entrar_ReleOsLimites()
    {
        var (vm, card) = Create(_account);
        await vm.InitializeAsync();

        await card.SignInCommand.ExecuteAsync(null);

        Assert.False(card.NeedsSignIn);
        Assert.False(card.IsSigningIn);
        Assert.True(card.CanSignOut);
        Assert.Single(card.Windows);
    }

    [Fact]
    public async Task EntrouMasALeituraFalhou_MostraSoOErro()
    {
        var provider = new FakeUsageProvider(ProviderIds.Antigravity, () => _account.IsSignedIn
            ? throw new UsageProviderException("O Google não libera o Antigravity para esta conta.")
            : throw new UsageProviderException("Entre com sua conta.", requiresSignIn: true))
        {
            Account = _account,
        };
        var vm = new WidgetViewModel(new AppSettings(), [provider], new FakeSettingsStore(), new FakeThemeService(), new FakeDialogService(), new FakeNotificationService(), new FakeUiTimer(), new FakeTimeProvider(Start));
        var card = vm.Providers[0];
        await vm.InitializeAsync();

        await card.SignInCommand.ExecuteAsync(null);

        Assert.False(card.NeedsSignIn);
        Assert.False(card.ShowSignInButton);
        Assert.True(card.CanSignOut);
        Assert.Equal("O Google não libera o Antigravity para esta conta.", card.ErrorMessage);
    }

    [Fact]
    public async Task Entrando_MostraCancelar_ECancelarVoltaAoConvite()
    {
        var account = new FakeAccount { OnSignIn = ct => Task.Delay(Timeout.Infinite, ct) };
        var (vm, card) = Create(account);
        await vm.InitializeAsync();

        var signIn = card.SignInCommand.ExecuteAsync(null);
        Assert.True(card.IsSigningIn);
        Assert.False(card.ShowSignInButton);

        card.CancelSignInCommand.Execute(null);
        await signIn;

        Assert.False(card.IsSigningIn);
        Assert.True(card.ShowSignInButton);
        Assert.Equal("Login cancelado.", card.SignInMessage);
    }

    [Fact]
    public async Task LoginNegado_MostraMotivoEMantemOBotao()
    {
        var account = new FakeAccount { OnSignIn = _ => throw new UsageProviderException("O login foi cancelado.", requiresSignIn: true) };
        var (vm, card) = Create(account);
        await vm.InitializeAsync();

        await card.SignInCommand.ExecuteAsync(null);

        Assert.True(card.ShowSignInButton);
        Assert.Equal("O login foi cancelado.", card.SignInMessage);
    }

    [Fact]
    public async Task Sair_EsqueceOLoginELimpaOsAneis()
    {
        _account.IsSignedIn = true;
        var (vm, card) = Create(_account);
        await vm.InitializeAsync();

        card.SignOutCommand.Execute(null);

        Assert.False(_account.IsSignedIn);
        Assert.True(card.NeedsSignIn);
        Assert.Empty(card.Windows);
    }
}
