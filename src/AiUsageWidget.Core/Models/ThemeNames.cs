namespace AiUsageWidget.Models;

public static class ThemeNames
{
    public const string Glass = "Glass";
    public const string Dark = "Dark";
    public const string Light = "Light";

    public static string Normalize(string? theme) => theme is Dark or Light ? theme : Glass;
}
