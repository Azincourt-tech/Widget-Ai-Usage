using AiUsageWidget.Models;

namespace AiUsageWidget.Services.Abstractions;

/// <summary>Lê e grava as preferências do usuário.</summary>
public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
