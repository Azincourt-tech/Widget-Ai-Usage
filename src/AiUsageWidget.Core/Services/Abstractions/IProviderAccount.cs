namespace AiUsageWidget.Services.Abstractions;

/// <summary>
/// Conta de um provedor que precisa de login feito pelo próprio widget (ex.: Claude).
/// </summary>
public interface IProviderAccount
{
    bool IsSignedIn { get; }

    /// <summary>Abre o login no navegador e espera o usuário concluir (ou o cancelamento).</summary>
    Task SignInAsync(CancellationToken cancellationToken);

    /// <summary>Esquece o login guardado no cofre.</summary>
    void SignOut();
}
