# Política de segurança

## Versões com suporte

Só a versão mais recente publicada em [Releases](https://github.com/Azincourt-tech/Widget-Ai-Usage/releases) recebe correções de segurança.

## Como reportar uma falha

**Não abra uma issue pública** para falhas de segurança.

Use o reporte privado do GitHub: na aba **Security** do repositório, clique em **Report a vulnerability**. Só os mantenedores veem o relato.

Inclua, se puder:

- o que acontece e qual o impacto;
- os passos para reproduzir;
- a versão do widget e do Windows.

Nunca envie tokens, senhas ou dados reais das suas contas no relato.

## O que esperar

Este é um projeto pessoal, mantido no tempo livre. O objetivo é responder em até 7 dias e, confirmada a falha, publicar a correção numa nova versão o quanto antes, dando o crédito a quem reportou (se quiser).

## Escopo

Interessam especialmente:

- vazamento de tokens ou logins (cofre do Windows, arquivos de configuração, logs);
- o servidor local que recebe o retorno do login (`127.0.0.1`);
- qualquer forma de dados das contas saírem do computador do usuário.

Ficam de fora problemas nos serviços da Anthropic, OpenAI ou Google em si: reporte direto a eles.
