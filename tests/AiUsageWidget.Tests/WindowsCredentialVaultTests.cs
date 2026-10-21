using System.Runtime.Versioning;
using AiUsageWidget.Services.Security;

namespace AiUsageWidget.Tests;

/// <summary>
/// Usa o Gerenciador de Credenciais de verdade, com uma chave de teste que é apagada no fim.
/// Em outros sistemas os testes passam sem fazer nada.
/// </summary>
public sealed class WindowsCredentialVaultTests : IDisposable
{
    private readonly string _key = $"AiUsageWidget.Tests/{Guid.NewGuid():N}";

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
            new WindowsCredentialVault().Delete(_key);
    }

    [Fact]
    public void GravaLeEApaga()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var vault = new WindowsCredentialVault();
        vault.Write(_key, "segredo fictício ✓");

        Assert.Equal("segredo fictício ✓", vault.Read(_key));

        vault.Delete(_key);
        Assert.Null(vault.Read(_key));
    }

    [Fact]
    public void ChaveInexistente_DevolveNuloEApagarNaoFalha()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var vault = new WindowsCredentialVault();

        Assert.Null(vault.Read(_key));
        vault.Delete(_key);
    }

    [Fact]
    [SupportedOSPlatform("windows")] // o analisador não enxerga a checagem de sistema dentro do lambda
    public void SegredoGrandeDemais_Recusa()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Throws<ArgumentException>(() => new WindowsCredentialVault().Write(_key, new string('x', WindowsCredentialVault.MaxSecretBytes + 1)));
    }
}
