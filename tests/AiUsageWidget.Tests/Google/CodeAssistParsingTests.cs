using AiUsageWidget.Models;
using AiUsageWidget.Services;
using AiUsageWidget.Services.Google;

namespace AiUsageWidget.Tests.Google;

/// <summary>Leitura das respostas do Code Assist (todas fictícias, no formato do Antigravity).</summary>
public class CodeAssistParsingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    // ---- loadCodeAssist ----

    [Theory]
    [InlineData("""{"cloudaicompanionProject": "projeto-ficticio", "currentTier": {"id": "free-tier"}}""", "projeto-ficticio", "Free")]
    [InlineData("""{"cloudaicompanionProject": {"id": "projeto-ficticio"}, "currentTier": {"id": "standard-tier"}}""", "projeto-ficticio", "Standard")]
    [InlineData("""{"cloudaicompanionProject": "p", "currentTier": {"id": "free-tier"}, "paidTier": {"id": "g1-pro-tier", "name": "Google AI Pro"}}""", "p", "Pro")]
    [InlineData("""{"cloudaicompanionProject": "p", "paidTier": {"id": "g1-ultra-tier", "name": "Google AI Ultra"}}""", "p", "Ultra")]
    [InlineData("""{"cloudaicompanionProject": "p", "currentTier": {"id": "free-tier", "name": "Antigravity"}, "paidTier": {"id": "g1-plus-tier", "name": "Google AI Plus"}}""", "p", "Plus")]
    [InlineData("""{"currentTier": {"id": "free-tier"}}""", null, "Free")]
    [InlineData("""{"cloudaicompanionProject": ""}""", null, null)]
    [InlineData("[]", null, null)]
    [InlineData("{ quebrado", null, null)]
    public void LoadCodeAssist_LeProjetoEPlano(string json, string? project, string? plan) =>
        Assert.Equal(new CodeAssistClient.AccountInfo(project, plan), CodeAssistClient.ParseLoadCodeAssist(json));

    [Fact]
    public void LoadCodeAssist_SemProjeto_GuardaOMotivo()
    {
        // Resposta real (estrutura) de uma conta gratuita quando o cliente não é aceito.
        var info = CodeAssistClient.ParseLoadCodeAssist("""
            {"allowedTiers": [{"id": "standard-tier", "isDefault": true, "userDefinedCloudaicompanionProject": true}],
             "ineligibleTiers": [{"tierId": "free-tier", "reasonCode": "UNSUPPORTED_CLIENT"}]}
            """);

        Assert.Null(info.ProjectId);
        Assert.Equal("UNSUPPORTED_CLIENT", info.IneligibleReason);
    }

    [Fact]
    public void LoadCodeAssist_ComProjeto_IgnoraOMotivo() =>
        Assert.Null(CodeAssistClient.ParseLoadCodeAssist("""
            {"cloudaicompanionProject": "p", "ineligibleTiers": [{"tierId": "x", "reasonCode": "OUTRO"}]}
            """).IneligibleReason);

    // ---- Antigravity: retrieveUserQuotaSummary ----

    private const string Summary = """
        {"groups": [
          {"displayName": "Gemini Pro", "description": "x", "buckets": [
            {"bucketId": "a", "displayName": "Por 5 horas", "window": "5h", "remainingFraction": 0.7, "resetTime": "2026-10-19T18:00:00Z"},
            {"bucketId": "b", "displayName": "Semanal", "window": "weekly", "remainingFraction": 0.4, "resetTime": "2026-10-24T00:00:00Z"}
          ]},
          {"displayName": "Claude e GPT", "buckets": [
            {"bucketId": "c", "displayName": "Por 5 horas", "window": "5h", "remainingFraction": 0.9, "resetTime": "2026-10-19T17:00:00Z"},
            {"bucketId": "d", "displayName": "Desligado", "remainingFraction": 0.0, "disabled": true}
          ]},
          {"displayName": "Sem limites", "buckets": []}
        ], "description": "Cotas do Antigravity"}
        """;

    [Fact]
    public void Antigravity_UmAnelPorGrupo_PeloLimiteMaisUsado()
    {
        var windows = AntigravityUsageProvider.ParseQuotaSummary(Summary, Now);

        Assert.Equal(2, windows.Count);
        Assert.Equal("Gemini Pro", windows[0].Label);
        Assert.Equal("Gemini", windows[0].ShortLabel);
        Assert.Equal(UsageWindowKind.Weekly, windows[0].Kind);
        Assert.Equal(60, Math.Round(windows[0].UsedPercent));
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero), windows[0].ResetsAt);

        // O bucket desligado é ignorado: vale o de 5 horas.
        Assert.Equal("Claude e GPT", windows[1].Label);
        Assert.Equal(UsageWindowKind.FiveHours, windows[1].Kind);
        Assert.Equal(10, Math.Round(windows[1].UsedPercent));
    }

    [Fact]
    public void Antigravity_SemGrupos_UsaOsBuckets()
    {
        var windows = AntigravityUsageProvider.ParseQuotaSummary("""
            {"buckets": [{"displayName": "Geral", "remainingFraction": 0.25, "resetTime": "2026-10-20T15:00:00Z"}]}
            """, Now);

        var window = Assert.Single(windows);
        Assert.Equal("Geral", window.Label);
        Assert.Equal(UsageWindowKind.Daily, window.Kind);
        Assert.Equal(75, Math.Round(window.UsedPercent));
    }

    [Fact]
    public void Antigravity_LimitaQuatroAneis()
    {
        var groups = string.Join(",", Enumerable.Range(1, 6).Select(i =>
            $$"""{"displayName": "G{{i}}", "buckets": [{"remainingFraction": 0.5}]}"""));

        Assert.Equal(AntigravityUsageProvider.MaxWindows, AntigravityUsageProvider.ParseQuotaSummary($$"""{"groups": [{{groups}}]}""", Now).Count);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"groups": []}""")]
    [InlineData("""{"groups": [{"displayName": "x", "buckets": [{"disabled": true, "remainingFraction": 0.1}]}]}""")]
    [InlineData("[]")]
    [InlineData("{ quebrado")]
    public void Antigravity_SemLimites_ViraErro(string json) =>
        Assert.Throws<UsageProviderException>(() => AntigravityUsageProvider.ParseQuotaSummary(json, Now));

    [Theory]
    [InlineData("weekly", null, UsageWindowKind.Weekly)]
    [InlineData("7d", null, UsageWindowKind.Weekly)]
    [InlineData("daily", null, UsageWindowKind.Daily)]
    [InlineData("24h", null, UsageWindowKind.Daily)]
    [InlineData("5h", null, UsageWindowKind.FiveHours)]
    [InlineData(null, 2, UsageWindowKind.FiveHours)]
    [InlineData(null, 20, UsageWindowKind.Daily)]
    [InlineData(null, 100, UsageWindowKind.Weekly)]
    [InlineData("algo", null, UsageWindowKind.Daily)]
    public void Antigravity_TipoPeloTextoOuPeloTempo(string? window, int? hoursToReset, UsageWindowKind expected) =>
        Assert.Equal(expected, AntigravityUsageProvider.KindFor(window, hoursToReset is int h ? Now.AddHours(h) : null, Now));

    [Fact]
    public void Antigravity_RespostaReal_DoisGruposSemanais()
    {
        // Estrutura real do Antigravity (outubro de 2026), com percentuais fictícios.
        var windows = AntigravityUsageProvider.ParseQuotaSummary("""
            {"groups": [
              {"displayName": "Gemini Models", "buckets": [{"bucketId": "a", "displayName": "Weekly Limit Remaining", "window": "weekly", "resetTime": "2026-10-24T00:00:00Z", "remainingFraction": 0.8}]},
              {"displayName": "Claude and GPT models", "buckets": [{"bucketId": "b", "displayName": "Weekly Limit Remaining", "window": "weekly", "resetTime": "2026-10-24T00:00:00Z", "remainingFraction": 0.3}]}
            ], "description": "Within each group, models share a weekly limit."}
            """, Now);

        Assert.Equal(["Gemini", "Claude e GPT"], windows.Select(w => w.Label));
        Assert.Equal(["Gemini", "C+GPT"], windows.Select(w => w.ShortLabel));
        Assert.All(windows, w => Assert.Equal(UsageWindowKind.Weekly, w.Kind));
    }

    [Theory]
    [InlineData("Gemini Models", "Gemini")]
    [InlineData("Claude and GPT models", "Claude e GPT")]
    [InlineData("Outro grupo", "Outro grupo")]
    public void Antigravity_NomeDoGrupo(string name, string expected) =>
        Assert.Equal(expected, AntigravityUsageProvider.GroupLabel(name));

    [Theory]
    [InlineData("Gemini Pro", "Gemini")]
    [InlineData("Antigravity", "Antigr")]
    [InlineData("GPT", "GPT")]
    [InlineData("Claude e GPT", "C+GPT")]
    [InlineData("Gemini e Claude e GPT", "GC+GPT")]
    [InlineData("Claude e Modelos externos", "Claude")]
    public void Antigravity_RotuloCurto(string label, string expected) =>
        Assert.Equal(expected, AntigravityUsageProvider.ShortLabelFor(label));
}
