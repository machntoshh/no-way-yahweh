# Segurança

## Como relatar uma vulnerabilidade

Use o relato **privado** do GitHub: aba **Security → Report a vulnerability** deste repositório.
Não abra issue pública para falhas de segurança. Não há e-mail pessoal de contato.

## Modelo de ameaça (resumo)

- O serviço local (`NoWayYahweh.Host.exe`) só aceita conexões iniciadas pelo navegador a partir da
  origem da extensão listada no manifesto, valida cada mensagem com um protocolo mínimo e
  estrito, e nunca recebe URLs nem conteúdo de páginas.
- A chave pública da extensão está no `manifest.json`; uma extensão carregada sem compactação com a
  mesma chave obtém o mesmo ID e pode falar com o serviço local. O pior caso é alterar contadores
  ou ler a lista de domínios extras do próprio usuário — nada que exponha dados.
- O programa é uma **pausa**, não uma tranca: um administrador do computador sempre pode removê-lo
  (por design, veja [docs/RECUPERACAO.md](docs/RECUPERACAO.md)). Relatos de "consegui desinstalar
  sendo administrador" não são vulnerabilidades; contornos *sem* privilégio que evitem a tela de
  desafio são.
- Limites conhecidos estão em [docs/ARQUITETURA.md](docs/ARQUITETURA.md), §9.
