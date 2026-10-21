namespace AiUsageWidget.Services;

/// <summary>
/// Falha esperada ao ler os limites de um provedor. A mensagem aparece no card do provedor,
/// então deve ser curta e dizer o que fazer (ex.: "Entre com sua conta do Claude").
/// </summary>
public sealed class UsageProviderException : Exception
{
    public UsageProviderException(string message, bool requiresSignIn = false) : base(message) =>
        RequiresSignIn = requiresSignIn;

    public UsageProviderException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>Verdadeiro quando o usuário precisa entrar (de novo) na conta: o card mostra o botão de login.</summary>
    public bool RequiresSignIn { get; }

    /// <summary>Verdadeiro quando a API respondeu "muitas consultas" (429): o widget pausa essa conta por um tempo.</summary>
    public bool IsRateLimited { get; private init; }

    /// <summary>Resposta 429 da API do provedor.</summary>
    public static UsageProviderException RateLimited() =>
        new("Muitas consultas seguidas; pausado por alguns minutos.") { IsRateLimited = true };
}
