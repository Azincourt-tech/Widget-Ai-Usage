namespace AiUsageWidget.Services.Abstractions;

/// <summary>
/// Cofre de segredos do sistema: Gerenciador de Credenciais no Windows. (Linux, com libsecret: no roadmap.)
/// </summary>
public interface ISecretVault
{
    /// <summary>O segredo guardado com essa chave, ou nulo se não houver.</summary>
    string? Read(string key);

    void Write(string key, string secret);

    /// <summary>Apaga o segredo. Não faz nada se ele não existir.</summary>
    void Delete(string key);
}
