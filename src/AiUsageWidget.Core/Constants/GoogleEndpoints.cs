namespace AiUsageWidget.Constants;

/// <summary>
/// Endereços do login Google e do servidor Code Assist, usados pelo conector do Antigravity. O Code
/// Assist não é documentado para terceiros (o mesmo que o Antigravity IDE e o Antigravity CLI usam),
/// então pode mudar sem aviso.
/// </summary>
public static class GoogleEndpoints
{
    public const string Authorize = "https://accounts.google.com/o/oauth2/v2/auth";

    public const string Token = "https://oauth2.googleapis.com/token";

    /// <summary>Base do Code Assist; cada operação é <c>{base}:{método}</c> (ex.: <c>:loadCodeAssist</c>).</summary>
    public const string CodeAssist = "https://cloudcode-pa.googleapis.com/v1internal";

    /// <summary>
    /// <c>User-Agent</c> do Antigravity. O Code Assist decide pelo <c>User-Agent</c> se o cliente é
    /// aceito: com outro, contas gratuitas recebem <c>UNSUPPORTED_CLIENT</c> e ficam sem projeto
    /// (conferido em outubro de 2026). Formato do Antigravity IDE 1.107; atualize se ele parar de ser aceito.
    /// </summary>
    public const string AntigravityUserAgent = "antigravity/1.107.0 windows/amd64";

    /// <summary>Permissões pedidas no login (as mesmas do Antigravity, sem as extras de telemetria).</summary>
    public const string Scopes =
        "https://www.googleapis.com/auth/cloud-platform https://www.googleapis.com/auth/userinfo.email https://www.googleapis.com/auth/userinfo.profile";
}
