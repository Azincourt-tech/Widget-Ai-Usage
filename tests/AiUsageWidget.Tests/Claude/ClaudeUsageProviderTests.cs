using System.Net;
using System.Net.Http;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Claude;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests.Claude;

public class ClaudeUsageProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    // Resposta fictícia no formato do endereço de uso.
    private const string Sample = """
        {
          "five_hour": {"utilization": 42.0, "resets_at": "2026-10-19T17:14:00+00:00"},
          "seven_day": {"utilization": 68.5, "resets_at": "2026-10-22T12:00:00.123456+00:00"},
          "seven_day_opus": null,
          "seven_day_sonnet": null,
          "seven_day_oauth_apps": null
        }
        """;

    private const string RefreshResponse = """{"access_token": "token-novo", "refresh_token": "refresh-novo", "expires_in": 28800}""";

    private readonly FakeCredentialStore<ClaudeCredentials> _store = new(new ClaudeCredentials("token-ficticio", "refresh-ficticio", Now.AddHours(5)));

    private ClaudeUsageProvider Create(FakeHttpHandler handler)
    {
        var http = new HttpClient(handler);
        var time = new FakeTimeProvider(Now);
        return new ClaudeUsageProvider(new ClaudeAccount(_store, new ClaudeOAuthClient(http, time), new FakeBrowserLauncher(), time), http);
    }

    [Fact]
    public void ParseUsage_LeSessaoESemana()
    {
        var windows = ClaudeUsageProvider.ParseUsage(Sample);

        Assert.Equal(2, windows.Count);
        Assert.Equal(new UsageWindow(UsageWindowKind.FiveHours, 42, new DateTimeOffset(2026, 10, 19, 17, 14, 0, TimeSpan.Zero)), windows[0]);
        Assert.Equal(UsageWindowKind.Weekly, windows[1].Kind);
        Assert.Equal(68.5, windows[1].UsedPercent);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero), windows[1].ResetsAt!.Value.AddTicks(-1234560));
    }

    [Fact]
    public void ParseUsage_LimitesPorModelo_GanhamAnelProprioComRotulo()
    {
        var windows = ClaudeUsageProvider.ParseUsage("""
            {
              "five_hour": {"utilization": 10, "resets_at": null},
              "seven_day": {"utilization": 20, "resets_at": null},
              "seven_day_opus": {"utilization": 30, "resets_at": "2026-10-22T12:00:00+00:00"},
              "seven_day_sonnet": {"utilization": 40, "resets_at": null}
            }
            """);

        Assert.Equal(4, windows.Count);
        Assert.Equal(new UsageWindow(UsageWindowKind.Weekly, 30, new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero), "Opus semanal", "Opus"), windows[2]);
        Assert.Equal(new UsageWindow(UsageWindowKind.Weekly, 40, null, "Sonnet semanal", "Sonnet"), windows[3]);
    }

    [Fact]
    public void ParseUsage_LimitesPorModeloNulos_NaoAparecem()
    {
        var windows = ClaudeUsageProvider.ParseUsage(Sample);

        Assert.All(windows, w => Assert.Null(w.Label));
    }

    [Fact]
    public void ParseUsage_SemHorarioDeRenovacao_AceitaNulo()
    {
        var windows = ClaudeUsageProvider.ParseUsage("""{"five_hour": {"utilization": 0, "resets_at": null}}""");

        Assert.Null(Assert.Single(windows).ResetsAt);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"five_hour": null, "seven_day": null}""")]
    [InlineData("""{"five_hour": {"resets_at": "2026-10-19T17:14:00Z"}}""")]
    [InlineData("[]")]
    [InlineData("{ quebrado")]
    public void ParseUsage_SemLimites_ViraErroAmigavel(string json) =>
        Assert.Throws<UsageProviderException>(() => ClaudeUsageProvider.ParseUsage(json));

    [Fact]
    public async Task GetUsage_EnviaTokenECabecalhoBeta()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, Sample);

        var usage = await Create(handler).GetUsageAsync(CancellationToken.None);

        Assert.Equal(2, usage.Windows.Count);
        var request = handler.Requests[0].Request;
        Assert.Equal(ClaudeEndpoints.Usage, request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("token-ficticio", request.Headers.Authorization.Parameter);
        Assert.Equal([ClaudeEndpoints.OAuthBetaHeader], request.Headers.GetValues("anthropic-beta"));
    }

    // Perfil fictício no formato do endereço de perfil.
    private const string Profile = """{"account": {"uuid": "x"}, "organization": {"organization_type": "claude_max", "rate_limit_tier": "default_claude_max_20x"}}""";

    [Theory]
    [InlineData("claude_max", "default_claude_max_20x", "Max 20x")]
    [InlineData("claude_max", "default_claude_max_5x", "Max 5x")]
    [InlineData("claude_max", "default_raven", "Max")]
    [InlineData("claude_pro", "default_claude_ai", "Pro")]
    [InlineData("claude_team", "default_raven", "Team")]
    [InlineData("claude_enterprise", null, "Enterprise")]
    [InlineData("algo_novo", null, null)]
    public void ParsePlan_TraduzOTipoDaOrganizacao(string type, string? tier, string? expected)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { organization = new { organization_type = type, rate_limit_tier = tier } });

        Assert.Equal(expected, ClaudeUsageProvider.ParsePlan(json));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"organization": null}""")]
    [InlineData("[]")]
    [InlineData("{ quebrado")]
    public void ParsePlan_SemPlano_DevolveNulo(string json) =>
        Assert.Null(ClaudeUsageProvider.ParsePlan(json));

    [Fact]
    public async Task GetUsage_LeOPlanoDoPerfilUmaVezSo()
    {
        var handler = new FakeHttpHandler(request => FakeHttpHandler.Json(HttpStatusCode.OK,
            request.RequestUri!.ToString() == ClaudeEndpoints.Profile ? Profile : Sample));
        var provider = Create(handler);

        var first = await provider.GetUsageAsync(CancellationToken.None);
        var second = await provider.GetUsageAsync(CancellationToken.None);

        Assert.Equal("Max 20x", first.Plan);
        Assert.Equal("Max 20x", second.Plan);
        var profileRequests = handler.Requests.Where(r => r.Request.RequestUri!.ToString() == ClaudeEndpoints.Profile).ToList();
        var profile = Assert.Single(profileRequests).Request;
        Assert.False(profile.Headers.Contains("anthropic-beta"));
    }

    [Fact]
    public async Task GetUsage_PerfilFalhou_SegueSemEtiqueta()
    {
        var handler = new FakeHttpHandler(request => request.RequestUri!.ToString() == ClaudeEndpoints.Profile
            ? FakeHttpHandler.Json(HttpStatusCode.InternalServerError, "{}")
            : FakeHttpHandler.Json(HttpStatusCode.OK, Sample));

        var usage = await Create(handler).GetUsageAsync(CancellationToken.None);

        Assert.Null(usage.Plan);
        Assert.Equal(2, usage.Windows.Count);
    }

    [Fact]
    public async Task GetUsage_SemLogin_PedeLogin()
    {
        _store.Current = null;
        var handler = new FakeHttpHandler(HttpStatusCode.OK, Sample);

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler).GetUsageAsync(CancellationToken.None));

        Assert.True(ex.RequiresSignIn);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task GetUsage_TokenRecusado_RenovaUmaVezETentaDeNovo()
    {
        var usageCalls = 0;
        var handler = new FakeHttpHandler(request => request.RequestUri!.ToString() == ClaudeEndpoints.Token
            ? FakeHttpHandler.Json(HttpStatusCode.OK, RefreshResponse)
            : FakeHttpHandler.Json(++usageCalls == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, Sample));

        var usage = await Create(handler).GetUsageAsync(CancellationToken.None);

        Assert.Equal(2, usage.Windows.Count);
        Assert.Equal("token-novo", handler.LastRequest!.Headers.Authorization!.Parameter);
        Assert.Equal("refresh-novo", _store.Current!.RefreshToken);
    }

    [Fact]
    public async Task GetUsage_RecusadoMesmoDepoisDeRenovar_PedeLogin()
    {
        var handler = new FakeHttpHandler(request => request.RequestUri!.ToString() == ClaudeEndpoints.Token
            ? FakeHttpHandler.Json(HttpStatusCode.OK, RefreshResponse)
            : FakeHttpHandler.Json(HttpStatusCode.Unauthorized, "{}"));

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler).GetUsageAsync(CancellationToken.None));

        Assert.True(ex.RequiresSignIn);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "Esta conta não tem acesso aos limites de uso.")]
    [InlineData(HttpStatusCode.TooManyRequests, "Muitas consultas seguidas; pausado por alguns minutos.")]
    [InlineData(HttpStatusCode.BadGateway, "A Anthropic respondeu com erro (502).")]
    public async Task GetUsage_ErroHttp_ViraMensagemAmigavel(HttpStatusCode status, string expected)
    {
        var ex = await Assert.ThrowsAsync<UsageProviderException>(() =>
            Create(new FakeHttpHandler(status, "{}")).GetUsageAsync(CancellationToken.None));

        Assert.Equal(expected, ex.Message);
        Assert.False(ex.RequiresSignIn);
    }

    [Fact]
    public async Task GetUsage_SemRede_ViraMensagemAmigavel()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "") { Throw = new HttpRequestException("falhou") };

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler).GetUsageAsync(CancellationToken.None));

        Assert.Equal("Sem conexão com a Anthropic.", ex.Message);
    }
}
