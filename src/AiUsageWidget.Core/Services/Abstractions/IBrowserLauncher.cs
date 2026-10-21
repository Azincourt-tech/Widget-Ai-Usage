namespace AiUsageWidget.Services.Abstractions;

/// <summary>Abre um endereço no navegador padrão do usuário.</summary>
public interface IBrowserLauncher
{
    void Open(Uri url);
}
