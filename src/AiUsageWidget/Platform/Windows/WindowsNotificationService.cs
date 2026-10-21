#if WINDOWS
using AiUsageWidget.Services;
using AiUsageWidget.Services.Abstractions;
using Microsoft.Toolkit.Uwp.Notifications;

namespace AiUsageWidget.Platform.Windows;

/// <summary>
/// Notificação nativa do Windows (Central de Notificações; respeita o "Não incomodar"). O pacote registra
/// o widget como remetente de notificações no registro do usuário na primeira vez.
/// </summary>
public sealed class WindowsNotificationService : INotificationService
{
    public void Show(string title, string message)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
        catch (Exception ex)
        {
            // Sem notificação o widget segue normal: os anéis já mostram o limite.
            ErrorLog.Write("Falha ao mostrar a notificação.", ex);
        }
    }
}
#endif
