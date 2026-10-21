using AiUsageWidget.Models;

namespace AiUsageWidget.Services.Abstractions;

/// <summary>
/// Lê os limites de uma conta de IA (Claude, Codex, Gemini...). Cada provedor é um adaptador
/// isolado: quando o formato de um serviço mudar, só o adaptador dele muda.
/// </summary>
public interface IUsageProvider
{
    /// <summary>Um dos valores de <see cref="ProviderIds"/>.</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>Login feito pelo widget, quando o provedor precisa de um. Nulo se não precisa.</summary>
    IProviderAccount? Account => null;

    /// <summary>
    /// Lê os limites atuais. Problemas esperados (sem login, token expirado, sem rede) devem virar
    /// <see cref="UsageProviderException"/> com uma mensagem que o usuário entenda.
    /// </summary>
    Task<ProviderUsage> GetUsageAsync(CancellationToken cancellationToken);
}
