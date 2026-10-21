namespace AiUsageWidget.Constants;

/// <summary>
/// Endereços e parâmetros do conector do Codex (conta do ChatGPT). Nenhum deles é documentado para
/// terceiros: são os mesmos que o Codex CLI usa (conferidos no binário do Codex 0.128), então podem mudar sem aviso.
/// </summary>
public static class CodexEndpoints
{
    /// <summary>Limites de uso do Codex (sessão de 5 horas e semanal, conforme o plano).</summary>
    public const string Usage = "https://chatgpt.com/backend-api/wham/usage";

    public const string Authorize = "https://auth.openai.com/oauth/authorize";

    public const string Token = "https://auth.openai.com/oauth/token";

    /// <summary>Identificador público do Codex CLI no OAuth (não há registro de app para terceiros).</summary>
    public const string ClientId = "app_EMoamEEZ73f0CkXaXp7hrann";

    /// <summary>Permissões pedidas no login (as do Codex CLI, sem as de conectores).</summary>
    public const string Scopes = "openid profile email offline_access";

    /// <summary>Permissões pedidas ao renovar (como o Codex CLI faz).</summary>
    public const string RefreshScopes = "openid profile email";

    /// <summary>
    /// O retorno do login só é aceito neste endereço exato (<c>http://localhost:1455/auth/callback</c>),
    /// por isso a porta é fixa. Se o login do Codex CLI estiver aberto ao mesmo tempo, a porta fica ocupada.
    /// </summary>
    public const int CallbackPort = 1455;

    public const string CallbackPath = "/auth/callback";

    /// <summary>Cabeçalho com a conta (workspace) do ChatGPT, exigido na consulta de uso.</summary>
    public const string AccountIdHeader = "ChatGPT-Account-Id";

    /// <summary>Onde ficam os dados do ChatGPT dentro do id_token (JWT).</summary>
    public const string AuthClaim = "https://api.openai.com/auth";
}
