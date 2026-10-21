using System.IO;

namespace AiUsageWidget.Constants;

/// <summary>
/// Nomes e caminhos fixos do aplicativo.
/// </summary>
public static class AppConstants
{
    public const string AppName = "AiUsageWidget";
    public const string SingleInstanceMutex = "AiUsageWidget.SingleInstance";

    /// <summary>Configurações do usuário (tema, tamanho, posição, contas visíveis...).</summary>
    public static string SettingsFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    /// <summary>Logs de erro, só nesta máquina.</summary>
    public static string LogsFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "logs");

    public static string SettingsFile => Path.Combine(SettingsFolder, "config.json");
}
