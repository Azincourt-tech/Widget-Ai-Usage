using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Claude;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests.Claude;

public class ClaudeAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    private const string TokenResponse = """{"access_token": "token-novo", "refresh_token": "refresh-novo", "expires_in": 28800}""";

    private readonly FakeCredentialStore<ClaudeCredentials> _store = new();
    private readonly FakeTimeProvider _time = new(Now);

    private ClaudeAccount Create(FakeHttpHandler handler, FakeBrowserLauncher? browser = null) =>
        new(_store, new ClaudeOAuthClient(new HttpClient(handler), _time), browser ?? new FakeBrowserLauncher(), _time);

    [Fact]
    public async Task SemLogin_PedeLogin()
    {
        var ex = await Assert.ThrowsAsync<UsageProviderException>(() =>
            Create(new FakeHttpHandler(HttpStatusCode.OK, TokenResponse)).GetValidCredentialsAsync(false, CancellationToken.None));

        Assert.True(ex.RequiresSignIn);
        Assert.Equal("Entre com sua conta do Claude para ver os limites.", ex.Message);
    }

    [Fact]
    public async Task TokenValido_NaoRenova()
    {
        _store.Current = new ClaudeCredentials("a", "r", Now.AddHours(1));
        var handler = new FakeHttpHandler(HttpStatusCode.OK, TokenResponse);

        var login = await Create(handler).GetValidCredentialsAsync(false, CancellationToken.None);

        Assert.Equal("a", login.AccessToken);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task TokenQuaseVencido_RenovaESalvaORefreshNovo()
    {
        _store.Current = new ClaudeCredentials("a", "r", Now.AddMinutes(1));

        var login = await Create(new FakeHttpHandler(HttpStatusCode.OK, TokenResponse)).GetValidCredentialsAsync(false, CancellationToken.None);

        Assert.Equal("token-novo", login.AccessToken);
        Assert.Equal(new ClaudeCredentials("token-novo", "refresh-novo", Now.AddHours(8)), _store.Current);
    }

    [Fact]
    public async Task RefreshRecusado_EsqueceOLoginEPedeDeNovo()
    {
        _store.Current = new ClaudeCredentials("a", "r", Now.AddMinutes(-5));

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() =>
            Create(new FakeHttpHandler(HttpStatusCode.BadRequest, "{}")).GetValidCredentialsAsync(false, CancellationToken.None));

        Assert.True(ex.RequiresSignIn);
        Assert.Null(_store.Current);
    }

    [Fact]
    public async Task FalhaTemporariaNaRenovacao_MantemOLogin()
    {
        _store.Current = new ClaudeCredentials("a", "r", Now.AddMinutes(-5));

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() =>
            Create(new FakeHttpHandler(HttpStatusCode.ServiceUnavailable, "{}")).GetValidCredentialsAsync(false, CancellationToken.None));

        Assert.False(ex.RequiresSignIn);
        Assert.NotNull(_store.Current);
    }

    [Fact]
    public async Task DuasConsultasAoMesmoTempo_RenovamUmaVezSo()
    {
        _store.Current = new ClaudeCredentials("a", "r", Now.AddMinutes(-5));
        var release = new TaskCompletionSource();
        var handler = new FakeHttpHandler(_ =>
        {
            release.Task.Wait();
            return FakeHttpHandler.Json(HttpStatusCode.OK, TokenResponse);
        });
        var account = Create(handler);

        var first = Task.Run(() => account.GetValidCredentialsAsync(false, CancellationToken.None));
        var second = Task.Run(() => account.GetValidCredentialsAsync(false, CancellationToken.None));
        await Task.Delay(100);
        release.SetResult();

        Assert.Equal("token-novo", (await first).AccessToken);
        Assert.Equal("token-novo", (await second).AccessToken);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SignIn_AbreNavegadorTrocaCodigoESalva()
    {
        // O "navegador" volta para o endereço de retorno com o código, como o claude.ai faria.
        var browser = new FakeBrowserLauncher(url =>
        {
            var query = HttpUtility.ParseQueryString(url.Query);
            _ = Task.Run(async () =>
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
                await http.GetAsync($"{query["redirect_uri"]}?code=codigo-ficticio&state={query["state"]}");
            });
        });
        var handler = new FakeHttpHandler(HttpStatusCode.OK, TokenResponse);
        var account = Create(handler, browser);

        await account.SignInAsync(CancellationToken.None);

        Assert.StartsWith(ClaudeEndpoints.Authorize, browser.Opened!.ToString());
        Assert.Equal("token-novo", _store.Current?.AccessToken);
        Assert.True(account.IsSignedIn);
        var sent = JsonSerializer.Deserialize<Dictionary<string, string>>(handler.Requests.Single().Body!)!;
        Assert.Equal("codigo-ficticio", sent["code"]);
    }

    [Fact]
    public async Task SignIn_Cancelado_NaoSalva()
    {
        using var cts = new CancellationTokenSource();
        var account = Create(new FakeHttpHandler(HttpStatusCode.OK, TokenResponse), new FakeBrowserLauncher(_ => cts.Cancel()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => account.SignInAsync(cts.Token));

        Assert.Null(_store.Current);
    }

    [Fact]
    public void SignOut_EsqueceOLogin()
    {
        _store.Current = new ClaudeCredentials("a", "r", null);
        var account = Create(new FakeHttpHandler(HttpStatusCode.OK, TokenResponse));

        account.SignOut();

        Assert.False(account.IsSignedIn);
    }
}
