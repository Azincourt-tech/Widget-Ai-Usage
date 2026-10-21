using AiUsageWidget.Platform.Windows;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Abstractions;
using AiUsageWidget.Services.Security;

namespace AiUsageWidget.Platform;

/// <summary>
/// Escolhe a implementação de cada serviço que depende do sistema operacional.
/// Hoje só o Windows é suportado; o Linux está no roadmap (docs/ROADMAP.md). macOS não será suportado.
/// </summary>
public static class PlatformServices
{
    private const string NotSupportedMessage =
        "Por enquanto o widget só roda no Windows. O suporte ao Linux está planejado (docs/ROADMAP.md). " +
        "Para testar a interface no Linux, use o modo demonstração: --demo.";

    /// <summary>Iniciar com o Windows (chave Run do usuário).</summary>
    public static IStartupRegistration CreateStartupRegistration() =>
        OperatingSystem.IsWindows()
            ? new WindowsStartupRegistration()
            : throw new PlatformNotSupportedException(NotSupportedMessage);

    /// <summary>Notificações do sistema. Fora do Windows, por enquanto, nenhuma (os anéis já mostram o limite).</summary>
    public static INotificationService CreateNotificationService() =>
#if WINDOWS
        new WindowsNotificationService();
#else
        new NoNotificationService();
#endif

    /// <summary>Cofre de segredos do sistema (Gerenciador de Credenciais do Windows).</summary>
    public static ISecretVault CreateSecretVault() =>
        OperatingSystem.IsWindows()
            ? new WindowsCredentialVault()
            : throw new PlatformNotSupportedException(NotSupportedMessage);
}
