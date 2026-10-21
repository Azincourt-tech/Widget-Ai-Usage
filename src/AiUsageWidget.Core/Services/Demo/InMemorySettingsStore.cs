using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Services.Demo;

/// <summary>Configurações só em memória, para o modo demonstração não tocar no config.json.</summary>
public sealed class InMemorySettingsStore(AppSettings initial) : ISettingsStore
{
    private AppSettings _settings = initial.Clone().Normalize();

    public AppSettings Load() => _settings.Clone();

    public void Save(AppSettings settings) => _settings = settings.Clone().Normalize();
}
