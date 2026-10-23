# AI Usage Widget

Widget de área de trabalho que mostra os limites de uso das IAs conectadas (Claude, ChatGPT/Codex, Antigravity): sessão de 5 horas, diário e semanal, com anéis no estilo do Apple Watch. Interface em **Avalonia 12** sobre **.NET 10**, arquitetura **MVVM** (CommunityToolkit.Mvvm). Sistemas suportados: **Windows** (hoje) e **Linux** (próximo). **macOS não será suportado** (decisão do usuário, outubro de 2026: ele não tem como testar). O visual continua inspirado no macOS/iOS; só o suporte ao sistema ficou de fora.

Segue o mesmo padrão do projeto irmão `C:\Dev\SteamWishlistWidget` (Core separado, temas Vidro/Escuro/Claro, `--demo`). Na dúvida sobre uma solução visual, veja como foi feito lá. Ideias planejadas: [docs/ROADMAP.md](docs/ROADMAP.md).

**Documentação (pedido do usuário):** o [README](README.md) é só para o **usuário final** (o que é, como baixar, como usar, perguntas frequentes, privacidade, como remover), sem nada de código. O que é técnico (compilar, secret, arquitetura, dados no PC, CI e release) fica em [docs/DESENVOLVIMENTO.md](docs/DESENVOLVIMENTO.md). Ao mudar um comportamento, atualize os dois quando couber.

## Como trabalhar com o usuário

- **Converse sempre em português (Brasil).** Comentários no código, textos da interface e documentação também em português.
- O usuário é desenvolvedor .NET e decide o rumo do projeto. Antes de mudanças grandes (nova dependência, mudar arquitetura, novo jeito de ler os limites), explique as opções e espere a decisão.
- **Nunca use dados reais do usuário** (tokens, e-mail, percentuais reais das contas) em testes, exemplos, documentação ou prints. Prints sempre no modo demonstração (`--demo`).
- Ao testar a interface, prefira automação (UI Automation pelo PowerShell) a simular cliques do mouse: o usuário costuma estar usando o PC.
- Ao final de uma mudança visual, confira de verdade na tela (capturar a janela e olhar) nos três temas e, se mexer no layout, nos três tamanhos. Como capturar:
  - **O handle da janela vem da UI Automation** (`NativeWindowHandle`): `Process.MainWindowHandle` vem **zerado**, porque o widget não aparece na barra de tarefas.
  - **Para layout e cores, use `PrintWindow(hwnd, hdc, 2)`:** desenha a janela mesmo atrás de outras e não mexe no mouse. As áreas transparentes saem pretas e o desfoque do Vidro não aparece.
  - **Imagens do README e o ícone ficam em `docs/images/`** (só imagens nessa pasta; documentos ficam em `docs/`).
  - **Para o Vidro de verdade (imagens do README):** `SetForegroundWindow` com esse handle e depois `CopyFromScreen`.
  - **Configurações:** abrem pelo botão "Configurações" via UI Automation (`InvokePattern`) e aparecem como **filha** da janela do widget na árvore (busque nos filhos dela, não na raiz).
  - **Botões do widget:** só aparecem com o mouse em cima. Evite mover o cursor; se for inevitável, devolva-o à posição original.
  - **Modo bandeja:** abra a janelinha invocando o ícone pela UI Automation (`NotifyItemIcon`, nome = a dica "Claude: ..."; no Windows 11 ele fica na área de ícones ocultos, botão `SystemTrayIcon` "Mostrar ícones ocultos"). Ela abre junto ao cursor do usuário e fecha ao perder o foco: capture **sem** `SetForegroundWindow`.
  - **Monitor com escala (o segundo monitor do usuário é 175%):** o PowerShell não é ciente do DPI e recebe coordenadas virtualizadas (captura cortada ou do lugar errado). Capture com um processo ciente do DPI (ex.: um `.cs` rodado com `dotnet run` que chama `SetProcessDpiAwarenessContext(-4)` antes de tudo).
- Se precisar conferir algo com a conta real do usuário, apague a captura logo depois (ela tem os percentuais reais).

## Comandos

```bash
dotnet build AiUsageWidget.sln            # precisa terminar com 0 avisos (avisos são erro)
dotnet test AiUsageWidget.sln             # rodam em qualquer sistema
dotnet run --project src/AiUsageWidget -- --demo --theme=Dark --size=Medium   # contas fictícias (Dark|Light|Glass, Small|Medium|Large; --mode=Widget|Tray)
```

