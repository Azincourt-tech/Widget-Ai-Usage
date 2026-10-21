using System.Reflection;

namespace AiUsageWidget.Services.Google;

/// <summary>
/// Um app OAuth do Google (client público de app instalado; o "secret" vai dentro do app, por desenho do
/// Google). O secret não fica no repositório: entra no build pelo <c>Directory.Build.local.props</c> ou
/// pela variável de ambiente de mesmo nome (ver <c>Directory.Build.local.props.example</c>).
/// </summary>
/// <param name="ClientSecret">Vazio quando o build não recebeu o secret.</param>
/// <param name="CallbackPath">Caminho do retorno do login (a porta é livre).</param>
/// <param name="SuccessPage">Página mostrada no navegador depois do login.</param>
public sealed record GoogleOAuthApp(string ClientId, string ClientSecret, string CallbackPath, string SuccessPage)
{
    /// <summary>Nome da propriedade do build (e da variável de ambiente) com o secret do Antigravity.</summary>
    public const string AntigravitySecretKey = "AntigravityClientSecret";

    /// <summary>
    /// App do Antigravity (conferido no Antigravity IDE instalado; o Antigravity CLI usa o mesmo).
    /// </summary>
    public static GoogleOAuthApp Antigravity { get; } = new(
        "1071006060591-tmhssin2h21lcre235vtolojh4g403ep.apps.googleusercontent.com",
        BuildSecret(AntigravitySecretKey),
        "/oauth-callback",
        "https://antigravity.google/auth-success?app=Antigravity");

    /// <summary>Sem o secret o Google recusa o login e a renovação.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>Secret gravado pelo build como <see cref="AssemblyMetadataAttribute"/>; vazio se não veio.</summary>
    internal static string BuildSecret(string key) =>
        typeof(GoogleOAuthApp).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value ?? "";
}
