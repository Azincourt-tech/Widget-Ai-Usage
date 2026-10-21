namespace AiUsageWidget.Services.OAuth;

/// <summary>O mínimo que todo login OAuth guardado precisa ter para ser usado e renovado.</summary>
public interface IOAuthCredentials
{
    string AccessToken { get; }

    /// <summary>Usado para renovar o <see cref="AccessToken"/> quando ele vence.</summary>
    string? RefreshToken { get; }

    /// <summary>Validade do <see cref="AccessToken"/>, se conhecida.</summary>
    DateTimeOffset? ExpiresAt { get; }
}
