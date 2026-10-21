using System.Text.Json;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Services.OAuth;

/// <summary>Guarda o login de um provedor no cofre do sistema, como JSON, sob uma chave própria.</summary>
public sealed class VaultCredentialStore<T>(ISecretVault vault, string key) : ICredentialStore<T>
    where T : class, IOAuthCredentials
{
    public string Key => key;

    public T? Load()
    {
        var json = vault.Read(key);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var stored = JsonSerializer.Deserialize<T>(json);
            return string.IsNullOrWhiteSpace(stored?.AccessToken) ? null : stored;
        }
        catch (JsonException ex)
        {
            // Conteúdo estragado: trata como "sem login" e pede para entrar de novo.
            ErrorLog.Write($"Login guardado em {key} está ilegível; descartado.", ex);
            vault.Delete(key);
            return null;
        }
    }

    public void Save(T credentials) => vault.Write(key, JsonSerializer.Serialize(credentials));

    public void Clear() => vault.Delete(key);
}
