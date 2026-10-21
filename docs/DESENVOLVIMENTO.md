# Guia técnico

Como compilar, testar, entender e lançar o AI Usage Widget. Para usar o widget, veja o [README](../README.md). O que está planejado fica no [ROADMAP](ROADMAP.md).

## Requisitos

- **Windows 10 (2004 ou mais novo) ou Windows 11.** No Linux, por enquanto, só o modo demonstração (`--demo`).
- **SDK do .NET 10.**
- Opcional: o `client_secret` do Antigravity (ver [O secret do Antigravity](#o-secret-do-antigravity)).

## Tecnologias

- **Interface:** [Avalonia](https://avaloniaui.net/) 12, com compiled bindings.
- **Arquitetura:** MVVM com [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/).
- **Plataforma:** .NET 10. No Windows, a interface compila com o alvo `net10.0-windows10.0.19041.0` (para as notificações nativas, pelo pacote `Microsoft.Toolkit.Uwp.Notifications`); no Linux, com `net10.0`.
- **Testes:** xUnit.
- **Fonte:** JetBrains Mono (licença OFL), embutida.

## Estrutura

| Pasta | O que tem |
|---|---|
| `src/AiUsageWidget.Core` | Modelos, ViewModels, provedores de uso, login (OAuth) e configurações. `net10.0` puro: sem Avalonia e sem API exclusiva de um sistema |
| `src/AiUsageWidget` | Interface Avalonia: janelas, temas (`Themes/`), estilos, o controle do anel (`Controls/UsageRing.cs`) e o que depende do sistema (`Platform/`) |
| `tests/AiUsageWidget.Tests` | Testes do Core. Os falsos ficam em `Fakes/Fakes.cs` |
| `docs/` | Documentação. As imagens ficam em `docs/images/` |
| `.github/workflows/` | CI e release |

## Compilar e rodar

```bash
dotnet build AiUsageWidget.sln
dotnet test AiUsageWidget.sln
dotnet run --project src/AiUsageWidget
```

O build trata avisos como erro, e todo build precisa terminar com 0 avisos.

**Modo demonstração** (contas fictícias, sem login, bom para testar a interface e tirar prints):

```bash
dotnet run --project src/AiUsageWidget -- --demo --theme=Dark --size=Medium
```

`--theme`: `Glass`, `Dark` ou `Light`. `--size`: `Small`, `Medium` ou `Large`.

O widget abre uma cópia por vez (o modo demo tem a sua própria). Antes de recompilar, feche o widget aberto, senão o `.exe` fica travado. No Windows, o `.exe` de desenvolvimento fica em `src/AiUsageWidget/bin/Debug/net10.0-windows10.0.19041.0/`.

## O secret do Antigravity

O login do Google exige o `client_secret` do app OAuth do Antigravity. É um "secret" de app instalado (vai dentro do próprio Antigravity), mas mesmo assim **não fica no repositório**.

1. Copie `Directory.Build.local.props.example` para `Directory.Build.local.props`, na raiz. Esse arquivo é ignorado pelo git.
2. Preencha `<AntigravityClientSecret>` com o valor que está no Antigravity IDE instalado, em `resources\app\out\main.js` (procure por `GOCSPX`).

Também dá para usar a variável de ambiente `AntigravityClientSecret`, que é o que o GitHub Actions faz.

**Como o valor chega ao programa:** o `Directory.Build.props` importa o arquivo local; o `AiUsageWidget.Core.csproj` grava a propriedade como `AssemblyMetadata`; e `GoogleOAuthApp.BuildSecret` a lê ao rodar. Sem o valor, o widget compila e funciona com Claude e Codex, e o card do Antigravity mostra "Antigravity não configurado nesta versão do widget.". Os testes usam um secret fictício e não dependem do arquivo.

## Como funciona

### Provedores

Cada conta é um `IUsageProvider` que devolve um `ProviderUsage`: o plano e uma lista de `UsageWindow` (tipo de janela, % usado, quando renova e um rótulo opcional). Os provedores rodam em paralelo; um lento não segura os outros. Problemas esperados (sem login, token vencido, sem rede, 429) viram `UsageProviderException` com uma mensagem curta, que aparece no card mantendo os últimos limites lidos. Qualquer outra exceção vai para o log e mostra uma mensagem genérica.

| Conta | Login | Consulta de uso | Plano |
|---|---|---|---|
| Claude | OAuth da Anthropic (client público do Claude Code, PKCE) | `api.anthropic.com/api/oauth/usage`: sessão de 5 h, semanal e, quando o plano tem, semanais do Opus e do Sonnet | `api/oauth/profile` |
| Codex | OAuth da OpenAI (client público do Codex CLI, PKCE, retorno fixo em `localhost:1455`) | `chatgpt.com/backend-api/wham/usage` | Do `id_token` |
| Antigravity | OAuth do Google (app instalado do Antigravity, PKCE + secret) | `cloudcode-pa.googleapis.com/v1internal:retrieveUserQuotaSummary`, um anel por grupo de modelos | `loadCodeAssist` |

Os endereços **não são documentados**: foram conferidos nos binários dos aplicativos oficiais. Ficam isolados em `Constants/<Provedor>Endpoints.cs`, e a leitura de cada resposta é um método estático testável (`ParseXxx`). O servidor do Antigravity aceita ou recusa o cliente pelo `User-Agent` (`GoogleEndpoints.AntigravityUserAgent`); se ele parar de responder, atualize a versão ali.

### Login e cofre

- `OAuthAccount<T>` faz o fluxo comum: abre o navegador, recebe o retorno num servidor HTTP mínimo só em `127.0.0.1`/`::1` (`LoopbackCallbackListener`), troca o código e renova o token 2 minutos antes de vencer ou depois de um 401. As renovações passam por um `SemaphoreSlim`, porque o refresh token pode mudar a cada renovação.
- Os tokens ficam no **Gerenciador de Credenciais do Windows** (`WindowsCredentialVault`), um item por conta: `AiUsageWidget/claude`, `AiUsageWidget/codex` e `AiUsageWidget/antigravity`.
- O widget nunca pede senha nem raspa páginas, e token nunca vai para o log nem para o `config.json`.

### Atualização

- A cada **1 minuto**, em silêncio (sem esmaecer a tela).
- Depois de **20 leituras seguidas sem mudança**, a conta pausa por **2 minutos**.
- Uma resposta **429** pausa a conta por **5 minutos**.
- "Atualizar agora" ignora as pausas.

Os valores ficam em `WidgetViewModel` e são travados pelo teste `Constantes_DecididasPeloUsuario`.

### Avisos de 80% e 95%

`UsageAlertTracker` decide quando avisar: só ao cruzar 80% ou 95% com o widget aberto. A primeira leitura de cada limite é só a base; se pular os dois de uma vez, avisa só o de 95%; depois que o limite renova, avisa de novo. No Windows, `WindowsNotificationService` mostra a notificação nativa; fora dele, `NoNotificationService` não mostra nada.

### Interface

- Uma linha por conta, com os anéis em colunas iguais para todas. Se alguma conta visível tiver mais de dois anéis, todas ganham colunas e o widget cresce para o lado (`WidgetViewModel.RingColumns` e `MainWindow.PanelWidthFor`).
- Os temas trocam em tempo real. Por isso as cores são sempre `{DynamicResource ...}`, e o Core passa cores como `#RRGGBB`.
- O ícone (`src/AiUsageWidget/Assets/icon.ico`, de 16 a 256 px) é usado no `.exe`, nas janelas e nas notificações.

## Dados no PC do usuário

| O quê | Onde |
|---|---|
| Configurações | `%APPDATA%\AiUsageWidget\config.json` |
| Logins | Gerenciador de Credenciais do Windows, itens `AiUsageWidget/...` |
| Log de erros | `%LOCALAPPDATA%\AiUsageWidget\logs\error.log` |
| Iniciar com o Windows | Chave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| Registro das notificações | `HKCU\Software\Classes\AppUserModelId\<caminho do exe>` (criado pelo pacote de notificações) |

## Git e lançamentos

- **Branches:** o trabalho vai na `develop`; a `main` só recebe o que for lançado.
- **Nunca suba dados sensíveis:** secrets, tokens, chaves ou e-mails, mesmo os "públicos". Antes de commitar, procure por `GOCSPX`, `secret`, `token`, `eyJ` e e-mails nos arquivos.

### CI (`.github/workflows/ci.yml`)

Em todo push na `develop` e em pull requests: compila e roda os testes no Windows, sem o secret.

### Release (`.github/workflows/release.yml`)

Em todo push na `main` (ou à mão, pela aba Actions):

1. Lê a `<Version>` do `Directory.Build.props`. **Para** se a release `v<versão>` já existir.
2. **Para** se o secret `ANTIGRAVITY_CLIENT_SECRET` não estiver cadastrado no environment `release` (Settings > Environments > release, com "Deployment branches" limitado à `main`, para nenhum workflow de outra branch conseguir usar o secret).
3. Roda os testes.
4. Gera o `.exe` único e autossuficiente (cerca de 58 MB, não precisa do .NET instalado):

   ```bash
   dotnet publish src/AiUsageWidget -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
   ```

5. Cria a release com o arquivo `AiUsageWidget-v<versão>-win-x64.exe` e notas geradas pelo GitHub.

**Para lançar uma versão:** suba a `<Version>` no `Directory.Build.props` (ex.: `0.2.0`) e envie para a `main`.

Sem trimming, porque o Avalonia e o System.Text.Json usam reflexão. O `.exe` não é assinado, então o SmartScreen avisa na primeira execução.

### Dependências e segurança

- **Dependabot** (`.github/dependabot.yml`): toda segunda-feira abre PRs para a `develop` com versões novas dos pacotes NuGet (os do Avalonia juntos num PR) e das actions do GitHub. As correções de segurança são ligadas em Settings > Advanced Security.
- **Falhas de segurança:** reportadas pelo canal privado do GitHub, como explica o [SECURITY.md](../SECURITY.md).
- **Issues:** só pelos formulários em `.github/ISSUE_TEMPLATE/` ("Reportar um problema" e "Sugerir uma ideia"), que pedem para não colar tokens nem dados das contas. A issue em branco está desligada, e o `config.yml` aponta falhas de segurança para o reporte privado.
