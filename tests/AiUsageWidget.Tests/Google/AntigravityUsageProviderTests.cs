using System.Net;
using System.Net.Http;
using System.Text.Json;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Google;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests.Google;

public class AntigravityUsageProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    private const string Load = """{"cloudaicompanionProject": "projeto-ficticio", "currentTier": {"id": "free-tier", "name": "Antigravity"}, "paidTier": {"id": "g1-plus-tier", "name": "Google AI Plus"}}""";
    private const string Summary = """{"groups": [{"displayName": "Gemini Models", "buckets": [{"remainingFraction": 0.25, "window": "weekly"}]}]}""";
    private const string RefreshResponse = """{"access_token": "token-novo", "expires_in": 3600}""";

    private readonly FakeCredentialStore<GoogleCredentials> _store = new(new GoogleCredentials("token-ficticio", "refresh-ficticio", Now.AddHours(1)));

    private static string Method(HttpRequestMessage request) => request.RequestUri!.ToString().Split(':').Last();

    private AntigravityUsageProvider Create(FakeHttpHandler handler, string clientSecret = "secret-ficticio")
    {
        var http = new HttpClient(handler);
        var time = new FakeTimeProvider(Now);
        var app = GoogleOAuthApp.Antigravity with { ClientSecret = clientSecret };
        var account = new GoogleAccount("Antigravity", _store, new GoogleOAuthClient(http, time, app), new FakeBrowserLauncher(), time);
        return new AntigravityUsageProvider(account, new CodeAssistClient(http), time);
    }

    private FakeHttpHandler Working() =>
        new(r => FakeHttpHandler.Json(HttpStatusCode.OK, Method(r) == "loadCodeAssist" ? Load : Summary));

    [Fact]
    public async Task LeProjetoUmaVezEConsultaOResumoDeCota()
    {
        var handler = Working();
        var provider = Create(handler);

        var first = await provider.GetUsageAsync(CancellationToken.None);
        await provider.GetUsageAsync(CancellationToken.None);

        Assert.Equal(ProviderIds.Antigravity, provider.Id);
        Assert.Equal("Antigravity", provider.DisplayName);
        Assert.Equal("Plus", first.Plan);
        Assert.Equal("Gemini", Assert.Single(first.Windows).Label);
        Assert.Equal(["loadCodeAssist", "retrieveUserQuotaSummary", "retrieveUserQuotaSummary"], handler.Requests.Select(r => Method(r.Request)));

        var quota = handler.Requests[1];
        Assert.Equal($"{GoogleEndpoints.CodeAssist}:retrieveUserQuotaSummary", quota.Request.RequestUri!.ToString());
        Assert.Equal("token-ficticio", quota.Request.Headers.Authorization!.Parameter);
        Assert.Equal("projeto-ficticio", JsonDocument.Parse(quota.Body!).RootElement.GetProperty("project").GetString());
    }

    [Fact]
    public async Task MandaOUserAgentDoAntigravity()
    {
        var handler = Working();

        await Create(handler).GetUsageAsync(CancellationToken.None);

        // É pelo User-Agent que o Google aceita o cliente (sem ele, contas gratuitas ficam sem projeto).
        Assert.All(handler.Requests, r => Assert.Equal(GoogleEndpoints.AntigravityUserAgent, r.Request.Headers.UserAgent.ToString()));
    }

    [Fact]
    public async Task ClienteRecusado_ExplicaQueOGoogleNaoLibera()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK,
            """{"allowedTiers": [{"id": "standard-tier", "isDefault": true}], "ineligibleTiers": [{"tierId": "free-tier", "reasonCode": "UNSUPPORTED_CLIENT"}]}""");

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler).GetUsageAsync(CancellationToken.None));

        Assert.Equal(AntigravityUsageProvider.UnsupportedClientMessage, ex.Message);
        Assert.False(ex.RequiresSignIn);
    }

    [Fact]
    public async Task SemProjetoNemMotivo_MensagemGenerica()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, """{"currentTier": {"id": "free-tier"}}""");

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler).GetUsageAsync(CancellationToken.None));

        Assert.Equal(AntigravityUsageProvider.NoProjectMessage, ex.Message);
    }

    [Fact]
    public async Task BuildSemSecret_AvisaQueNaoEstaConfiguradoSemConsultarNemPedirLogin()
    {
        _store.Current = null;
        var handler = Working();

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler, clientSecret: "").GetUsageAsync(CancellationToken.None));

        Assert.Equal(AntigravityUsageProvider.NotConfiguredMessage, ex.Message);
        Assert.False(ex.RequiresSignIn);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task SemLogin_PedeLogin()
    {
        _store.Current = null;
        var handler = Working();

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler).GetUsageAsync(CancellationToken.None));

        Assert.True(ex.RequiresSignIn);
        Assert.Equal("Entre com sua conta do Antigravity para ver os limites.", ex.Message);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task TokenRecusado_RenovaUmaVezETentaDeNovo()
    {
        var rejected = false;
        var handler = new FakeHttpHandler(r =>
        {
            if (r.RequestUri!.ToString() == GoogleEndpoints.Token)
                return FakeHttpHandler.Json(HttpStatusCode.OK, RefreshResponse);
            if (!rejected)
            {
                rejected = true;
                return FakeHttpHandler.Json(HttpStatusCode.Unauthorized, "{}");
            }

            return FakeHttpHandler.Json(HttpStatusCode.OK, Method(r) == "loadCodeAssist" ? Load : Summary);
        });

        var usage = await Create(handler).GetUsageAsync(CancellationToken.None);

        Assert.Single(usage.Windows);
        Assert.Equal("token-novo", _store.Current!.AccessToken);
        Assert.Equal("refresh-ficticio", _store.Current.RefreshToken);
    }

    [Fact]
    public async Task RecusadoMesmoDepoisDeRenovar_PedeLogin()
    {
        var handler = new FakeHttpHandler(r => r.RequestUri!.ToString() == GoogleEndpoints.Token
            ? FakeHttpHandler.Json(HttpStatusCode.OK, RefreshResponse)
            : FakeHttpHandler.Json(HttpStatusCode.Unauthorized, "{}"));

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler).GetUsageAsync(CancellationToken.None));

        Assert.True(ex.RequiresSignIn);
        Assert.Equal("Sua sessão do Antigravity expirou. Entre de novo.", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "Esta conta não tem acesso aos limites de uso.", false)]
    [InlineData(HttpStatusCode.TooManyRequests, "Muitas consultas seguidas; pausado por alguns minutos.", true)]
    [InlineData(HttpStatusCode.BadGateway, "O Google respondeu com erro (502).", false)]
    public async Task ErroHttp_ViraMensagemAmigavel(HttpStatusCode status, string expected, bool rateLimited)
    {
        var ex = await Assert.ThrowsAsync<UsageProviderException>(() =>
            Create(new FakeHttpHandler(status, "{}")).GetUsageAsync(CancellationToken.None));

        Assert.Equal(expected, ex.Message);
        Assert.Equal(rateLimited, ex.IsRateLimited);
    }

    [Fact]
    public async Task SemRede_ViraMensagemAmigavel()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "") { Throw = new HttpRequestException("falhou") };

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => Create(handler).GetUsageAsync(CancellationToken.None));

        Assert.Equal("Sem conexão com o Google.", ex.Message);
    }
}
