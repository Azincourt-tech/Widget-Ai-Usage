using System.IO;
using System.Text.Json;
using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Services;

/// <summary>
/// Lê e grava as configurações na pasta de dados do usuário. Não guarda tokens nem dados das contas:
/// tokens ficam no cofre do sistema (<see cref="Abstractions.ISecretVault"/>).
/// </summary>
public sealed class SettingsStore(string path) : ISettingsStore
{
    private const int MaxFileSize = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > MaxFileSize)
                return new AppSettings();

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
            return settings.Normalize();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            ErrorLog.Write("Falha ao ler as configurações; usando o padrão.", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Grava num arquivo temporário e troca no fim, para nunca deixar o config pela metade.
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings.Clone().Normalize(), JsonOptions));
        File.Move(temp, path, overwrite: true);
    }
}