No Windows, a interface compila com o alvo `net10.0-windows10.0.19041.0` (por causa das notificações nativas); no Linux, com `net10.0`. O `.exe` fica em `src/AiUsageWidget/bin/Debug/net10.0-windows10.0.19041.0/`. O Core e os testes continuam em `net10.0`.

O widget só abre uma cópia por vez (mutex). Antes de recompilar, feche o widget aberto (`Stop-Process -Name AiUsageWidget`), senão o `.exe` fica travado. O modo `--demo` tem mutex próprio.

## Estrutura

| Pasta | O que é | Regras |
|---|---|---|
| `src/AiUsageWidget.Core` | Models, ViewModels, Services (provedores), Constants. `net10.0` puro | Nada de Avalonia nem API exclusiva de um sistema |
| `src/AiUsageWidget` | Interface Avalonia, temas, estilos, `Controls/UsageRing.cs`, `Tray/` (ícone da bandeja) e `Platform/` | Só comportamento visual nas Views |
| `tests/AiUsageWidget.Tests` | xUnit, referencia só o Core | Fakes em `Fakes/Fakes.cs` (relógio fixo em UTC-3) |

## Como os limites são lidos

- Cada provedor implementa `IUsageProvider` (`Services/Abstractions`) e devolve `ProviderUsage` (plano + lista de `UsageWindow`: tipo, % usado, quando renova).
- Problema esperado (sem login, token expirado, sem rede) vira `UsageProviderException` com mensagem curta para o usuário; aparece no card e os últimos limites lidos continuam visíveis. Qualquer outra exceção vai para o `ErrorLog` e mostra mensagem genérica.
- Os provedores rodam em paralelo; um lento não segura os outros.
- **Atualização (decisão do usuário):** a cada **1 minuto**, fixo (não há opção nas configurações). As leituras automáticas são silenciosas (sem esmaecer nem "Atualizando..."). Para não estourar limites de APIs não documentadas (o limite real é desconhecido): depois de **20 leituras seguidas sem nenhuma mudança** (uns 20 minutos), a conta pausa por **2 minutos**; resposta 429 (`UsageProviderException.RateLimited()`) pausa a conta por **5 minutos**, mantendo os últimos limites. "Atualizar agora" (menu do clique direito) ignora as pausas. Constantes em `WidgetViewModel` (`RefreshInterval`, `UnchangedReadsBeforePause`, `IdlePause`, `RateLimitPause`); a contagem fica em `ProviderCardViewModel.CountRead`. O teste `Constantes_DecididasPeloUsuario` trava esses valores: só mude com pedido do usuário.
- **Avisos de 80% e 95% (decisão do usuário):** notificação nativa do Windows, **só ao cruzar** com o widget aberto (abrir já acima de 80% não avisa: a primeira leitura de cada limite é só a base). Se pulou os dois de uma vez, avisa só o de 95%. Quando o limite renova e volta a subir, avisa de novo. A regra fica em `UsageAlertTracker` (Core, testado); o `WidgetViewModel` chama depois de cada leitura e só mostra se `AppSettings.Notifications` estiver ligado (padrão: ligado; interruptor "Avisar em 80% e 95%" nas configurações). Sair da conta (`RequiresSignIn`) zera a base daquela conta. Texto: "Claude: Semanal em 82%" / "Passou de 80% do limite; renova sex. 21:22." (95%: "Quase no fim: passou de 95%...").
  - **Windows:** `Platform/Windows/WindowsNotificationService.cs` (dentro de `#if WINDOWS`) com o pacote `Microsoft.Toolkit.Uwp.Notifications` 7.1.3, que na primeira notificação registra o widget no registro do usuário (`HKCU\Software\Classes\AppUserModelId\<caminho do exe>`; o nome mostrado vem do `AssemblyTitle`). Esse pacote puxa o `System.Drawing.Common` 4.7.0, que tem falha de segurança conhecida: o csproj fixa a 10.0.0 (sem isso o build falha com NU1904).
  - **Fora do Windows:** `NoNotificationService` (sem aviso) até o Linux ganhar o dele.
  - **Para conferir:** o histórico de notificações pode ser lido pelo Windows PowerShell 5.1 com `[Windows.UI.Notifications.ToastNotificationManager]::History.GetHistory(<AUMID>)` e limpo com `.History.Clear(<AUMID>)`. Para provocar um aviso no `--demo`, mude temporariamente o `DemoUsageProvider` para subir um limite na segunda leitura (a leitura automática vem depois de 1 minuto) e desfaça depois.
