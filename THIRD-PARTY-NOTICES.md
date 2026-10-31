# Componentes de terceiros e licenças

Auditoria feita em 2026-10-30. Todos os itens permitem uso, modificação e distribuição,
inclusive comercial. O projeto não depende de nenhum serviço pago nem de API externa.

## Distribuídos junto com o programa (instalador)

| Componente | Versão | Licença | Observação |
|---|---|---|---|
| .NET Runtime (`Microsoft.NETCore.App`) | 10.0.14 | MIT | Embutido (publicação autocontida). Avisos: [`licenses/dotnet-runtime-LICENSE.txt`](licenses/dotnet-runtime-LICENSE.txt), [`licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt`](licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt) |
| Windows Desktop Runtime / WPF (`Microsoft.WindowsDesktop.App`) | 10.0.14 | MIT | Embutido. Aviso: [`licenses/windowsdesktop-runtime-LICENSE.txt`](licenses/windowsdesktop-runtime-LICENSE.txt) |
| Inno Setup (motor do instalador) | 6.7.4 | Licença Inno Setup (permissiva: qualquer finalidade, inclusive comercial, mantendo os avisos de copyright) | © Jordan Russell e Martijn Laan — https://jrsoftware.org/ |
| Bíblia Livre (BLIVRE) — textos dos versículos | versão 2022-03-08 | CC BY 4.0 | © 2018 Diego Santos, Mario Sérgio e Marco Teles — https://ebible.org/porbr2018/. Títulos de seção removidos. Atribuição exibida na página de bloqueio e em `extension/verses.json`. |

## Usados apenas para desenvolvimento e testes (não distribuídos)

| Pacote | Versão | Licença |
|---|---|---|
| xunit, xunit.core, xunit.assert, xunit.extensibility.*, xunit.analyzers, xunit.abstractions | 2.9.3 / 1.18.0 / 2.0.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 |
| Microsoft.NET.Test.Sdk, Microsoft.TestPlatform.*, Microsoft.CodeCoverage | 17.14.1 | MIT |
| Newtonsoft.Json (dependência transitiva dos testes) | 13.0.3 | MIT |
| .NET SDK | 10.0 | MIT |
| Node.js (scripts em `tools/` e testes da extensão) — sem pacotes npm | 20+ | MIT |
| Python (só `tools/verses/extract.py`, biblioteca padrão) | 3.x | PSF |

## Criados para este projeto (MIT, como o resto do código)

- Ícone (cruz partindo a cauda), gerado por `tools/icon/IconGen.cs`.
- Lista de domínios (`blocklist/`) e regras derivadas (`extension/rules/`).
- Textos de reflexão em `extension/verses.json` (o texto bíblico é BLIVRE, acima).

## Não usados

- Fontes: o app usa Segoe UI, a fonte do próprio Windows; nenhuma fonte é distribuída.
- Modelos de IA: nenhum. O classificador de imagens da antiga Fase 2 (MobileNetV2 do
  GantMan/nsfw_model, MIT) foi removido e não faz parte do repositório nem do instalador.
- Serviços externos em tempo de execução: nenhum. Em desenvolvimento: NuGet.org (pacotes),
  eBible.org (só para regenerar os versículos), jrsoftware.org (Inno Setup).

## Vulnerabilidades

`dotnet list package --vulnerable --include-transitive` em 2026-10-30: nenhum pacote vulnerável
em nenhum projeto. O app, o serviço local e o núcleo não usam pacotes NuGet.
