using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Codex;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests.Codex;

public class CodexOAuthClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly Uri Redirect = new("http://localhost:1455/auth/callback");

    /// <summary>id_token fictício com a conta e o plano, no formato da OpenAI.</summary>
    private static string IdToken(string account = "conta-ficticia", string plan = "plus") =>
        TestJwt.Create(new Dictionary<string, object>
        {
            [CodexEndpoints.AuthClaim] = new { chatgpt_account_id = account, chatgpt_plan_type = plan },
        });

    private static string TokenResponse(string? idToken = null, string? refresh = "refresh-ficticio", int? expiresIn = 3600) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["access_token"] = "token-ficticio",
            ["refresh_token"] = refresh,
            ["id_token"] = idToken ?? IdToken(),
            ["expires_in"] = expiresIn,
        });

    [Fact]
    public void BuildAuthorizeUrl_TemOsParametrosDoCodex()
    {
        var url = CodexOAuthClient.BuildAuthorizeUrl("desafio", "estado", Redirect);
        var query = HttpUtility.ParseQueryString(url.Query);

        Assert.StartsWith(CodexEndpoints.Authorize + "?", url.ToString());
        Assert.Equal("code", query["response_type"]);
        Assert.Equal(CodexEndpoints.ClientId, query["client_id"]);
        Assert.Equal(Redirect.ToString(), query["redirect_uri"]);
        Assert.Equal(CodexEndpoints.Scopes, query["scope"]);
        Assert.Equal("desafio", query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("true", query["id_token_add_organizations"]);
        Assert.Equal("true", query["codex_cli_simplified_flow"]);
        Assert.Equal("estado", query["state"]);
    }

    [Fact]
    public void ParseTokenResponse_LeContaPlanoEValidade()
    {
        var login = CodexOAuthClient.ParseTokenResponse(TokenResponse(), Now, previous: null);

        Assert.Equal(new CodexCredentials("token-ficticio", "refresh-ficticio", Now.AddHours(1), "conta-ficticia", "plus"), login);
    }

    [Fact]
    public void ParseTokenResponse_SemExpiresIn_UsaOExpDoToken()
    {
        var access = TestJwt.Create(new { exp = 1792000000 });
        var json = JsonSerializer.Serialize(new { access_token = access, id_token = IdToken() });

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792000000), CodexOAuthClient.ParseTokenResponse(json, Now, null).ExpiresAt);
    }

    [Fact]
    public void ParseTokenResponse_RenovacaoSemIdToken_MantemContaEPlano()
    {
        var previous = new CodexCredentials("antigo", "refresh-antigo", Now, "conta-ficticia", "pro");
        var json = JsonSerializer.Serialize(new { access_token = "novo", expires_in = 60 });

        var login = CodexOAuthClient.ParseTokenResponse(json, Now, previous);

        Assert.Equal(new CodexCredentials("novo", "refresh-antigo", Now.AddMinutes(1), "conta-ficticia", "pro"), login);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"access_token": ""}""")]
    [InlineData("[]")]
    [InlineData("{ quebrado")]
    public void ParseTokenResponse_SemToken_ViraErro(string json) =>
        Assert.Throws<UsageProviderException>(() => CodexOAuthClient.ParseTokenResponse(json, Now, null));

    [Fact]
    public async Task ExchangeCode_EnviaFormularioComVerificador()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, TokenResponse());
        var client = new CodexOAuthClient(new HttpClient(handler), new FakeTimeProvider(Now));

        var login = await client.ExchangeCodeAsync("codigo", "verificador", Redirect, CancellationToken.None);

        Assert.Equal("conta-ficticia", login.AccountId);
        var (request, body) = handler.Requests.Single();
        Assert.Equal(CodexEndpoints.Token, request.RequestUri!.ToString());
        Assert.Equal("application/x-www-form-urlencoded", request.Content!.Headers.ContentType!.MediaType);
        var sent = HttpUtility.ParseQueryString(body!);
        Assert.Equal("authorization_code", sent["grant_type"]);
        Assert.Equal("codigo", sent["code"]);
        Assert.Equal("verificador", sent["code_verifier"]);
        Assert.Equal(Redirect.ToString(), sent["redirect_uri"]);
        Assert.Equal(CodexEndpoints.ClientId, sent["client_id"]);
    }

    [Fact]
    public async Task Refresh_EnviaJsonComRefreshToken()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, TokenResponse(refresh: "refresh-novo"));
        var client = new CodexOAuthClient(new HttpClient(handler), new FakeTimeProvider(Now));

        var login = await client.RefreshAsync(new CodexCredentials("a", "refresh-antigo", Now, "conta-ficticia", "plus"), CancellationToken.None);

        Assert.Equal("refresh-novo", login.RefreshToken);
        var sent = JsonSerializer.Deserialize<Dictionary<string, string>>(handler.Requests.Single().Body!)!;
        Assert.Equal("refresh_token", sent["grant_type"]);
        Assert.Equal("refresh-antigo", sent["refresh_token"]);
        Assert.Equal(CodexEndpoints.RefreshScopes, sent["scope"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task Refresh_Recusado_DizSePrecisaDeNovoLogin(HttpStatusCode status, bool requiresSignIn)
    {
        var client = new CodexOAuthClient(new HttpClient(new FakeHttpHandler(status, "{}")), new FakeTimeProvider(Now));

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() =>
            client.RefreshAsync(new CodexCredentials("a", "r", Now, null, null), CancellationToken.None));

        Assert.Equal(requiresSignIn, ex.RequiresSignIn);
    }
}
