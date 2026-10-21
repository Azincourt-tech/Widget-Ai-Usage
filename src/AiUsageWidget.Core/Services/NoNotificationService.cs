using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Services;

/// <summary>Sem notificações: sistemas ainda sem implementação (Linux, por enquanto).</summary>
public sealed class NoNotificationService : INotificationService
{
    public void Show(string title, string message)
    {
    }
}
