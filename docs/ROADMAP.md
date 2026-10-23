# Roadmap

Só o que ainda está pendente. O que já está pronto fica descrito no [README](../README.md) (para quem usa) e no [guia técnico](DESENVOLVIMENTO.md).

## Windows

- **Observar "muitas consultas" (429):** se aparecer com o uso, aumentar o intervalo ou a pausa. Até 22/10/2026 não apareceu nenhum, com a leitura a cada 1 minuto e as três contas.

## Consumo de memória

Feito em outubro de 2026: desenho por software e `.exe` sem compressão. No `--demo` a memória caiu de ~75–80 MB para ~29–37 MB (números e como medir no [guia técnico](DESENVOLVIMENTO.md#consumo-de-memória)). Falta:

- **Conferir com as contas reais:** o usuário tinha visto ~130 MB. Medir o widget real (coluna "Memória" do Gerenciador de Tarefas) com o `.exe` novo, sem anotar nem mostrar os limites.
- **Medir depois de algumas horas aberto**, para saber se a memória sobe com o tempo (vazamento).
- **Rede:** um `HttpClient` reaproveitado por provedor, e respostas lidas sem guardar o texto inteiro.
- **Janela de configurações** liberada ao fechar, e nada de janelas escondidas guardadas.

## Modo bandeja (decidido em outubro de 2026)

Inspirado no [ai-usagebar](https://github.com/akitaonrails/ai-usagebar) do Akita. O usuário escolhe nas configurações um **Modo**: "Widget na área de trabalho" (como hoje), "Ícone na bandeja" ou os dois. O Core (`WidgetViewModel`, provedores, avisos) é o mesmo; muda só a interface.

- **Ícone na área de notificação** com o `TrayIcon` do Avalonia. O ícone é um mini anel desenhado em 16 px com o maior uso entre as contas. A dica ao passar o mouse resume todas, por exemplo "Claude 42% · Codex 26%".
- **Clique esquerdo** abre e fecha uma janelinha (popover). **Clique direito** abre o menu de hoje: Atualizar agora, Configurações e Sair.
- **Posição:** o Avalonia não informa onde fica o ícone, então a janelinha abre perto do cursor (`GetCursorPos`), dentro da área livre do monitor, sem a barra de tarefas. É o mesmo jeito do ai-usagebar.
- **Fecha sozinha** ao clicar fora, ao clicar de novo no ícone ou com Esc. Cuidado (visto no ai-usagebar): o Windows às vezes ativa a barra de tarefas sozinho quando o ícone é atualizado. Isso não pode fechar a janelinha.
- **Visual (escolha deixada para o que ficar melhor):** a proposta é seguir o print que o usuário trouxe.
  - No topo, uma aba por conta com o ponto colorido e o maior %.
  - Embaixo, os limites da conta escolhida em **barras horizontais**: nome, %, "Renova em 1h 38m" e a linha "Atualizado há 2 min".
  - As barras leem melhor que os anéis numa janela estreita e com texto. Os anéis continuam no widget e no ícone.
  - Respeita os três temas. Confirmar na tela antes de fechar o visual.
- **Atalho global (opcional)** para abrir e fechar a janelinha de qualquer lugar, com `RegisterHotKey`.

## Aproveitar os logins dos CLIs (decidido em outubro de 2026)

O usuário decidiu fazer **como o ai-usagebar**: usar o login que já existe no PC (Claude Code, Codex CLI, Antigravity) e renovar o token quando vencer, gravando o novo no próprio arquivo do CLI. **Isso substitui a decisão de "só login próprio" do CLAUDE.md.** Ao implementar, atualizar o CLAUDE.md, o README (privacidade: o widget lê e atualiza o login dos CLIs) e o DESENVOLVIMENTO.md.

- **Ordem:** primeiro o login do CLI. Sem ele, o login próprio de hoje continua valendo (para quem não tem o CLI instalado).
- **Onde está cada login** (formato conferido só pelos nomes dos campos):
  - **Claude:** `%USERPROFILE%\.claude\.credentials.json`, objeto `claudeAiOauth` com `accessToken`, `refreshToken`, `expiresAt` (milissegundos), `scopes`, `subscriptionType` e `rateLimitTier`. Há outros campos no arquivo (`organizationUuid`, `mcpOAuth`) que precisam ser preservados.
  - **Codex:** `%USERPROFILE%\.codex\auth.json`, com `auth_mode`, `OPENAI_API_KEY`, `last_refresh` e `tokens` (`id_token`, `access_token`, `refresh_token`, `account_id`).
  - **Antigravity:** credencial genérica `gemini:antigravity` no Gerenciador de Credenciais. É gravada pelo `go-keyring` e pode vir com o prefixo `go-keyring-base64:`. **Só leitura:** o Google costuma manter o refresh token, então o widget renova em memória com o próprio secret e nunca grava nessa credencial.
- **Renovação nos arquivos do Claude e do Codex:** renovar com o refresh token do CLI e gravar de volta de forma atômica (arquivo temporário e troca), mantendo todos os outros campos.
  - O refresh token troca a cada renovação. Se o CLI e o widget renovarem ao mesmo tempo, um deles fica com um token inválido.
  - Proteções além das do ai-usagebar: ler o arquivo de novo logo antes de renovar (o CLI pode já ter renovado). Se a renovação for recusada, ler o arquivo de novo antes de desistir.
- **Nunca apagar nem esvaziar o arquivo do CLI.** "Sair" e o login vencido só fazem o widget parar de usar aquele login e voltar ao login próprio.
- **Detecção sem rede:** na primeira vez, procurar esses logins e mostrar a conta já lida, sem pedir "Entrar". O menu do clique direito ganha "Procurar logins".
- **O card diz a origem do login**, por exemplo "via Claude Code".
- **Testes** com arquivos fictícios: leitura (`ParseXxx` estático), gravação de volta preservando os outros campos, não apagar o arquivo e voltar ao login próprio.

## Idiomas (inglês e espanhol)

Hoje tudo está em português. Para traduzir para inglês e espanhol:

- **Textos da interface e do Core** (janelas, configurações, mensagens dos cards, avisos, "renova em...") num lugar só, por exemplo arquivos `.resx` por idioma (`pt-BR`, `en`, `es`), sem texto fixo no código.
- **Datas e horários** no formato de cada idioma (hoje o "renova sex. 21:22" usa a cultura `pt-BR` fixa).
- **Escolha do idioma:** pelo idioma do Windows na primeira vez, com uma opção "Idioma" nas configurações.
- **Notificações e modelos de issue** também traduzidos.
- **README:** versões em inglês e espanhol (`README.en.md`, `README.es.md`) com links entre elas no topo.
- **Testes:** conferir que todo texto existe nos três idiomas e que a interface não quebra com textos mais longos (o espanhol costuma ser maior que o português).

## Em avaliação

- **Chaves de API (pagamento por uso):** mostrar o gasto do mês pelas APIs oficiais de uso e custo da Anthropic e da OpenAI. Pela mesma via, outros provedores que o ai-usagebar cobre por chave: Z.AI, OpenRouter e Kimi.

Outras ideias vistas no ai-usagebar, ainda sem decisão:

- **Ritmo:** um tracinho no anel ou na barra marca onde o uso estaria se fosse gasto por igual até renovar. Uma nota mostra "Acaba em 1d 9h" quando o ritmo estoura o limite antes da renovação, ou "Sobra ~40% ao renovar". É cálculo puro no Core, fácil de testar.
- **Usado ou restante:** clicar no % troca entre "80% usado" e "20% restante".
- **"Renova em 1h 38m" ou a hora exata**, como opção nas configurações.
- **Aviso de nova versão** pelas releases do GitHub, conferindo o `.sha256`. Só avisa, sem instalar sozinho.
- **Resets acumulados do Claude** ("Rate Limit Resets: 1 disponível"): conferir se a resposta de uso traz isso e mostrar no card.
- **Antigravity com janela de 5 h:** segundo o ai-usagebar, cada grupo tem uma janela de 5 h e uma semanal. Na conta do usuário só veio a semanal; talvez dependa do plano.

## Suporte ao Linux

**Fica por último (decisão do usuário, outubro de 2026):** só depois das outras melhorias deste roadmap.

Hoje só o Windows funciona fora do modo demonstração. Para o Linux (testado por um amigo do usuário) falta:

- Cofre de segredos com libsecret (`secret-tool`), implementando `ISecretVault`. Se o `secret-tool` não estiver instalado, mostrar como instalar (`libsecret-tools` no Debian/Ubuntu).
- Abrir o navegador com `xdg-open` (`IBrowserLauncher`).
- Conferir o tema Vidro (o desfoque depende do compositor: bom no KDE, limitado no GNOME) e os cantos arredondados.
- Iniciar com o sistema (arquivo `.desktop` em `~/.config/autostart`).
- Avisos de 80% e 95% com a notificação do sistema (`notify-send` ou D-Bus `org.freedesktop.Notifications`), no lugar do `NoNotificationService`.
- Empacotamento: AppImage primeiro, Flatpak depois.
- Um roteiro curto de teste para quem for testar.

O macOS não está nos planos.
