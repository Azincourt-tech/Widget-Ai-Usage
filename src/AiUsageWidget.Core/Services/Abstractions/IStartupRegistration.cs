namespace AiUsageWidget.Services.Abstractions;

/// <summary>Iniciar junto com o sistema.</summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void Set(bool enabled);
}
