namespace AiUsageWidget.Services.Abstractions;

/// <summary>
/// Temporizador que dispara na thread da interface. Cada framework de interface fornece o seu
/// (no Avalonia, o DispatcherTimer).
/// </summary>
public interface IUiTimer
{
    TimeSpan Interval { get; set; }

    bool IsEnabled { get; }

    event EventHandler? Tick;

    void Start();

    void Stop();
}