- `App.axaml.cs` monta a lista de provedores. Hoje, fora do `--demo`: Claude, Codex e Antigravity (nessa ordem).
- `UsageWindow` pode ter rótulo próprio (`Label`/`ShortLabel`, ex.: "Pro", "Flash", nome do grupo do Antigravity); sem ele, vale o do tipo ("Sessão (5h)", "5h").

### Login próprio (`Services/OAuth/`), comum a todos os conectores

Cada conta tem **login próprio do widget** (decisão do usuário, outubro de 2026), em vez de ler os arquivos dos CLIs oficiais: esses arquivos só são renovados quando o CLI roda.

- **`OAuthAccount<T>`** (base): OAuth com PKCE no navegador, tempo limite de 5 minutos, renovação automática 2 minutos antes de vencer e depois de um 401. As renovações passam por um `SemaphoreSlim`, porque o refresh token pode ser trocado a cada renovação. Refresh recusado (400/401) apaga o login e pede para entrar de novo. Cada provedor diz como montar o endereço de login, trocar o código e renovar, e pode fixar porta, caminho e página de sucesso do retorno.
- **`LoopbackCallbackListener`**: servidor HTTP mínimo só em 127.0.0.1 e ::1. Porta livre por padrão, ou fixa quando o provedor exige. Sem página de sucesso, responde "Login concluído. Pode fechar esta aba...".
- **`VaultCredentialStore<T>`**: guarda o login como JSON no **Gerenciador de Credenciais do Windows** (`WindowsCredentialVault`), uma chave por provedor (`AiUsageWidget/claude`, `AiUsageWidget/codex`).
- **`JwtPayload`**: lê o conteúdo de um JWT sem validar a assinatura, só para dados que o próprio servidor de login devolveu (validade, conta).
- O card mostra "Entrar com o X", "Cancelar" e, ao passar o mouse, o ícone de sair.
- **Os `client_id` são os públicos dos CLIs oficiais** (não há registro de app OAuth para terceiros). Os endereços não são documentados: se um login parar de funcionar, confira de novo no binário do CLI.
- **Regras para qualquer conector:** nunca pedir senha nem raspar o site. Endpoints ficam em `Constants/<Provedor>Endpoints.cs`, isolados no adaptador, com a leitura da resposta em método **estático** testável (`ParseXxx`). Nunca gravar token no log nem no `config.json`; `ToString()` de modelos com token não mostra o token.

### Conector do Claude (`Services/Claude/`)

- **Endereços (`ClaudeEndpoints`):** autorização `https://claude.com/cai/oauth/authorize`, token `https://platform.claude.com/v1/oauth/token`, uso `https://api.anthropic.com/api/oauth/usage` (com `anthropic-beta: oauth-2025-04-20`). Conferidos no binário do Claude Code 2.1.299 (`%APPDATA%\Claude\claude-code\<versão>\claude.exe`, busque por `oauth/token`). Escopos: `user:profile user:inference`. Retorno em porta livre, depois a página de sucesso da Anthropic.
- **Consulta:** `five_hour` e `seven_day`, cada um com `utilization` (0 a 100) e `resets_at` (ISO). Os semanais por modelo `seven_day_opus` e `seven_day_sonnet` viram anéis próprios ("Opus semanal"/"Opus", "Sonnet semanal"/"Sonnet") **só quando vêm preenchidos**: na conta Team do usuário (20/10/2026) vêm nulos. Os outros campos (`seven_day_oauth_apps`, `extra_usage`, `spend`, `limits`, codinomes) são ignorados; `limits` repete sessão e semanal.
- **Plano:** `GET https://api.anthropic.com/api/oauth/profile` (sem o cabeçalho beta), campo `organization.organization_type`: `claude_max`/`claude_pro`/`claude_team`/`claude_enterprise` (o mesmo mapa do Claude Code). No Max, `rate_limit_tier` terminado em `_20x`/`_5x` vira "Max 20x"/"Max 5x". Lido uma vez por login; se falhar, o card segue sem a etiqueta.
- **Testado de ponta a ponta com a conta real do usuário** (20/10/2026): login, cofre, consulta e plano funcionando.

