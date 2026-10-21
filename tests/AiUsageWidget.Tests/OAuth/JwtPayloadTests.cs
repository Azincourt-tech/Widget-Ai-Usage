using AiUsageWidget.Services.OAuth;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests.OAuth;

public class JwtPayloadTests
{
    [Fact]
    public void Read_LeOConteudo()
    {
        var payload = JwtPayload.Read(TestJwt.Create(new { sub = "usuario-ficticio" }));

        Assert.Equal("usuario-ficticio", payload?.GetProperty("sub").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem-pontos")]
    [InlineData("a.%%%.c")]
    [InlineData("a.bm8.c")] // "no": base64 válido, mas não é JSON
    public void Read_TokenInvalido_DevolveNulo(string? token) =>
        Assert.Null(JwtPayload.Read(token));

    [Fact]
    public void ExpiresAt_LeOExp() =>
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792000000), JwtPayload.ExpiresAt(TestJwt.Create(new { exp = 1792000000 })));

    [Fact]
    public void ExpiresAt_SemExp_DevolveNulo() =>
        Assert.Null(JwtPayload.ExpiresAt(TestJwt.Create(new { sub = "x" })));
}
