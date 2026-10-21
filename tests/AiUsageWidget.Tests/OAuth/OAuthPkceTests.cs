using AiUsageWidget.Services.OAuth;

namespace AiUsageWidget.Tests.OAuth;

public class OAuthPkceTests
{
    [Fact]
    public void ChallengeFor_SegueORfc7636()
    {
        // Exemplo do apêndice B da RFC 7636.
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            OAuthPkce.ChallengeFor("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void Create_GeraValoresNovosEUrlSafe()
    {
        var a = OAuthPkce.Create();
        var b = OAuthPkce.Create();

        Assert.NotEqual(a.Verifier, b.Verifier);
        Assert.Equal(43, a.Verifier.Length);
        Assert.DoesNotContain(a.Verifier, c => c is '+' or '/' or '=');
        Assert.Equal(OAuthPkce.ChallengeFor(a.Verifier), a.Challenge);
    }

    [Fact]
    public void CreateState_MudaACadaVez() =>
        Assert.NotEqual(OAuthPkce.CreateState(), OAuthPkce.CreateState());

    [Fact]
    public void Query_EscapaOsValores() =>
        Assert.Equal("a=1%202&b=x%2Fy", OAuthPkce.Query(new Dictionary<string, string> { ["a"] = "1 2", ["b"] = "x/y" }));
}
