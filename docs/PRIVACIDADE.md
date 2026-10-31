# Privacidade

**Resumo:** nada sai do seu computador. Não há conta, telemetria, rastreamento, anúncios nem
servidor. O programa funciona offline.

## O que o programa nunca faz

- Não envia nada pela internet: nem endereços visitados, nem estatísticas, nem identificadores.
  O código distribuído não contém nenhuma chamada de rede (a única leitura é de um arquivo da
  própria extensão, `verses.json`).
- Não registra histórico: a extensão só conta *que* houve um bloqueio, nunca *qual* site.
- Não monitora outros programas nem processos do computador.
- Não identifica quem usa: não há ID de usuário, de máquina ou de instalação.

## O que fica guardado, e onde

| Onde | O quê | Para quê |
|---|---|---|
| `%LOCALAPPDATA%\NoWayYahweh\state.json` | data de início da proteção; total de bloqueios; último contato de cada navegador (Brave/Chrome/Edge); fim da pausa, se houver; últimos tipos de desafio; hash da senha de responsável, se definida; se a verificação ao iniciar já foi ativada | painel do app, pausa, desafios |
| `%LOCALAPPDATA%\NoWayYahweh\extra-domains.txt` | domínios que **você** escolheu bloquear | regras extras |
| `%LOCALAPPDATA%\NoWayYahweh\com.nowayyahweh.host.json` | manifesto que diz aos navegadores onde está o serviço local | integração |
| Armazenamento da extensão no navegador | bloqueios ainda não contados; se o serviço local respondeu e, se não, a mensagem de erro; fim da pausa | funcionar mesmo sem o serviço |
| Registro do Windows (HKCU) | localização do serviço para Brave/Chrome/Edge; entrada de inicialização (opcional) | integração, verificação ao iniciar |
| Registro do Windows (HKLM, opcional) | política que desliga janelas privadas | só se você ativar |

A senha de responsável é guardada como hash PBKDF2-SHA256 com sal aleatório; a senha em si não
é armazenada.

## Como apagar tudo

- Pelo app: **Desinstalar…**, ou "Aplicativos instalados" do Windows.
- À mão, inclusive sem o app ou sem a senha: [RECUPERACAO.md](RECUPERACAO.md).

## Conexões só durante o desenvolvimento

Quem compila o projeto baixa pacotes do NuGet.org; regenerar os versículos lê páginas do
eBible.org. Nada disso acontece no programa instalado.
