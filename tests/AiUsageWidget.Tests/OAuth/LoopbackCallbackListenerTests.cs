using System.Net;
using System.Net.Http;
using AiUsageWidget.Services;
using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Tests.OAuth;

public class LoopbackCallbackListenerTests
{
    private static readonly Uri Success = new("https://exemplo.invalid/sucesso");

    [Fact]
    public void ParseCallback_DevolveOCodigo() =>
        Assert.Equal("abc", LoopbackCallbackListener.ParseCallback("GET /callback?code=abc&state=s1 HTTP/1.1", "s1"));

    [Fact]
    public void ParseCallback_CaminhoProprioDoProvedor()
    {
        Assert.Equal("abc", LoopbackCallbackListener.ParseCallback("GET /auth/callback?code=abc&state=s1 HTTP/1.1", "s1", "/auth/callback"));
        Assert.Null(LoopbackCallbackListener.ParseCallback("GET /callback?code=abc&state=s1 HTTP/1.1", "s1", "/auth/callback"));
    }

    [Theory]
    [InlineData("GET /favicon.ico HTTP/1.1")]
    [InlineData("POST /callback?code=abc&state=s1 HTTP/1.1")]
    [InlineData("")]
    public void ParseCallback_OutroPedido_Ignora(string requestLine) =>
        Assert.Null(LoopbackCallbackListener.ParseCallback(requestLine, "s1"));

    [Theory]
    [InlineData("GET /callback?error=access_denied&state=s1 HTTP/1.1")]
    [InlineData("GET /callback?code=abc&state=outro HTTP/1.1")]
    [InlineData("GET /callback?code=abc HTTP/1.1")]
    [InlineData("GET /callback?state=s1 HTTP/1.1")]
    public void ParseCallback_NegadoOuForjado_ViraErro(string requestLine)
    {
        var ex = Assert.Throws<UsageProviderException>(() => LoopbackCallbackListener.ParseCallback(requestLine, "s1"));

        Assert.True(ex.RequiresSignIn);
    }

    [Fact]
    public async Task Listener_RecebeCodigoERedirecionaParaSucesso()
    {
        using var listener = LoopbackCallbackListener.Start();
        Assert.Equal("localhost", listener.RedirectUri.Host);

        var wait = listener.WaitForCodeAsync("s1", Success, CancellationToken.None);

        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var favicon = await http.GetAsync(new Uri(listener.RedirectUri, "/favicon.ico"));
        var callback = await http.GetAsync($"{listener.RedirectUri}?code=codigo-ficticio&state=s1");

        Assert.Equal(HttpStatusCode.NotFound, favicon.StatusCode);
        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Equal(Success, callback.Headers.Location);
        Assert.Equal("codigo-ficticio", await wait);
    }

    [Fact]
    public async Task Listener_Cancelado_ParaDeEsperar()
    {
        using var listener = LoopbackCallbackListener.Start();
        using var cts = new CancellationTokenSource();

        var wait = listener.WaitForCodeAsync("s1", Success, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }
}
