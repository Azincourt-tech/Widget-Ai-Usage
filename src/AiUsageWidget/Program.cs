using AiUsageWidget.Constants;
using AiUsageWidget.Services;
using Avalonia;

namespace AiUsageWidget;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var options = StartupOptions.Parse(args);

        // Só uma cópia do widget por vez (o modo demonstração tem a sua própria, para rodar junto).
        var mutexName = options.Demo ? AppConstants.SingleInstanceMutex + ".Demo" : AppConstants.SingleInstanceMutex;
        using var singleInstance = new Mutex(true, mutexName, out var isFirst);
        if (!isFirst)
            return 0;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ErrorLog.Write("Erro fatal.", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ErrorLog.Write("Tarefa em segundo plano falhou.", e.Exception);
            e.SetObserved();
        };

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Usado também pelo designer visual do Avalonia.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
