namespace AiUsageWidget.Models;

/// <summary>Identificadores e cores de destaque dos provedores suportados.</summary>
public static class ProviderIds
{
    public const string Claude = "claude";
    public const string Codex = "codex";

    /// <summary>Cota do Antigravity (a mesma para o Antigravity IDE e o Antigravity CLI).</summary>
    public const string Antigravity = "antigravity";

    public static readonly IReadOnlyList<string> All = [Claude, Codex, Antigravity];

    /// <summary>Cor de destaque do anel de cada provedor (#RRGGBB).</summary>
    public static string AccentFor(string id) => id switch
    {
        Claude => "#D97757",
        Codex => "#10A37F",
        Antigravity => "#A970FF",
        _ => "#0A84FF",
    };
}
