using System.Text;
using System.Text.Json;

namespace AiUsageWidget.Tests.Fakes;

/// <summary>Monta JWTs fictícios (assinatura falsa) para os testes que leem o conteúdo do token.</summary>
internal static class TestJwt
{
    public static string Create(object payload) =>
        $"{Encode(new { alg = "none" })}.{Encode(payload)}.assinatura-ficticia";

    private static string Encode(object value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
