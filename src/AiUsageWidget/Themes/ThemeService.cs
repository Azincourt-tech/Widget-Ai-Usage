using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Themes;

public sealed class ThemeService : IThemeService
{
    public void Apply(string theme) => ThemeManager.Apply(theme);
}
