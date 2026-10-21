using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests;

public class UsageFormatterTests
{
    // Segunda-feira, 19/10/2026, 12:00 em Brasília.
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Zone = FakeTimeProvider.Brasilia;

    [Theory]
    [InlineData(UsageWindowKind.FiveHours, "Sessão (5h)")]
    [InlineData(UsageWindowKind.Daily, "Diário")]
    [InlineData(UsageWindowKind.Weekly, "Semanal")]
    public void WindowLabel_TemNomeEmPortugues(UsageWindowKind kind, string expected) =>
        Assert.Equal(expected, UsageFormatter.WindowLabel(kind));

    [Theory]
    [InlineData(UsageWindowKind.FiveHours, "5h")]
    [InlineData(UsageWindowKind.Daily, "24h")]
    [InlineData(UsageWindowKind.Weekly, "7d")]
    public void ShortWindowLabel_ParaOTamanhoPequeno(UsageWindowKind kind, string expected) =>
        Assert.Equal(expected, UsageFormatter.ShortWindowLabel(kind));

    [Theory]
    [InlineData("plus", "Plus")]
    [InlineData("PRO", "Pro")]
    [InlineData(" free ", "Free")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void PlanName_Capitaliza(string? plan, string? expected) =>
        Assert.Equal(expected, UsageFormatter.PlanName(plan));

    [Theory]
    [InlineData(42.4, "42%")]
    [InlineData(42.5, "43%")]
    [InlineData(-5, "0%")]
    [InlineData(130, "100%")]
    [InlineData(double.NaN, "0%")]
    public void PercentText_ArredondaELimita(double percent, string expected) =>
        Assert.Equal(expected, UsageFormatter.PercentText(percent));

    [Theory]
    [InlineData(0, UsageLevel.Normal)]
    [InlineData(74.9, UsageLevel.Normal)]
    [InlineData(75, UsageLevel.Warning)]
    [InlineData(89.9, UsageLevel.Warning)]
    [InlineData(90, UsageLevel.Critical)]
    [InlineData(150, UsageLevel.Critical)]
    public void LevelFor_UsaAsFaixas(double percent, UsageLevel expected) =>
        Assert.Equal(expected, UsageFormatter.LevelFor(percent));

    [Fact]
    public void ResetText_SemHorario_FicaVazio() =>
        Assert.Equal("", UsageFormatter.ResetText(null, Now, Zone));

    [Fact]
    public void ResetText_NoPassado_MostraRenovando() =>
        Assert.Equal("renovando...", UsageFormatter.ResetText(Now.AddSeconds(-1), Now, Zone));

    [Fact]
    public void ResetText_MenosDeUmaHora_MostraMinutos() =>
        Assert.Equal("renova em 38 min", UsageFormatter.ResetText(Now.AddMinutes(37).AddSeconds(10), Now, Zone));

    [Fact]
    public void ResetText_PoucosSegundos_MostraUmMinuto() =>
        Assert.Equal("renova em 1 min", UsageFormatter.ResetText(Now.AddSeconds(5), Now, Zone));

    [Fact]
    public void ResetText_MenosDeUmDia_MostraHorasEMinutos() =>
        Assert.Equal("renova em 2h 05min", UsageFormatter.ResetText(Now.AddHours(2).AddMinutes(5), Now, Zone));

    [Fact]
    public void ResetText_MaisDeUmDia_MostraDiaEHoraLocal() =>
        // Quinta 22/10 às 12:00 UTC = 09:00 em Brasília.
        Assert.Equal("renova qui. 09:00", UsageFormatter.ResetText(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero), Now, Zone));

    [Fact]
    public void UpdatedText_Recente_MostraAgora() =>
        Assert.Equal("Atualizado agora", UsageFormatter.UpdatedText(Now.AddSeconds(-20), Now, Zone));

    [Fact]
    public void UpdatedText_HaMinutos_MostraMinutos() =>
        Assert.Equal("Atualizado há 7 min", UsageFormatter.UpdatedText(Now.AddMinutes(-7), Now, Zone));

    [Fact]
    public void UpdatedText_HaMaisDeUmaHora_MostraHorario() =>
        Assert.Equal("Atualizado às 10:30", UsageFormatter.UpdatedText(Now.AddMinutes(-90), Now, Zone));
}
