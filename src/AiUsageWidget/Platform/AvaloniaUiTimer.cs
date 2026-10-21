using Avalonia.Threading;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Platform;

/// <summary>Temporizador na thread da interface do Avalonia.</summary>
public sealed class AvaloniaUiTimer : IUiTimer
{
    private readonly DispatcherTimer _timer = new();

    public AvaloniaUiTimer() => _timer.Tick += (_, e) => Tick?.Invoke(this, e);

    public event EventHandler? Tick;

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public bool IsEnabled => _timer.IsEnabled;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}
