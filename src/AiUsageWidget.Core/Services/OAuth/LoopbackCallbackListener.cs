using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace AiUsageWidget.Services.OAuth;

/// <summary>
/// Recebe o retorno do login no navegador: um servidor HTTP mínimo, só no próprio PC (127.0.0.1 e ::1),
/// numa porta livre escolhida pelo sistema (ou fixa, quando o provedor exige). Atende um único login e é descartado.
/// </summary>
public sealed class LoopbackCallbackListener : IDisposable
{
    public const string DefaultPath = "/callback";
    private const int MaxRequestHead = 8 * 1024;

    private readonly TcpListener _ipv4;
    private readonly TcpListener? _ipv6;
    private readonly string _path;

    private LoopbackCallbackListener(TcpListener ipv4, TcpListener? ipv6, string path)
    {
        _ipv4 = ipv4;
        _ipv6 = ipv6;
        _path = path;
        var port = ((IPEndPoint)ipv4.LocalEndpoint).Port;
        RedirectUri = new Uri($"http://localhost:{port}{path}");
    }

    /// <summary>Endereço de retorno informado no login (<c>http://localhost:porta/caminho</c>).</summary>
    public Uri RedirectUri { get; }

    /// <param name="port">0 para uma porta livre; ou a porta fixa que o provedor exige no retorno.</param>
    /// <param name="path">Caminho do retorno (ex.: <c>/callback</c>).</param>
    /// <exception cref="SocketException">A porta fixa já está em uso.</exception>
    public static LoopbackCallbackListener Start(int port = 0, string path = DefaultPath)
    {
        var ipv4 = new TcpListener(IPAddress.Loopback, port);
        ipv4.Start();

        // O navegador pode resolver "localhost" como ::1; escuta lá também, na mesma porta, se der.
        TcpListener? ipv6 = null;
        try
        {
            ipv6 = new TcpListener(IPAddress.IPv6Loopback, ((IPEndPoint)ipv4.LocalEndpoint).Port);
            ipv6.Start();
        }
        catch (SocketException)
        {
            ipv6?.Dispose();
            ipv6 = null;
        }

        return new LoopbackCallbackListener(ipv4, ipv6, path);
    }

    /// <summary>
    /// Espera o navegador voltar com o código de login. Pedidos para outros caminhos (ex.: favicon)
    /// são ignorados. Depois do código, o navegador é levado para <paramref name="successPage"/> ou, sem ela,
    /// recebe uma página simples dizendo que pode fechar a aba.
    /// </summary>
    public async Task<string> WaitForCodeAsync(string expectedState, Uri? successPage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stop = cancellationToken.Register(Dispose);
        while (true)
        {
            TcpClient client;
            try
            {
                client = await AcceptAnyAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SocketException or InvalidOperationException
                                       && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            using (client)
            {
                var stream = client.GetStream();
                var requestLine = await ReadRequestLineAsync(stream, cancellationToken);

                string? code;
                try
                {
                    code = ParseCallback(requestLine, expectedState, _path);
                }
                catch (UsageProviderException)
                {
                    await RespondAsync(stream, "400 Bad Request", null, "Login não concluído. Volte ao widget e tente de novo.", cancellationToken);
                    throw;
                }

                if (code is null)
                {
                    await RespondAsync(stream, "404 Not Found", null, "", cancellationToken);
                    continue;
                }

                if (successPage is not null)
                    await RespondAsync(stream, "302 Found", successPage, "", cancellationToken);
                else
                    await RespondAsync(stream, "200 OK", null, "Login concluído. Pode fechar esta aba e voltar ao widget.", cancellationToken);
                return code;
            }
        }
    }

    /// <summary>
    /// Lê a primeira linha do pedido (<c>GET /callback?code=...&amp;state=... HTTP/1.1</c>).
    /// Devolve o código; nulo se for outro caminho; erro se o login foi negado ou o <c>state</c> não confere.
    /// </summary>
    public static string? ParseCallback(string requestLine, string expectedState, string path = DefaultPath)
    {
        var parts = requestLine.Split(' ');
        if (parts.Length < 2 || parts[0] != "GET")
            return null;

        var target = parts[1];
        var queryStart = target.IndexOf('?');
        var requestPath = queryStart < 0 ? target : target[..queryStart];
        if (requestPath != path)
            return null;

        var query = HttpUtility.ParseQueryString(queryStart < 0 ? "" : target[(queryStart + 1)..]);
        if (query["error"] is not null)
            throw new UsageProviderException("O login foi cancelado.", requiresSignIn: true);
        if (query["state"] != expectedState)
            throw new UsageProviderException("O retorno do login não confere. Tente entrar de novo.", requiresSignIn: true);

        return string.IsNullOrEmpty(query["code"])
            ? throw new UsageProviderException("O login voltou sem código. Tente entrar de novo.", requiresSignIn: true)
            : query["code"];
    }

    public void Dispose()
    {
        _ipv4.Dispose();
        _ipv6?.Dispose();
    }

    private async Task<TcpClient> AcceptAnyAsync(CancellationToken cancellationToken)
    {
        if (_ipv6 is null)
            return await _ipv4.AcceptTcpClientAsync(cancellationToken);

        var v4 = _ipv4.AcceptTcpClientAsync(cancellationToken).AsTask();
        var v6 = _ipv6.AcceptTcpClientAsync(cancellationToken).AsTask();
        var first = await Task.WhenAny(v4, v6);

        // A outra espera continua pendente; se ela chegar a aceitar algo, a conexão é fechada.
        // Se falhar (listener descartado), o erro é só observado, sem ir para o log.
        var other = first == v4 ? v6 : v4;
        _ = other.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
                t.Result.Dispose();
            else
                _ = t.Exception;
        }, TaskScheduler.Default);
        return await first;
    }

    private static async Task<string> ReadRequestLineAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxRequestHead];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (n == 0)
                break;
            read += n;
            if (Array.IndexOf(buffer, (byte)'\n', 0, read) >= 0)
                break;
        }

        var text = Encoding.ASCII.GetString(buffer, 0, read);
        var end = text.IndexOf('\r');
        return end < 0 ? text.Trim() : text[..end];
    }

    private static async Task RespondAsync(Stream stream, string status, Uri? location, string message, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(message);
        var head = new StringBuilder()
            .Append("HTTP/1.1 ").Append(status).Append("\r\n")
            .Append("Content-Type: text/plain; charset=utf-8\r\n")
            .Append("Content-Length: ").Append(body.Length).Append("\r\n")
            .Append("Connection: close\r\n");
        if (location is not null)
            head.Append("Location: ").Append(location).Append("\r\n");
        head.Append("\r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString()), cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