### Conector do Codex (`Services/Codex/`)

- **Endereços (`CodexEndpoints`):** autorização `https://auth.openai.com/oauth/authorize`, token `https://auth.openai.com/oauth/token`, uso `https://chatgpt.com/backend-api/wham/usage`. Conferidos no binário do Codex CLI 0.128 (`%APPDATA%\npm\node_modules\@openai\codex\...\codex.exe`, busque por `wham/usage`). Escopos: `openid profile email offline_access`.
- **Retorno em porta fixa:** só `http://localhost:1455/auth/callback` é aceito. Se o login do Codex CLI estiver aberto ao mesmo tempo, a porta fica ocupada e o card explica isso.
- **Troca do código:** formulário (`x-www-form-urlencoded`). **Renovação:** JSON, com `scope: openid profile email` (como o Codex CLI faz).
- **Conta e plano** vêm do `id_token`, no claim `https://api.openai.com/auth` (`chatgpt_account_id`, `chatgpt_plan_type`). A conta vai no cabeçalho `ChatGPT-Account-Id` da consulta.
- **Consulta:** `{"plan_type", "rate_limit": {"primary_window", "secondary_window"}}`, cada janela com `used_percent`, `limit_window_seconds` e `reset_at` (segundos desde 1970) ou `reset_after_seconds`. O tipo vem da duração: até 6 h é sessão, até 36 h é diário, acima é semanal. O plano grátis tem só a semanal.
- **Testado de ponta a ponta com a conta real do usuário** (20/10/2026).

### Conector do Antigravity (`Services/Google/`)

O Antigravity (IDE e CLI `agy`) usa login Google e **uma cota só, por conta**: o Antigravity CLI usa o mesmo client OAuth e a mesma consulta de cota do IDE (conferido no `%LOCALAPPDATA%\agy\bin\agy.exe`), então o card "Antigravity" cobre os dois. Id `ProviderIds.Antigravity`, login no cofre em `AiUsageWidget/antigravity`.

- **Gemini CLI foi removido** (outubro de 2026, pedido do usuário): o Google substituiu o Gemini CLI pelo Antigravity CLI. Nas contas gratuitas/Plus o Code Assist recusa o Gemini CLI (`free-tier: UNSUPPORTED_CLIENT`, sem projeto; o nível gratuito agora se chama "Antigravity"). Não traga de volta sem pedido.
- **Login (`GoogleAccount` + `GoogleOAuthClient`):** OAuth do Google para app instalado, com PKCE, `access_type=offline` e `prompt=consent` (sem isso o Google não manda refresh token). Escopos: `cloud-platform`, `userinfo.email`, `userinfo.profile`. Troca e renovação por formulário, com `client_secret` (o "secret" de app instalado vem no próprio app; não é segredo). Na renovação o Google em geral não manda refresh token novo: mantém o anterior.
- **App (`GoogleOAuthApp.Antigravity`):** client `1071006060591-…`, retorno `/oauth-callback`. Conferido em `resources\app\out\main.js` do Antigravity IDE. Há um segundo client, `884354919052-…`, só para contas com termos do GCP; não usamos.
- **O `client_secret` não fica no código (pedido do usuário):** vem do `Directory.Build.local.props` (ignorado pelo git; modelo versionado em `Directory.Build.local.props.example`) ou da variável de ambiente `AntigravityClientSecret` (para o GitHub Actions). O `AiUsageWidget.Core.csproj` grava o valor como `AssemblyMetadata`, e `GoogleOAuthApp.BuildSecret` o lê. Sem o secret, o widget compila e o card mostra "Antigravity não configurado nesta versão do widget." (sem botão "Entrar"). Os testes usam um secret fictício e não dependem do arquivo local. Claude e Codex não têm secret: só `client_id` público com PKCE.
- **Servidor Code Assist** (`https://cloudcode-pa.googleapis.com/v1internal:{método}`, POST com JSON):
  - **O servidor aceita ou recusa o cliente pelo `User-Agent`** (descoberto na prática em 20/10/2026). O widget manda `GoogleEndpoints.AntigravityUserAgent` = `antigravity/1.107.0 windows/amd64`, sem validação de cabeçalho. Com outro `User-Agent`, conta gratuita recebe `free-tier: UNSUPPORTED_CLIENT` e fica sem projeto. O `metadata.ideType` **não** faz diferença. Se parar de responder, atualize a versão no `User-Agent`.
  - `loadCodeAssist`: devolve o projeto da conta (`cloudaicompanionProject`, texto ou `{id}`), o plano (`paidTier`/`currentTier` → "Ultra", "Pro", "Plus", "Free", "Standard") e, sem projeto, `ineligibleTiers[].reasonCode`. Lido uma vez por login. Sem projeto: `UNSUPPORTED_CLIENT` vira "O Google não libera o Antigravity para esta conta"; outro motivo, "Esta conta ainda não tem o Antigravity liberado pelo Google". **Não depende de ter o app instalado nem de abri-lo.**
  - `retrieveUserQuotaSummary` `{project}`: `{groups[{displayName, description, buckets}], buckets, description}`, cada bucket com `bucketId, displayName, description, window, remainingFraction, remainingAmount, disabled, resetTime`. Campos tirados do protobuf embutido em `extensions\antigravity\bin\language_server_windows_x64.exe`. Um anel por grupo (o bucket mais usado; desligados ignorados), no máximo 4. O tipo da janela vem do texto de `window` ou, se não disser, de quanto falta para renovar. **Resposta real** (20/10/2026): dois grupos, "Gemini Models" e "Claude and GPT models", cada um com um bucket "Weekly Limit Remaining", `window: "weekly"`; `GroupLabel` encurta para "Gemini" e "Claude e GPT". No tamanho pequeno, `ShortLabelFor` dá "Gemini" e "C+GPT" (iniciais + último nome, para não parecer a conta do Claude).
