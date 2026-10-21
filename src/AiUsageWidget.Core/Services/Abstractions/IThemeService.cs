namespace AiUsageWidget.Services.Abstractions;

/// <summary>Troca as cores do app em tempo real.</summary>
public interface IThemeService
{
    void Apply(string theme);
}
