namespace AiUsageWidget.ViewModels;

/// <summary>Dica do ícone da bandeja: uma linha por conta visível com o maior uso ("Claude: 42%").</summary>
public static class TrayToolTip
{
    /// <summary>O Windows corta a dica do ícone da bandeja em 127 caracteres.</summary>
    public const int MaxLength = 127;

    public const string Empty = "Nenhuma conta para mostrar";

    public static string For(IEnumerable<ProviderCardViewModel> cards)
    {
        var text = string.Join("\n", cards.Select(LineFor));
        if (text.Length == 0)
            return Empty;

        return text.Length > MaxLength ? text[..(MaxLength - 3)] + "..." : text;
    }

    private static string LineFor(ProviderCardViewModel card) =>
        card.NeedsSignIn ? $"{card.Name}: entrar" : $"{card.Name}: {card.PeakText}";
}
