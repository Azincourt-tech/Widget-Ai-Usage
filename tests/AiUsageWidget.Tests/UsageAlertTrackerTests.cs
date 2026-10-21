using AiUsageWidget.Models;
using AiUsageWidget.Services;

namespace AiUsageWidget.Tests;

public class UsageAlertTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    private readonly UsageAlertTracker _tracker = new();

    private IReadOnlyList<UsageAlert> Read(double percent, string? label = null, string provider = "claude") =>
        _tracker.Check(provider, "Claude", [new UsageWindow(UsageWindowKind.Weekly, percent, null, label)]);

    [Fact]
    public void PrimeiraLeitura_SoServeDeBase()
    {
        Assert.Empty(Read(97));
        Assert.Empty(Read(98));
    }

    [Theory]
    [InlineData(70, 80, 80)]
    [InlineData(79.9, 85, 80)]
    [InlineData(85, 95, 95)]
    [InlineData(70, 97, 95)]
    public void CruzouUmAviso_AvisaOMaisAlto(double before, double after, double threshold)
    {
        Read(before);

        var alert = Assert.Single(Read(after));

        Assert.Equal(threshold, alert.Threshold);
        Assert.Equal(after, alert.UsedPercent);
        Assert.Equal("Semanal", alert.WindowLabel);
    }

    [Theory]
    [InlineData(10, 50)]
    [InlineData(80, 90)]
    [InlineData(96, 99)]
    [InlineData(90, 20)]
    public void SemCruzar_NaoAvisa(double before, double after)
    {
        Read(before);

        Assert.Empty(Read(after));
    }

    [Fact]
    public void DepoisDeRenovar_AvisaDeNovo()
    {
        Read(70);
        Assert.Single(Read(85));
        Read(5);

        Assert.Single(Read(82));
    }

    [Fact]
    public void LimitesDiferentes_SaoAcompanhadosSeparados()
    {
        Read(70, label: "Opus semanal");
        Read(70, provider: "codex");

        Assert.Empty(Read(85));
        Assert.Single(Read(85, label: "Opus semanal"));
        Assert.Single(Read(85, provider: "codex"));
    }

    [Fact]
    public void Esquecer_PrimeiraLeituraVoltaASerBase()
    {
        Read(10);
        _tracker.Forget("claude");

        Assert.Empty(Read(90));
    }

    [Fact]
    public void Textos()
    {
        var tz = TimeZoneInfo.Utc;

        Assert.Equal("Claude: Semanal em 82%", UsageAlertTracker.TitleFor(new UsageAlert("Claude", "Semanal", 80, 82, null)));
        Assert.Equal("Passou de 80% do limite.", UsageAlertTracker.MessageFor(new UsageAlert("Claude", "Semanal", 80, 82, null), Now, tz));
        Assert.Equal("Quase no fim: passou de 95% do limite; renova em 38 min.",
            UsageAlertTracker.MessageFor(new UsageAlert("Codex", "Sessão (5h)", 95, 96, Now.AddMinutes(38)), Now, tz));
    }
}
