using System.Diagnostics;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Platform;

/// <summary>Abre o endereço no navegador padrão (no Windows, pelo shell).</summary>
public sealed class BrowserLauncher : IBrowserLauncher
{
    public void Open(Uri url)
    {
        // Só endereços web: nunca repassa ao shell algo que possa ser um programa ou arquivo.
        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
            throw new ArgumentException("Só endereços http(s) podem ser abertos.", nameof(url));

        Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }
}
