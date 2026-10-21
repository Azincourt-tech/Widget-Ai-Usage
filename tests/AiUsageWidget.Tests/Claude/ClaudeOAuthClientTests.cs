using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using AiUsageWidget.Constants;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Claude;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests.Claude;

public class ClaudeOAuthClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly Uri Redirect = new("http://localhost:54321/callback");

    [Fact]
    public void BuildAuthorizeUrl_TemTodosOsParametros()
    {
        var url = ClaudeOAuthClient.BuildAuthorizeUrl("desafio", "estado", Redirect);
        var query = HttpUtility.ParseQueryString(url.Query);

        Assert.StartsWith(ClaudeEndpoints.Authorize + "?", url.ToString());
        Assert.Equal("true", query["code"]);
        Assert.Equal(ClaudeEndpoints.ClientId, query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal(Redirect.ToString(), query["redirect_uri"]);
        Assert.Equal(ClaudeEndpoints.Scopes, query["scope"]);
        Assert.Equal("desafio", query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("estado", query["state"]);
    }

    [Fact]
    public void ParseTokenResponse_CalculaValidade()
    {
        var login = ClaudeOAuthClient.ParseTokenResponse("""{"access_token": "a", "refresh_token": "r", "expires_in": 3600}""", Now, null);

        Assert.Equal("a", login.AccessToken);
        Assert.Equal("r", login.RefreshToken);
        Assert.Equal(Now.AddHours(1), login.ExpiresAt);
    }

    [Fact]
    public void ParseTokenResponse_SemRefreshNovo_MantemOAnterior() =>
        Assert.Equal("antigo", ClaudeOAuthClient.ParseTokenResponse("""{"access_token": "a"}""", Now, "antigo").RefreshToken);

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"access_token": ""}""")]
    [InlineData("[]")]
    [InlineData("{ quebrado")]
    public void ParseTokenResponse_SemToken_ViraErro(string json) =>
        Assert.Throws<UsageProviderException>(() => ClaudeOAuthClient.ParseTokenResponse(json, Now, null));

    [Fact]
    public async Task ExchangeCode_EnviaCodigoVerificadorERetorno()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, """{"access_token": "a", "refresh_token": "r", "expires_in": 60}""");
        var client = new ClaudeOAuthClient(new HttpClient(handler), new FakeTimeProvider(Now));

        var login = await client.ExchangeCodeAsync("codigo", "verificador", "estado", Redirect, CancellationToken.None);

        Assert.Equal("a", login.AccessToken);
        var (request, body) = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(ClaudeEndpoints.Token, request.RequestUri!.ToString());
        var sent = JsonSerializer.Deserialize<Dictionary<string, string>>(body!)!;
        Assert.Equal("authorization_code", sent["grant_type"]);
        Assert.Equal("codigo", sent["code"]);
        Assert.Equal("verificador", sent["code_verifier"]);
        Assert.Equal("estado", sent["state"]);
        Assert.Equal(Redirect.ToString(), sent["redirect_uri"]);
        Assert.Equal(ClaudeEndpoints.ClientId, sent["client_id"]);
    }

    [Fact]
    public async Task Refresh_EnviaRefreshToken()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, """{"access_token": "a2", "expires_in": 60}""");
        var client = new ClaudeOAuthClient(new HttpClient(handler), new FakeTimeProvider(Now));

        var login = await client.RefreshAsync("r1", CancellationToken.None);

        Assert.Equal("r1", login.RefreshToken);
        var sent = JsonSerializer.Deserialize<Dictionary<string, string>>(handler.Requests.Single().Body!)!;
        Assert.Equal("refresh_token", sent["grant_type"]);
        Assert.Equal("r1", sent["refresh_token"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task Refresh_Recusado_DizSePrecisaDeNovoLogin(HttpStatusCode status, bool requiresSignIn)
    {
        var client = new ClaudeOAuthClient(new HttpClient(new FakeHttpHandler(status, "{}")), new FakeTimeProvider(Now));

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() => client.RefreshAsync("r1", CancellationToken.None));

        Assert.Equal(requiresSignIn, ex.RequiresSignIn);
    }
}
