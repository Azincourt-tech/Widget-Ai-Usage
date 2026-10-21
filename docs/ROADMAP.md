# Roadmap

Só o que ainda está pendente. O que já está pronto fica descrito no [README](../README.md) e no [CLAUDE.md](../CLAUDE.md).

## Windows

- **Empacotamento:** `.exe` único, compilado pelo GitHub Actions com o secret do Antigravity guardado nos secrets do repositório (variável `AntigravityClientSecret`).
- **Observar "muitas consultas" (429):** se aparecer com o uso, aumentar o intervalo ou a pausa.

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
