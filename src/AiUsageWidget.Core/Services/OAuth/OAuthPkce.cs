using System.Security.Cryptography;
using System.Text;

namespace AiUsageWidget.Services.OAuth;

/// <summary>
/// Par PKCE (RFC 7636): o <see cref="Verifier"/> fica guardado; o <see cref="Challenge"/> vai no endereço de login.
/// </summary>
public sealed record OAuthPkce(string Verifier, string Challenge)
{
    public static OAuthPkce Create()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return new OAuthPkce(verifier, ChallengeFor(verifier));
    }

    public static string ChallengeFor(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>Valor aleatório que volta no retorno do login; protege contra respostas forjadas.</summary>
    public static string CreateState() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Monta a query string, com cada valor escapado.</summary>
    public static string Query(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join('&', parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
}
