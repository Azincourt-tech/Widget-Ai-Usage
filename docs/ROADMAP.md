# Roadmap

Só o que ainda está pendente. O que já está pronto fica descrito no [README](../README.md) (para quem usa) e no [guia técnico](DESENVOLVIMENTO.md).

## Windows

- **Microsoft Store (MSIX), em paralelo ao `.exe`:** uma tentativa de distribuição maior, **sem substituir o `.exe` do GitHub**, que continua sendo lançado igual. Na loja, a Microsoft assina o pacote, então o SmartScreen não avisa nada; a conta de desenvolvedor é grátis para pessoa física.
  - Empacotar como MSIX no mesmo workflow de release (o `.exe` e o MSIX saem da mesma versão).
  - Com identidade de pacote: "Iniciar com o Windows" pelo recurso do pacote (no lugar da chave `Run`) e notificações pela identidade do pacote. O `.exe` segue como hoje.
  - Página na loja: descrição, imagens (do modo demo) e política de privacidade (nada sai do PC).
  - Risco: a certificação pode questionar o uso dos `client_id` dos aplicativos oficiais, o `User-Agent` do Antigravity ou os nomes Claude e ChatGPT. Se recusarem, o `.exe` continua e dá para tentar o SignPath Foundation (grátis para código aberto, mas o SmartScreen ainda avisa até ganhar reputação).
  - Assinar o `.exe` com certificado próprio não compensa agora: desde 2024 nenhum certificado tira o aviso de imediato, e a assinatura da Microsoft (Azure Artifact Signing) não está disponível para pessoa física no Brasil.
- **Observar "muitas consultas" (429):** se aparecer com o uso, aumentar o intervalo ou a pausa.

## Idiomas (inglês e espanhol)

Hoje tudo está em português. Para traduzir para inglês e espanhol:

- **Textos da interface e do Core** (janelas, configurações, mensagens dos cards, avisos, "renova em...") num lugar só, por exemplo arquivos `.resx` por idioma (`pt-BR`, `en`, `es`), sem texto fixo no código.
- **Datas e horários** no formato de cada idioma (hoje o "renova sex. 21:22" usa a cultura `pt-BR` fixa).
- **Escolha do idioma:** pelo idioma do Windows na primeira vez, com uma opção "Idioma" nas configurações.
- **Notificações e modelos de issue** também traduzidos.
- **README:** versões em inglês e espanhol (`README.en.md`, `README.es.md`) com links entre elas no topo.
- **Testes:** conferir que todo texto existe nos três idiomas e que a interface não quebra com textos mais longos (o espanhol costuma ser maior que o português).

## Suporte ao Linux

Hoje só o Windows funciona fora do modo demonstração. Para o Linux (testado por um amigo do usuário) falta:

- Cofre de segredos com libsecret (`secret-tool`), implementando `ISecretVault`. Se o `secret-tool` não estiver instalado, mostrar como instalar (`libsecret-tools` no Debian/Ubuntu).
- Abrir o navegador com `xdg-open` (`IBrowserLauncher`).
- Conferir o tema Vidro (o desfoque depende do compositor: bom no KDE, limitado no GNOME) e os cantos arredondados.
- Iniciar com o sistema (arquivo `.desktop` em `~/.config/autostart`).
- Avisos de 80% e 95% com a notificação do sistema (`notify-send` ou D-Bus `org.freedesktop.Notifications`), no lugar do `NoNotificationService`.
- Empacotamento: AppImage primeiro, Flatpak depois.
- Um roteiro curto de teste para quem for testar.

O macOS não está nos planos.

## Em avaliação

- **Ícone na bandeja com popover.** O Avalonia não informa a posição do ícone; a janela teria que abrir perto do cursor. Decidir depois de usar o widget flutuante por um tempo.
- **Chaves de API (pagamento por uso):** mostrar o gasto do mês pelas APIs oficiais de uso e custo da Anthropic e da OpenAI.
