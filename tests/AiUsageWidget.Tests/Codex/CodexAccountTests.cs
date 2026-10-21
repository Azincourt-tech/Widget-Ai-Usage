using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Web;
using AiUsageWidget.Constants;
using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Codex;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests.Codex;

/// <summary>
/// Estes testes usam a porta fixa 1455 de verdade. Ficam na mesma classe para o xUnit não rodá-los
/// ao mesmo tempo.
/// </summary>
public class CodexAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    private readonly FakeCredentialStore<CodexCredentials> _store = new();
    private readonly FakeTimeProvider _time = new(Now);

    private CodexAccount Create(FakeHttpHandler handler, FakeBrowserLauncher browser) =>
        new(_store, new CodexOAuthClient(new HttpClient(handler), _time), browser, _time);

    [Fact]
    public async Task SignIn_UsaAPortaFixaETrocaOCodigo()
    {
        // O "navegador" volta para o endereço de retorno com o código, como a OpenAI faria.
        Task<string>? page = null;
        var browser = new FakeBrowserLauncher(url =>
        {
            var query = HttpUtility.ParseQueryString(url.Query);
            page = Task.Run(async () =>
            {
                using var http = new HttpClient();
                return await http.GetStringAsync($"{query["redirect_uri"]}?code=codigo-ficticio&state={query["state"]}");
            });
        });
        var idToken = TestJwt.Create(new Dictionary<string, object>
        {
            [CodexEndpoints.AuthClaim] = new { chatgpt_account_id = "conta-ficticia", chatgpt_plan_type = "plus" },
        });
        var handler = new FakeHttpHandler(HttpStatusCode.OK,
            $$"""{"access_token": "token-ficticio", "refresh_token": "r", "id_token": "{{idToken}}", "expires_in": 3600}""");

        await Create(handler, browser).SignInAsync(CancellationToken.None);

        var redirect = HttpUtility.ParseQueryString(browser.Opened!.Query)["redirect_uri"];
        Assert.Equal("http://localhost:1455/auth/callback", redirect);
        Assert.Equal("conta-ficticia", _store.Current?.AccountId);
        Assert.Equal("plus", _store.Current?.PlanType);

        // Sem página de sucesso do provedor: o próprio widget responde.
        Assert.Equal("Login concluído. Pode fechar esta aba e voltar ao widget.", await page!);
    }

    [Fact]
    public async Task SignIn_PortaOcupada_ExplicaOQueFazer()
    {
        using var busy = new TcpListener(IPAddress.Loopback, CodexEndpoints.CallbackPort);
        busy.Start();
        var browser = new FakeBrowserLauncher();

        var ex = await Assert.ThrowsAsync<UsageProviderException>(() =>
            Create(new FakeHttpHandler(HttpStatusCode.OK, "{}"), browser).SignInAsync(CancellationToken.None));

        Assert.Contains("1455", ex.Message);
        Assert.Null(browser.Opened);
    }
}
