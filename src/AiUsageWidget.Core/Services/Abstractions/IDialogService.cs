using AiUsageWidget.Models;

namespace AiUsageWidget.Services.Abstractions;

/// <summary>Janelas abertas a partir dos ViewModels, sem eles conhecerem as telas.</summary>
public interface IDialogService
{
    /// <summary>
    /// Abre as configurações. <paramref name="providers"/> são as contas que podem ser mostradas ou
    /// escondidas. Retorna true se o usuário salvou (e então <paramref name="settings"/> já foi atualizado).
    /// </summary>
    Task<bool> ShowSettingsAsync(AppSettings settings, IReadOnlyList<IUsageProvider> providers);
}
