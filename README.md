<p align="center"><img src="docs/icon-512.png" width="128" alt="Ícone: uma cruz partindo uma cauda de súcubo"></p>

<h1 align="center">No way? Yahweh!</h1>
<p align="center"><em>A pause before the temptation.</em></p>

Bloqueador de pornografia para Windows, **gratuito, de código aberto e 100% local**, feito para
quem quer abandonar o consumo de pornografia — especialmente cristãos que desejam crescer em
autocontrole. Em vez de só barrar, ele oferece **uma pausa**: um versículo, uma reflexão e um
desafio de raciocínio entre o impulso e a decisão.

> Sem conta, sem servidor, sem telemetria. Nada sai do seu computador.

## Recursos

- **Bloqueio de sites adultos** no Brave, Chrome e Edge: domínios conhecidos, todos os seus
  subdomínios, redirecionamentos para eles e conteúdo deles embutido em outros sites; endereços
  cujo nome contém "porn" ou "hentai"; e domínios que você mesmo adicionar. Funciona offline.
- **Página de bloqueio acolhedora**, com o motivo, um versículo (Bíblia Livre) e uma reflexão —
  nunca mensagens que humilham ou condenam.
- **Pausa com desafios**: para pausar as regras secundárias (15 min, 1 h ou 4 h), passe por uma
  reflexão com espera de 20 s e 1–3 desafios (conta, sequência, lógica, memória, ordenação,
  raciocínio em etapas). Os sites conhecidos **nunca** são pausados; a pausa termina sozinha.
- **Segurança**: verificação ao iniciar o Windows, bloqueio opcional de janelas privadas/anônimas
  (com permissão de administrador), senha opcional para uma pessoa de confiança guardar e
  verificação de integridade.
- **Sempre recuperável**: desinstalar passa pelo mesmo desafio, mas o dono do computador sempre
  pode remover tudo — [docs/RECUPERACAO.md](docs/RECUPERACAO.md).

## Privacidade

Processamento e armazenamento só locais; nenhum dado pessoal coletado; nenhuma chamada de rede no
programa distribuído; nenhum identificador de usuário. A extensão conta *que* houve um bloqueio,
nunca *qual* site. Detalhes do que fica guardado e como apagar: [docs/PRIVACIDADE.md](docs/PRIVACIDADE.md).

## Instalação

1. Baixe ou gere o `NoWayYahweh-Setup-<versão>.exe` e execute. Não pede administrador.
   O instalador ainda não é assinado digitalmente, então o Windows SmartScreen pode avisar:
   **Mais informações → Executar assim mesmo**.
2. No navegador (`brave://extensions`, `chrome://extensions` ou `edge://extensions`): ative o
   **Modo do desenvolvedor**, clique em **Carregar sem compactação** e escolha
   `%LOCALAPPDATA%\Programs\NoWayYahweh\extension`.
3. Em **Detalhes** da extensão, ative **Permitir em modo privado**.

Requisitos: Windows 10 ou 11 (x64). O runtime .NET já vai dentro do instalador.

## Compilar a partir do código

Requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download), Node.js 20+ e, para o
instalador, [Inno Setup 6](https://jrsoftware.org/isinfo.php). Nenhuma credencial é necessária.

```powershell
dotnet build                                   # app, serviço local e testes
dotnet test                                    # testes C#
node --test "tests/extension/*.test.mjs"       # testes das regras da extensão
node tests/e2e/browser.e2e.mjs edge            # ponta a ponta num navegador real (edge ou brave)
powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1   # gera artifacts\installer\*.exe
```

### Gerar o instalador

`tools\build-installer.ps1` procura o .NET SDK no `PATH` e o Inno Setup nas pastas padrão de
instalação. Se algum deles estiver em outro lugar, informe o caminho na mesma janela do PowerShell
antes de rodar o script (as variáveis valem só para aquela janela):

```powershell
$env:DOTNET_ROOT = 'C:\caminho\para\dotnet'               # pasta que contém dotnet.exe
$env:ISCC = 'C:\caminho\para\Inno Setup 6\ISCC.exe'
powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1
```

O instalador fica em `artifacts\installer\NoWayYahweh-Setup-<versão>.exe`. Se aparecer
"Nenhum .NET SDK encontrado", defina `DOTNET_ROOT`; se o Inno Setup não for encontrado, defina `ISCC`.

Para usar sem instalador: rode `artifacts\bin\Debug\NoWayYahweh.exe` uma vez (ele registra o serviço
nos navegadores) e carregue a pasta `extension/` no navegador.

Variáveis de ambiente opcionais: [docs/CONFIGURACAO.md](docs/CONFIGURACAO.md).

## Como funciona

```
Navegador (extensão MV3)  ──Native Messaging──▶  NoWayYahweh.Host.exe  ──▶  %LOCALAPPDATA%\NoWayYahweh
  regras de bloqueio                               valida mensagens         ▲
  página de bloqueio                               contador, pausa          │
                                                                    NoWayYahweh.exe (painel WPF)
```

O bloqueio vive dentro do navegador (`declarativeNetRequest`) e continua funcionando mesmo sem o
serviço local. Decisões, limites e critérios de cada fase: [docs/ARQUITETURA.md](docs/ARQUITETURA.md).

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `extension/` | Extensão Manifest V3 (regras geradas, página de bloqueio, versículos, ícones) |
| `src/NoWayYahweh.Core` | Protocolo, estado, registro, políticas, desafios, senha |
| `src/NoWayYahweh.Host` | Serviço local chamado pelo navegador (Native Messaging) |
| `src/NoWayYahweh.App` | Painel WPF, tela de pausa/desafios, configurações |
| `blocklist/` | Fonte da lista de domínios e da lista de exceções |
| `installer/` | Script do Inno Setup |
| `licenses/` | Avisos de licença do runtime .NET distribuído no instalador |
| `tests/` | xUnit, testes das regras, ponta a ponta no navegador, ciclo do instalador |
| `tools/` | Geração de regras, ícone e versículos; build do instalador; diagnóstico; verificação de privacidade |
| `docs/` | Arquitetura, privacidade, configuração, recuperação |

## Limites conhecidos

- A extensão é carregada no modo de desenvolvedor, então pode ser desligada em `brave://extensions`
  sem passar pelo desafio. Fechar esse caminho exige publicá-la em loja e usar a política de
  instalação forçada (planejado).
- Firefox e Opera não são suportados.
- O instalador não é assinado digitalmente.
- A lista vem com o instalador; ainda não há atualização online.

Matriz completa de contornos: [docs/ARQUITETURA.md §9](docs/ARQUITETURA.md).

## Contribuir

Veja [CONTRIBUTING.md](CONTRIBUTING.md). Falhas de segurança: [SECURITY.md](SECURITY.md).

## Licença

Código sob a [licença MIT](LICENSE). Os textos bíblicos são da **Bíblia Livre (BLIVRE)**,
© 2018 Diego Santos, Mario Sérgio e Marco Teles, sob [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
Componentes de terceiros e suas licenças: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

<p align="center"><strong>Obrigado pela sua confiança em mim e Deus te abençoe!</strong></p>
