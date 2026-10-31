# Como contribuir

Obrigado por ajudar! Contribuições de código, listas de domínios, traduções e documentação são bem-vindas.

## Princípios do projeto (não negociáveis)

1. **Privacidade**: nada sai do computador do usuário. Nenhuma telemetria, conta ou servidor.
2. **Uma pausa, não uma prisão**: o dono do computador sempre pode recuperar o controle
   ([docs/RECUPERACAO.md](docs/RECUPERACAO.md)). Nada de técnicas de malware, ocultação ou
   persistência clandestina.
3. **Gratuito**: sem assinaturas, licenças pagas ou dependências com cobrança.
4. **Tom acolhedor**: mensagens que encorajam, nunca humilham ou condenam.

## Preparar o ambiente

- Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download), Node.js 20+.
- Para o instalador: [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
dotnet build
dotnet test
node --test "tests/extension/*.test.mjs"
node tests/e2e/browser.e2e.mjs edge     # ou brave — abre o navegador headless
```

## Antes de abrir um pull request

- `dotnet build -warnaserror` sem avisos e todos os testes passando.
- Mudou `blocklist/`? Rode `node tools/build-rules.mjs` e inclua as regras geradas.
- Rode `node tools/privacy-scan.mjs` (com `PRIVACY_PATTERNS` contendo seus identificadores) e
  confira que nenhum dado pessoal, caminho local ou segredo entrou no commit.
- Use no Git um e-mail `noreply` do GitHub se não quiser expor o seu.

## Lista de domínios

Adicione domínios em `blocklist/domains.txt` (um por linha, sem `www.` nem caminho). Só sites cujo
propósito principal é conteúdo sexual explícito. Sites de ajuda que a regra de palavra-chave
pegaria por engano vão em `blocklist/allow.txt`.

## Sobre a extensão de desenvolvimento

O ID da extensão (`olipaknkbpadbdcdaaehfnpolcdeieee`) é fixado pela chave **pública** em
`extension/manifest.json`; é ela que o serviço local reconhece. A chave privada correspondente
não é publicada e não é necessária para compilar, testar ou usar o projeto.
