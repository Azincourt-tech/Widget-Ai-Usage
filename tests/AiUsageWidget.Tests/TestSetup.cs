using System.IO;
using System.Runtime.CompilerServices;
using AiUsageWidget.Services;

namespace AiUsageWidget.Tests;

internal static class TestSetup
{
    /// <summary>
    /// Roda antes de qualquer teste: erros esperados nos testes não podem ir para o log real do usuário.
    /// </summary>
    [ModuleInitializer]
    internal static void RedirectErrorLog() =>
        ErrorLog.Folder = Path.Combine(Path.GetTempPath(), "AiUsageWidget.Tests", "logs");
}
