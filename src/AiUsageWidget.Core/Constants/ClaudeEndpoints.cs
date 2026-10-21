namespace AiUsageWidget.Constants;

/// <summary>
/// Endereços e parâmetros do conector do Claude. Nenhum deles é documentado para terceiros: são os
/// mesmos que o Claude Code usa (conferidos no binário do Claude Code 2.1.299), então podem mudar sem aviso.
/// </summary>
public static class ClaudeEndpoints
{
    /// <summary>Limites de uso (o mesmo do comando <c>/usage</c> do Claude Code).</summary>
    public const string Usage = "https://api.anthropic.com/api/oauth/usage";

    /// <summary>Perfil da conta: o plano vem em <c>organization.organization_type</c>.</summary>
    public const string Profile = "https://api.anthropic.com/api/oauth/profile";

    /// <summary>Página de login para contas do claude.ai (Pro, Max, Team...).</summary>
    public const string Authorize = "https://claude.com/cai/oauth/authorize";

    /// <summary>Troca do código de login por tokens e renovação do token.</summary>
    public const string Token = "https://platform.claude.com/v1/oauth/token";

    /// <summary>Página mostrada no navegador depois do login.</summary>
    public const string LoginSuccessPage = "https://platform.claude.com/oauth/code/success?app=claude-code";

    /// <summary>Identificador público do Claude Code no OAuth (não há registro de app para terceiros).</summary>
    public const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    /// <summary>Permissões pedidas no login: só o perfil e o necessário para consultar os limites.</summary>
    public const string Scopes = "user:profile user:inference";

    /// <summary>Cabeçalho exigido pela API para aceitar o token de login (OAuth).</summary>
    public const string OAuthBetaHeader = "oauth-2025-04-20";
}
