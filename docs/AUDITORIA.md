# Auditoria de privacidade e segurança (pré-publicação)

Data: 2026-10-30 · Escopo: todos os arquivos que entram no repositório público (o que o Git não
ignora) e os binários distribuídos.

## 1. Histórico Git

O projeto não tinha repositório Git: **não existe histórico anterior**, branches, tags, commits ou
autores a auditar ou reescrever. O repositório público começa com histórico novo. Nenhuma operação
destrutiva foi necessária.

## 2. Dados pessoais e do ambiente de desenvolvimento

| Verificação | Resultado |
|---|---|
| Nomes, usuários, e-mails, nome da máquina | Nenhum nos arquivos publicáveis. O titular dos direitos é "No way? Yahweh! contributors". |
| Caminhos locais (perfil do Windows, unidades de desenvolvimento) | Removidos: o script do instalador usava pastas do ambiente de desenvolvimento; agora usa PATH, variáveis de ambiente e locais padrão. Restam só caminhos genéricos (`%LOCALAPPDATA%`, instalação padrão do Edge/Brave nos testes). |
| Descrição do ambiente/hábitos de quem desenvolveu | Generalizada na documentação. |
| Metadados de imagens (PNG/ICO) | Gerados por código, sem texto embutido. |
| Binários publicados (`dotnet publish`, instalador) | Sem caminhos locais: build de distribuição sem PDB (`DebugType=none`). |
| Logs, relatórios, temporários | Nenhum no repositório; `bin/`, `obj/`, `artifacts/`, `TestResults/`, `*.log` ignorados. |

Ferramenta: `node tools/privacy-scan.mjs`, rodada com os identificadores pessoais passados só por
variável de ambiente (`PRIVACY_PATTERNS`) — eles não estão escritos em nenhum arquivo. Resultado: 0 achados.

## 3. Segredos e credenciais

| Item | Situação |
|---|---|
| Chave privada da extensão de desenvolvimento (`keys/`) | **Excluída** do repositório (`.gitignore`: `keys/`, `*.pem`, `*.key`, `*.pfx`, `*.p12`, `*.snk`). Nunca foi publicada, então não precisa de rotação. A chave **pública** fica no `manifest.json` por design. |
| Tokens, senhas, API keys | Nenhum encontrado (padrões GitHub, AWS, Google, Slack, `sk-`, atribuições de senha). Valores de teste fictícios marcados como tal. |
| `.env` | O projeto não usa credenciais nem `.env`; por isso não há `.env.example` (ver `docs/CONFIGURACAO.md`). `.env*` está no `.gitignore`. |
| Certificados de assinatura | Nenhum (instalador não assinado). |

## 4. Privacidade de quem usa

Sem telemetria, rastreamento, contas ou identificadores. Nenhuma chamada de rede no código
distribuído (verificado: o único `fetch` lê `verses.json` de dentro da extensão). O que fica salvo
e como apagar: `docs/PRIVACIDADE.md`, `docs/RECUPERACAO.md`.

## 5. Dependências, licenças e vulnerabilidades

Detalhes em `THIRD-PARTY-NOTICES.md`. Resumo: código MIT; runtime .NET e WPF (MIT) embutidos no
instalador com seus avisos em `licenses/`; Inno Setup (licença permissiva); textos bíblicos
BLIVRE (CC BY 4.0, com atribuição); dependências de teste MIT/Apache-2.0. Nenhum serviço pago,
nenhuma API com cobrança. `dotnet list package --vulnerable --include-transitive`: nenhum
pacote vulnerável.

## 6. Testes feitos a partir de uma cópia limpa

Cópia só com os arquivos publicáveis (sem `keys/`, sem builds anteriores):
`dotnet build -warnaserror` (0 avisos), 76 testes C#, 11 testes de regras, ponta a ponta no Edge e no
Brave, geração do instalador. O instalador gerado não copia arquivos do navegador (`_metadata`).
O ciclo completo instalar/atualizar/desinstalar (`tests/installer/installer.test.ps1`) deve ser rodado
num ambiente sem instalação prévia (VM ou Windows Sandbox); ele se recusa a rodar onde já há uma.

## 7. Riscos e pontos de atenção

- **Identidade do Git**: os commits passam a mostrar o nome e o e-mail configurados no Git local.
  Use um e-mail `noreply` do GitHub e um nome neutro antes do primeiro commit.
- **Conta do GitHub**: o repositório fica visível sob a conta (ou organização) que o publicar.
  Para não associá-lo à sua conta pessoal, publique por uma organização ou conta dedicada.
- **ID da extensão**: qualquer extensão carregada sem compactação com a mesma chave pública obtém o
  mesmo ID; o impacto está descrito em `SECURITY.md` (sem exposição de dados).
- **Instalador sem assinatura**: SmartScreen avisa; não é risco de privacidade.
