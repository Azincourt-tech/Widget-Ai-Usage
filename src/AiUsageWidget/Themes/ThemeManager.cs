using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using AiUsageWidget.Models;

namespace AiUsageWidget.Themes;

/// <summary>
/// Troca o dicionário de cores do tema em tempo real. A interface usa DynamicResource com as chaves
/// definidas em Themes/Dark.axaml, Light.axaml e Glass.axaml. Também ajusta a variante clara/escura
/// dos controles padrão (Fluent), como menus e caixas de texto.
/// </summary>
public static class ThemeManager
{
    private const string ThemeFolder = "avares://AiUsageWidget/Themes/";

    // O dicionário do tema atual. Fica sob controle daqui (e não no App.axaml) porque o Avalonia
    // compila os ResourceInclude do XAML e eles deixam de ser identificáveis para troca.
    private static IResourceProvider? _current;

    public static void Apply(string? theme)
    {
        var app = Application.Current;
        if (app is null)
            return;

        theme = ThemeNames.Normalize(theme);
        app.RequestedThemeVariant = theme == ThemeNames.Light ? ThemeVariant.Light : ThemeVariant.Dark;

        var dictionaries = app.Resources.MergedDictionaries;
        if (_current is not null)
            dictionaries.Remove(_current);

        // O último dicionário mesclado tem prioridade, então o tema entra no fim.
        _current = new ResourceInclude(new Uri(ThemeFolder)) { Source = new Uri($"{ThemeFolder}{theme}.axaml") };
        dictionaries.Add(_current);
    }
}
