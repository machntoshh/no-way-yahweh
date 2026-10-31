# Configuração

O programa **não precisa de nenhuma credencial, chave de API ou arquivo `.env`** — por isso o
repositório não tem `.env.example`. Tudo o que o usuário configura é feito pelo app e fica local
(veja [PRIVACIDADE.md](PRIVACIDADE.md)).

## Variáveis de ambiente (opcionais, só para desenvolvimento)

| Variável | Usada por | Efeito |
|---|---|---|
| `NOWAYYAHWEH_DATA_DIR` | app, serviço local, testes | Troca a pasta de dados (padrão `%LOCALAPPDATA%\NoWayYahweh`). Os testes usam pastas temporárias para não tocar nos seus dados. |
| `DOTNET_ROOT` | `tools/build-installer.ps1` | Onde está o .NET SDK, se não estiver no `PATH`. |
| `ISCC` | `tools/build-installer.ps1` | Caminho do `ISCC.exe` do Inno Setup, se não estiver nos locais padrão. |
| `PRIVACY_PATTERNS` | `tools/privacy-scan.mjs` | Expressão regular com **seus** identificadores (nome, usuário, e-mail) para procurar antes de publicar. Nunca escreva esses valores em arquivos do repositório. |
| `NWY_BRAVE_KEY` | `tests/e2e/browser.e2e.mjs` | Chave de registro alternativa do Brave, para depuração. |

## Configurações do usuário (no app)

- Verificação ao iniciar o Windows (ligada por padrão).
- Bloqueio de janelas privadas/anônimas (opcional, pede administrador).
- Senha de responsável (opcional).
- Domínios extras (`extra-domains.txt`, um por linha).
- Pausa temporária das proteções secundárias (com reflexão e desafios).
