namespace AiUsageWidget.Services.Abstractions;

/// <summary>Notificação do sistema (no Windows, a que vai para a Central de Notificações).</summary>
public interface INotificationService
{
    /// <summary>Mostra a notificação. Falhas não derrubam o widget (a implementação registra no log).</summary>
    void Show(string title, string message);
}
