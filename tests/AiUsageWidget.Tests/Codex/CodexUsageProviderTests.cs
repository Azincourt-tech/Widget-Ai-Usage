using System.Net;
using System.Net.Http;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Codex;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests.Codex;

public class CodexUsageProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    // Resposta fictícia no formato do endereço de uso do Codex.
    private const string Sample = """
        {
          "plan_type": "plus",
          "rate_limit": {
            "allowed": true,
            "limit_reached": false,
            "primary_window": {"used_percent": 42, "limit_window_seconds": 18000, "reset_after_seconds": 3600, "reset_at": 1792000000},
            "secondary_window": {"used_percent": 68.5, "limit_window_seconds": 604800, "reset_after_seconds": 90000, "reset_at": 1792500000}
          },
          "credits": null
        }
        """;

    private const string RefreshResponse = """{"access_token": "token-novo", "refresh_token": "refresh-novo", "expires_in": 3600}""";

    private readonly FakeCredentialStore<CodexCredentials> _store =
        new(new CodexCredentials("token-ficticio", "refresh-ficticio", Now.AddHours(1), "conta-ficticia", "pro"));

    private CodexUsageProvider Create(FakeHttpHandler handler)
    {
        var http = new HttpClient(handler);
        var time = new FakeTimeProvider(Now);
        return new CodexUsageProvider(new CodexAccount(_store, new CodexOAuthClient(http, time), new FakeBrowserLauncher(), time), http, time);
    }

    [Fact]
    public void ParseUsage_LeSessaoESemanaEPlano()
    {
        var usage = CodexUsageProvider.ParseUsage(Sample, Now);

        Assert.Equal("Plus", usage.Plan);
        Assert.Equal(
            [
                new UsageWindow(UsageWindowKind.FiveHours, 42, DateTimeOffset.FromUnixTimeSeconds(1792000000)),
                new UsageWindow(UsageWindowKind.Weekly, 68.5, DateTimeOffset.FromUnixTimeSeconds(1792500000)),
            ],
            usage.Windows);
    }

    [Fact]
    public void ParseUsage_SoSemanal_ComoNoPlanoGratis()
    {
        var usage = CodexUsageProvider.ParseUsage("""
            {"plan_type": "free", "rate_limit": {"primary_window": {"used_percent": 96, "limit_window_seconds": 604800, "reset_at": 1792000000}, "secondary_window": null}}
            """, Now);

        Assert.Equal("Free", usage.Plan);
        Assert.Equal(UsageWindowKind.Weekly, Assert.Single(usage.Windows).Kind);
    }

    [Fact]
    public void ParseUsage_SemResetAt_UsaResetAfterSeconds()
    {
        var usage = CodexUsageProvider.ParseUsage("""
            {"rate_limit": {"primary_window": {"used_percent": 10, "limit_window_seconds": 18000, "reset_after_seconds": 600}}}
            """, Now);

        Assert.Equal(Now.AddMinutes(10), Assert.Single(usage.Windows).ResetsAt);
        Assert.Null(usage.Plan);
    }

    [Fact]
    public void ParseUsage_OrdenaSessaoAntesDaSemana()
    {
        var usage = CodexUsageProvider.ParseUsage("""
            {"rate_limit": {
              "primary_window": {"used_percent": 1, "limit_window_seconds": 604800},
              "secondary_window": {"used_percent": 2, "limit_window_seconds": 18000}}}
            """, Now);

        Assert.Equal([UsageWindowKind.FiveHours, UsageWindowKind.Weekly], usage.Windows.Select(w => w.Kind));
    }

    [Theory]
    [InlineData(18000, UsageWindowKind.FiveHours)]
    [InlineData(21600, UsageWindowKind.FiveHours)]
    [InlineData(86400, UsageWindowKind.Daily)]
    [InlineData(604800, UsageWindowKind.Weekly)]
    public void KindFor_PelaDuracao(long seconds, UsageWindowKind expected) =>
        Assert.Equal(expected, CodexUsageProvider.KindFor(seconds));

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"rate_limit": null}""")]
    [InlineData("""{"rate_limit": {"primary_window": null, "secondary_window": null}}""")]
    [InlineData("""{"rate_limit": {"primary_window": {"used_percent": 10}}}""")]
    [InlineData("[]")]
    [InlineData("{ quebrado")]
    public void ParseUsage_SemLimites_ViraErroAmigavel(string json) =>
        Assert.Throws<UsageProviderException>(() => CodexUsageProvider.ParseUsage(json, Now));

    [Fact]
    public async Task GetUsage_EnviaTokenEConta()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, Sample);

        var usage = await Create(handler).GetUsageAsync(CancellationToken.None);

        Assert.Equal(2, usage.Windows.Count);
        var request = handler.LastRequest!;
        Assert.Equal(CodexEndpoints.Usage, request.RequestUri!.ToString());
        Assert.Equal("token-ficticio", request.Headers.Authorization!.Parameter);
        Assert.Equal(["conta-ficticia"], request.Headers.GetValues(CodexEndpoints.AccountIdHeader));
    }

    [Fact]
    public async Task GetUsage_RespostaSemPlano_UsaOPlanoDoLogin()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK,
            """{"rate_limit": {"primary_window": {"used_percent": 5, "limit_window_seconds": 18000}}}""");

        var usage = await Create(handler).GetUsageAsync(CancellationToken.None);

        Assert.Equal("Pro", usage.Plan);
    }

    [Fact]
    public async Task GetUsage_SemLogin_PedeLogin()
    {
        _store.Current = null;
        var handler = new FakeHttpHandler(HttpStatusCode.OK, Sample);

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler).GetUsageAsync(CancellationToken.None));

        Assert.True(ex.RequiresSignIn);
        Assert.Equal("Entre com sua conta do Codex para ver os limites.", ex.Message);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task GetUsage_TokenRecusado_RenovaUmaVezETentaDeNovo()
    {
        var usageCalls = 0;
        var handler = new FakeHttpHandler(request => request.RequestUri!.ToString() == CodexEndpoints.Token
            ? FakeHttpHandler.Json(HttpStatusCode.OK, RefreshResponse)
            : FakeHttpHandler.Json(++usageCalls == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, Sample));

        await Create(handler).GetUsageAsync(CancellationToken.None);

        Assert.Equal("token-novo", handler.LastRequest!.Headers.Authorization!.Parameter);
        Assert.Equal("refresh-novo", _store.Current!.RefreshToken);
        Assert.Equal("conta-ficticia", _store.Current.AccountId);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "Esta conta não tem acesso ao Codex.")]
    [InlineData(HttpStatusCode.TooManyRequests, "Muitas consultas seguidas; pausado por alguns minutos.")]
    [InlineData(HttpStatusCode.BadGateway, "A OpenAI respondeu com erro (502).")]
    public async Task GetUsage_ErroHttp_ViraMensagemAmigavel(HttpStatusCode status, string expected)
    {
        var ex = await Assert.ThrowsAsync<UsageProviderException>(() =>
            Create(new FakeHttpHandler(status, "{}")).GetUsageAsync(CancellationToken.None));

        Assert.Equal(expected, ex.Message);
    }

    [Fact]
    public async Task GetUsage_SemRede_ViraMensagemAmigavel()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "") { Throw = new HttpRequestException("falhou") };

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler).GetUsageAsync(CancellationToken.None));

        Assert.Equal("Sem conexão com a OpenAI.", ex.Message);
    }
}