- **`AntigravityUsageProvider`** faz tudo: token, projeto, `User-Agent`, renovação no 401 e erros (403, 429 → pausa).
- **Testado de ponta a ponta com a conta real do usuário** (20/10/2026, plano Google AI Plus).
- Os scripts de diagnóstico que levaram a essas conclusões leem o login do widget no cofre e chamam `loadCodeAssist`/cotas mostrando só a estrutura (nunca token, e-mail ou percentuais).

## Interface

- **Visual minimalista, mais largo que alto (pedido do usuário):** sem título e sem cartões. **Uma linha por conta**: à esquerda ponto colorido, nome e etiqueta de plano (só contorno); à direita os anéis, com o mesmo número de colunas em todas as contas para ficarem alinhados. **Colunas (pedido do usuário):** no mínimo 2; se alguma conta visível tiver mais anéis (ex.: Opus semanal), todas ganham colunas e o widget **cresce para o lado**, nunca uma segunda linha. Vale a conta visível com mais anéis (escondida não conta). `WidgetViewModel.RingColumns` (repassado a cada card) e `MainWindow.PanelWidthFor(size, columns)`, que soma `RingColumnWidthFor(size)` por coluna extra. Linhas finas separam as contas. Os botões (configurações, fixar, fechar) ficam numa coluna fina à direita e só aparecem ao passar o mouse (fixado: alfinete preenchido em `Accent` com fundo `AccentSoft`, classe `active` em `Button.icon`; solto: contorno inclinado 45°). **Esses botões não têm `ToolTip`** (pedido do usuário: a dica atrapalhava o clique), só `AutomationProperties.Name`; "Atualizar agora" e a hora da última leitura (`Subtitle`) ficam no menu do clique direito. Fonte **JetBrains Mono** embutida (`Assets/Fonts`, licença OFL), chave `UiFont` no `App.axaml`.
- **Ícone (escolha do usuário: o "meio-termo"):** anel de uso laranja→roxo com uma carinha sorrindo (olhos fechados e sorriso em ciano) num visor escuro, inspirado no logo do robô que o usuário trouxe, mas simples para funcionar em 16 px. Arquivos prontos (o script que os gerou foi removido a pedido do usuário): `src/AiUsageWidget/Assets/icon.ico` (16 a 256 px, PNG dentro do .ico) e `docs/images/icon.png` (256 px, README). Usado como `ApplicationIcon` do `.exe` (é dele que as notificações tiram o ícone) e `Icon` das janelas. O registro de notificações só atualiza nome e ícone na próxima notificação.
- **Tamanhos (`WidgetSize`):** a largura do painel fica no código (`MainWindow.PanelWidthFor`: 330/500/600), porque o posicionamento inicial precisa dela antes dos estilos serem aplicados. Anéis, fontes e a largura da coluna da esquerda ficam nos estilos do `MainWindow.axaml`, pelas classes `small`/`large` no `PanelView` (médio é o padrão). No pequeno, só o rótulo curto ("5h", "7d"); quando renova fica na dica do anel. Sem login, o botão diz só "Entrar" (o nome da conta já está à esquerda), e no pequeno a mensagem some, ficando só o botão. Nomes longos ("Antigravity") são cortados com "..." na coluna da esquerda. Ao mudar de tamanho do lado direito da tela, a borda direita fica parada.
- **Configurações (`SettingsWindow` + `SettingsViewModel`):** tema (prévia ao vivo; quem abriu reaplica o tema salvo ao fechar), tamanho, contas visíveis (pelo menos uma), avisar em 80% e 95% e iniciar com o Windows (chave `Run` do usuário, `WindowsStartupRegistration`). Contas escondidas não são consultadas. A janela arrasta por qualquer parte (borda `Root`), menos sobre `Button`/`ToggleButton` (botões, interruptores e opções do segmentado).
- **Desenho por software (decisão do usuário, outubro de 2026):** o `Program.cs` usa `Win32RenderingMode.Software`. Com a GPU (padrão do Avalonia) o widget gastava ~20 MB de RAM e ~70 threads a mais, sem ganho visual. Não volte para a GPU sem medir (números e como medir em [docs/DESENVOLVIMENTO.md](docs/DESENVOLVIMENTO.md#consumo-de-memória)).
  - **Cuidado:** com software o Avalonia **não** dá o `AcrylicBlur` (fica `Transparent`, sem desfoque). O desfoque do Vidro vem do `Platform/Windows/WindowsBlur.cs` (accent policy do DWM), chamado pelo `Views/PanelChrome.cs`. Para conferir o Vidro, olhe `ActualTransparencyLevel`/a captura real: o fundo atrás tem de sair desfocado, não só transparente.
- **Widget ou bandeja (decisão do usuário, outubro de 2026: um ou outro, sem "os dois"):** "Mostrar como" nas configurações (`AppSettings.Mode`: `Widget` padrão ou `Tray`), vale na hora. O `App` cria o `WidgetViewModel` uma vez (leituras, relógio e avisos ficam nele, `ShutdownMode.OnExplicitShutdown`) e troca o `MainWindow` pelo `TrayController` e vice-versa; ao ir para a bandeja, o widget é fechado (libera memória). "Fechar" (widget) e "Sair" (menu do ícone) encerram o app; fechar o widget por fora (Alt+F4) também.
  - **Ícone (escolha do usuário):** o `icon.ico` do widget, não um desenho dinâmico. A dica (`TrayToolTip.For`) tem uma linha por conta visível com o maior uso ("Claude: 42%", "Codex: entrar", "—" sem leitura), até 127 caracteres. Menu: Atualizar agora, Configurações..., Sair.
  - **Janelinha (`TrayPanelWindow` + `TrayPanelViewModel`):** 360 de largura, abas por conta (ponto, nome e maior %; a escolhida continua ao salvar as configurações) e os limites da conta em barras (nome, %, barra na cor do anel, "renova em..."), rodapé com "Atualizado..." e os botões atualizar e configurações. Abre junto ao cursor (`TrayPanelPlacement`, no Core e testado), **posicionada só depois de mostrar** (escondida, a janela não se ajusta à escala de outro monitor). Fecha ao perder o foco (menos nos primeiros 300 ms), com Esc ou num novo clique no ícone (folga de 400 ms para o clique que a fechou não a reabrir).
  - As configurações abrem presas ao widget (`ShowDialog`) quando ele está na tela; no modo bandeja, sozinhas. `OpenSettings` ignora um segundo pedido enquanto a tela está aberta.
  - Atalho global para abrir a janelinha ficou para depois (ROADMAP).

## Regras do projeto

- **Privacidade:** dados só no PC do usuário. Sem servidor próprio.
- **Nada sensível no repositório (decisão do usuário):** `client_secret`, tokens, chaves, e-mails ou qualquer credencial não entram em arquivo versionado, mesmo os "secrets" públicos de app instalado. Antes de qualquer commit, procure por `GOCSPX`, `secret`, `token`, `eyJ` e e-mails nos arquivos que vão subir. Na dúvida, pergunte.
- Cores sempre com `{DynamicResource ...}` (o tema troca em tempo real). O Core passa cores como `#RRGGBB` e a interface converte com `HexToBrush`.
- Compiled bindings ligados: toda janela e todo `DataTemplate` precisam de `x:DataType`.
- Botões só com ícone precisam de `AutomationProperties.Name`.
- Toda mudança de comportamento vem com teste. Build com 0 avisos.
- Uma classe ou interface por arquivo (exceto `Fakes.cs`); namespaces por arquivo; campos privados `_camelCase`.

## Armadilhas herdadas do SteamWishlistWidget (não repita)

- **Troca de tema:** o tema não fica no `App.axaml`; o `Themes/ThemeManager.cs` adiciona o dicionário no fim de `MergedDictionaries` e guarda a referência para trocar.
- **Cantos do tema Vidro no Windows:** o desfoque ignora cantos desenhados pela interface. No Vidro, `WindowDecorations = BorderOnly` e o painel usa `WindowsCorners.SystemCornerRadius` (8 px); nos outros temas, `None` e cantos de 22 px.
- **Avalonia 12:** `SystemDecorations` virou `WindowDecorations`.
- **Captura de tela pelo PowerShell:** `Bitmap.Save` falha ("generic error in GDI+") em caminhos muito longos; salve em `$env:TEMP`.

## Estado atual (outubro de 2026)

- Pronto: modo demonstração, três temas, três tamanhos, tela de configurações, iniciar com o Windows, avisos de 80% e 95%, anéis animados, atualização periódica, fixar, arrastar e lembrar posição, e o modo bandeja (ícone com janelinha, no lugar do widget).
- Conectores do Claude, Codex e Antigravity, todos testados com conta real, com login próprio e cofre do Windows. O Gemini CLI foi removido (substituído pelo Antigravity CLI).
- **Windows primeiro, depois Linux.** O Linux é testado por um amigo do usuário: mudanças para Linux precisam de instruções claras de como testar (comandos, o que conferir). Falta no Linux: cofre com libsecret (`secret-tool`), abrir o navegador com `xdg-open` e conferir Vidro/cantos (ver ROADMAP). Hoje `PlatformServices` lança `PlatformNotSupportedException` fora do Windows (exceto no `--demo`).
- **Não escreva código específico de macOS** (Keychain, `open`, `.app`, etc.).
- **Sem Microsoft Store/MSIX (decisão do usuário, outubro de 2026):** a distribuição é só o `.exe` da release do GitHub. Não traga de volta sem pedido.
- **Git:** repositório `https://github.com/Azincourt-tech/Widget-Ai-Usage` (licença MIT). Trabalho na branch `develop`; a `main` só recebe o que for lançado. Nome e e-mail dos commits ficam **só neste repositório** (`.git/config`), porque o git global deste PC usa o e-mail do trabalho, que não pode ir para o histórico: confira com `git config --show-origin user.email` antes de commitar. Commit e push só quando o usuário pedir.
- **GitHub Actions:** `.github/workflows/ci.yml` (build e testes em push na `develop` e em PR) e `release.yml` (push na `main`: testes, `.exe` único autossuficiente de ~140 MB com `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`, **sem** `EnableCompressionInSingleFile` (decisão do usuário: a compressão custava ~25 MB de RAM), e release `v<Version>` com o arquivo `AiUsageWidget-v<Version>-win-x64.exe`). A versão vem da `<Version>` do `Directory.Build.props`; se a release já existe, o workflow falha (é preciso subir a versão). O secret vem de `secrets.ANTIGRAVITY_CLIENT_SECRET` do environment `release` (só a `main` pode usar) na variável `AntigravityClientSecret`; sem ele o workflow de release falha de propósito. Sem trimming (Avalonia e System.Text.Json usam reflexão). O `.exe` não é assinado: o SmartScreen avisa na primeira vez. `.github/dependabot.yml` abre PRs semanais para a `develop`; `SECURITY.md` aponta o reporte privado de falhas.
