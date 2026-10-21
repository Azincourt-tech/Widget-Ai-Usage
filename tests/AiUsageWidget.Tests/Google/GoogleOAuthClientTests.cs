using System.Net;
using System.Net.Http;
using System.Web;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Google;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests.Google;

public class GoogleOAuthClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly Uri Redirect = new("http://localhost:54321/oauth-callback");

    // O secret real só existe no build local (Directory.Build.local.props); os testes não dependem dele.
    private static readonly GoogleOAuthApp App = GoogleOAuthApp.Antigravity with { ClientSecret = "secret-ficticio" };

    [Fact]
    public void BuildAuthorizeUrl_PedeRefreshTokenComOClientDoApp()
    {
        var app = GoogleOAuthApp.Antigravity;
        var url = GoogleOAuthClient.BuildAuthorizeUrl(app, "desafio", "estado", Redirect);
        var query = HttpUtility.ParseQueryString(url.Query);

        Assert.StartsWith(GoogleEndpoints.Authorize + "?", url.ToString());
        Assert.Equal(app.ClientId, query["client_id"]);
        Assert.Equal(Redirect.ToString(), query["redirect_uri"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal(GoogleEndpoints.Scopes, query["scope"]);
        Assert.Equal("offline", query["access_type"]);
        Assert.Equal("consent", query["prompt"]);
        Assert.Equal("desafio", query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("estado", query["state"]);
    }

    [Fact]
    public void AppDoAntigravity_UsaORetornoDele() =>
        Assert.Equal("/oauth-callback", GoogleOAuthApp.Antigravity.CallbackPath);

    [Fact]
    public void SemSecret_NaoEstaConfigurado()
    {
        Assert.True(App.IsConfigured);
        Assert.False((App with { ClientSecret = "" }).IsConfigured);
    }

    [Fact]
    public void BuildSecret_QueNaoVeioNoBuild_FicaVazio() =>
        Assert.Equal("", GoogleOAuthApp.BuildSecret("SecretQueNaoExiste"));

    [Fact]
    public void ParseTokenResponse_LeTokensEValidade()
    {
        var login = GoogleOAuthClient.ParseTokenResponse("""{"access_token": "a", "refresh_token": "r", "expires_in": 3599}""", Now, null);

        Assert.Equal(new GoogleCredentials("a", "r", Now.AddSeconds(3599)), login);
    }

    [Fact]
    public void ParseTokenResponse_RenovacaoSemRefreshNovo_MantemOAnterior()
    {
        var previous = new GoogleCredentials("velho", "refresh-antigo", Now);

        Assert.Equal("refresh-antigo", GoogleOAuthClient.ParseTokenResponse("""{"access_token": "novo", "expires_in": 60}""", Now, previous).RefreshToken);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"access_token": ""}""")]
    [InlineData("[]")]
    [InlineData("{ quebrado")]
    public void ParseTokenResponse_SemToken_ViraErro(string json) =>
        Assert.Throws<UsageProviderException>(() => GoogleOAuthClient.ParseTokenResponse(json, Now, null));

    [Fact]
    public async Task ExchangeCode_EnviaFormularioComSecretEVerificador()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, """{"access_token": "a", "refresh_token": "r", "expires_in": 60}""");
        var client = new GoogleOAuthClient(new HttpClient(handler), new FakeTimeProvider(Now), App);

        await client.ExchangeCodeAsync("codigo", "verificador", Redirect, CancellationToken.None);

        var (request, body) = handler.Requests.Single();
        Assert.Equal(GoogleEndpoints.Token, request.RequestUri!.ToString());
        var sent = HttpUtility.ParseQueryString(body!);
        Assert.Equal("authorization_code", sent["grant_type"]);
        Assert.Equal("codigo", sent["code"]);
        Assert.Equal("verificador", sent["code_verifier"]);
        Assert.Equal(App.ClientId, sent["client_id"]);
        Assert.Equal("secret-ficticio", sent["client_secret"]);
        Assert.Equal(Redirect.ToString(), sent["redirect_uri"]);
    }

    [Fact]
    public async Task Refresh_EnviaRefreshToken()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, """{"access_token": "a2", "expires_in": 60}""");
        var client = new GoogleOAuthClient(new HttpClient(handler), new FakeTimeProvider(Now), App);

        var login = await client.RefreshAsync(new GoogleCredentials("a", "r1", Now), CancellationToken.None);

        Assert.Equal("r1", login.RefreshToken);
        var sent = HttpUtility.ParseQueryString(handler.Requests.Single().Body!);
        Assert.Equal("refresh_token", sent["grant_type"]);
        Assert.Equal("r1", sent["refresh_token"]);
        Assert.Equal(App.ClientId, sent["client_id"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task Refresh_Recusado_DizSePrecisaDeNovoLogin(HttpStatusCode status, bool requiresSignIn)
    {
        var client = new GoogleOAuthClient(new HttpClient(new FakeHttpHandler(status, "{}")), new FakeTimeProvider(Now), App);

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() =>
            client.RefreshAsync(new GoogleCredentials("a", "r", Now), CancellationToken.None));

        Assert.Equal(requiresSignIn, ex.RequiresSignIn);
    }
}
