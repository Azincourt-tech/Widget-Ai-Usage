using System.Runtime.Versioning;
using AiUsageWidget.Constants;
using AiUsageWidget.Services.Abstractions;
using Microsoft.Win32;

namespace AiUsageWidget.Platform.Windows;

/// <summary>
/// Liga ou desliga a abertura do widget junto com o Windows (chave Run do usuário, sem precisar de admin).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsStartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(AppConstants.AppName) is not null;
        }
    }

    public void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
            return;

        if (enabled && Environment.ProcessPath is { } exe)
            key.SetValue(AppConstants.AppName, $"\"{exe}\"");
        else
            key.DeleteValue(AppConstants.AppName, throwOnMissingValue: false);
    }
}
