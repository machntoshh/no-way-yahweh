# No way? Yahweh! — Arquitetura e viabilidade

> *A pause before the temptation.*

Este documento registra a avaliação técnica feita antes do código e as decisões que guiam
o projeto. Ele é atualizado a cada fase.

---

## 1. Viabilidade técnica

| Requisito | Viável? | Como |
|---|---|---|
| Bloquear domínios adultos, subdomínios e redirecionamentos | **Sim, totalmente** | `declarativeNetRequest` (MV3). A condição `requestDomains` casa o domínio e todos os subdomínios; o navegador avalia cada salto de redirecionamento como uma nova requisição, então redirecionamentos para domínios da lista também são bloqueados. |
| Tela explicando o bloqueio | **Sim** | Regra `redirect` para `blocked.html` da própria extensão. |
| Borrar imagens/vídeos explícitos em sites legítimos | **Tecnicamente sim, mas retirado** | Implementado e testado na Fase 2, depois **removido** por excesso de falsos positivos em uso real. Ver §7. |
| Serviço local que conversa com a extensão | **Sim** | Native Messaging. **Importante:** o host nativo é um processo iniciado *pelo navegador* (um por conexão), não um Windows Service. O estado compartilhado fica em disco, protegido por mutex. |
| Dificultar desativação impulsiva | **Sim, com limites honestos** | Ver §2 e Fase 4. |
| Impedir desativação por um administrador determinado | **Não, e não deve** | O dono do computador sempre pode recuperar o controle. O objetivo é *uma pausa*, não uma prisão. |

## 2. Limitações das APIs dos navegadores (e como lidamos)

1. **O usuário pode desativar/remover a extensão em `chrome://extensions`.** A única forma
   legítima de impedir isso é política corporativa (`ExtensionInstallForcelist` em `HKLM`).
   No Windows **não gerenciado** (sem domínio/MDM), Chrome e Edge só aceitam forçar extensões
   publicadas na Chrome Web Store / Edge Add-ons. → Fase 5 publica a extensão nas lojas e a Fase 4
   grava a política (exige admin; remover também exige admin + passar pelo desafio no app).
2. **Janela anônima / InPrivate**: extensões ficam desligadas por padrão. → Política
   `IncognitoModeAvailability=1` (Chrome) e `InPrivateModeAvailability=1` (Edge), opcional, Fase 4.
3. **Outros navegadores**: Brave é suportado (usa a própria chave `HKCU\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts`). Firefox e Opera ainda não. Brave aceita as mesmas
   políticas Chromium em chave própria (`HKLM\Software\Policies\BraveSoftware\Brave`) — entra na Fase 4. Um filtro DNS familiar pode ser
   oferecido como camada *opcional e explícita*; o app nunca altera DNS sem o usuário pedir.
4. **Regra sem `resourceTypes` ignora `main_frame`** (padrão do DNR). Toda regra declara os tipos — há teste para isso.
5. **Limites do declarativeNetRequest**: 30.000 regras estáticas garantidas por extensão, 1.000 regras
   regex, 30.000 dinâmicas. Como uma única regra aceita uma lista de `requestDomains`, cabem centenas
   de milhares de domínios em poucas regras. Ação `redirect` exige `host_permissions`.
6. **Service worker MV3 é encerrado após ~30 s ocioso.** Nada de estado só em memória: contadores
   pendentes ficam em `chrome.storage.local`.
7. **Native Messaging**: mensagem do host → navegador ≤ 1 MB; navegador → host ≤ 64 MiB. O manifesto do host precisa do campo `description` (sem ele: "host not found"). O navegador
   passa a origem da extensão como argumento do host; o host recusa qualquer outra origem.
8. **Não existe API para ver o conteúdo de outros programas** — e não queremos: o serviço não monitora
   processos.

## 3. Arquitetura

```
┌──────────── Chrome / Edge ────────────┐
│  regras DNR (estáticas + dinâmicas)   │  bloqueiam sites antes da requisição
│  service worker (background.js)       │  contador pendente, domínios extras
│  blocked.html                         │  explica o bloqueio, versículo (BLIVRE)
└────────┼──────────────────────────────┘
         │ Native Messaging (stdin/stdout, JSON com prefixo de 4 bytes)
┌────────▼──────────────────────────────┐
│  NoWayYahweh.Host.exe                 │  valida origem e mensagens, atualiza estado,
│  (iniciado pelo navegador)            │  entrega domínios extras
└────────┼──────────────────────────────┘
         │ %LOCALAPPDATA%\NoWayYahweh\state.json  (mutex nomeado)
┌────────▼──────────────────────────────┐
│  NoWayYahweh.App.exe (WPF)            │  dashboard, registro do host,
│                                       │  desafios de desativação (Fase 3)
└───────────────────────────────────────┘
```

**Decisões:**

- **A extensão funciona sozinha para o bloqueio de domínios.** As regras estáticas são carregadas
  pelo próprio navegador; se o host não estiver instalado ou o PC estiver offline, o bloqueio continua.
- **Sem histórico.** A extensão informa apenas "houve um bloqueio" (um contador). Nunca envia URL,
  domínio ou título — nem para o host local.
- **Um único módulo de núcleo (`NoWayYahweh.Core`)** contém protocolo, validação, estado e registro do
  host; o Host e o App apenas o usam. Isso evita duas implementações do mesmo formato.
- **Sem Windows Service na Fase 1.** O host nativo já cobre a comunicação. Um serviço só entra se a
  Fase 4 mostrar necessidade concreta (ex.: verificação de integridade das políticas).

## 4. Tecnologias

| Componente | Escolha | Por quê |
|---|---|---|
| App Windows | C# / .NET 10 (LTS) + WPF | Nativo, leve, sem runtime extra além do .NET; WPF é maduro e estável. WinUI 3 traria dependência do Windows App SDK sem ganho real aqui. |
| Host nativo | C# console, mesmo núcleo | Reaproveita protocolo e estado. |
| Extensão | JavaScript puro, Manifest V3 | Sem bundler: menos superfície, revisão mais fácil pela loja. |
| Versículos | Bíblia Livre (BLIVRE), CC BY 4.0 | Texto literal, redistribuível inclusive comercialmente com atribuição; extraído por `tools/verses/` para `extension/verses.json`. |
| Estado | JSON em `%LOCALAPPDATA%` + mutex | Poucos campos, um escritor por vez; SQLite seria excesso. |
| Testes | xUnit (C#), `node:test` (extensão), Edge real via CDP (E2E) | Ferramentas gratuitas e já padrão. |
| Instalador (Fase 5) | MSIX ou Inno Setup (a decidir na Fase 5) | Ambos gratuitos; Inno facilita gravar políticas em HKLM. |

## 5. Estrutura de pastas

```
no-way-yahweh/
├─ blocklist/domains.txt         # fonte única da lista de domínios
├─ extension/                    # extensão MV3 (Chrome/Edge)
│  ├─ manifest.json
│  ├─ background.js
│  ├─ blocked.html / blocked.css / blocked.js
│  ├─ verses.json                # GERADO por tools/verses/ (Bíblia Livre)
│  ├─ rules/adult_domains.json   # GERADO por tools/build-rules.mjs
│  └─ icons/
├─ src/
│  ├─ NoWayYahweh.Core/          # protocolo, validação, estado, registro do host
│  ├─ NoWayYahweh.Host/          # host de Native Messaging
│  └─ NoWayYahweh.App/           # app WPF
├─ tests/
│  ├─ NoWayYahweh.Tests/         # xUnit
│  ├─ extension/                 # node:test (regras)
│  └─ e2e/                       # Edge real carregando a extensão
├─ tools/                        # scripts de build/geração
├─ tools/diagnose.ps1           # checa registro + host no sistema real
└─ docs/
```

## 6. Critérios de aceitação por fase

**Fase 1 — MVP de bloqueio** ✅ (verificado por `tests/e2e/browser.e2e.mjs` no Edge 156 e no Brave 1.98 (Chromium 156))
- [x] Acessar qualquer domínio da lista, ou subdomínio dele, abre `blocked.html` em vez do site (testado em navegador real).
- [x] Um redirecionamento de um site permitido para um domínio da lista também é bloqueado.
- [x] Recursos embutidos vindos de domínios da lista são bloqueados em outros sites (testado com imagem; vídeo/iframe usam a mesma regra, sem teste próprio ainda).
- [x] Sites comuns (google.com, wikipedia.org, youtube.com) não são bloqueados.
- [x] Bloqueio funciona sem o host instalado (verificado). Sem internet: as regras são locais e casam domínios que nem resolvem DNS (teste da palavra-chave), mas ainda não houve teste com a rede desligada.
- [x] Extensão ↔ host: `hello` registra o navegador, `blocked` incrementa o contador; mensagens inválidas, grandes demais ou de origem desconhecida são recusadas.
- [x] Domínios extras em `%LOCALAPPDATA%\NoWayYahweh\extra-domains.txt` passam a ser bloqueados via regras dinâmicas.
- [x] O app mostra status, navegadores protegidos, total de bloqueios e tempo de proteção, e registra o host para Chrome e Edge.
- [x] Nenhuma URL, domínio ou título de página é gravado ou transmitido.

**Fase 2 — IA** ❌ removida (ver §7)

**Fase 3 — Desafios** ✅ (tela; ver §8 sobre o que ela protege hoje)
- [x] "Pausar proteção…" no app passa por reflexão com versículo (20 s de espera obrigatória), escolha de duração e desafio(s); "Continuar protegido" é sempre o botão principal, e fechar a janela nunca pausa.
- [x] 6 tipos de desafio (conta, sequência, lógica, memória, ordenar peças, raciocínio em etapas), gerados aleatoriamente; os 3 últimos tipos não se repetem; cada resposta é verificada em teste recalculando-a do enunciado.
- [x] A pausa é temporária (15 min / 1 h / 4 h, com 1 / 2 / 3 desafios) e termina sozinha, mesmo sem o serviço local; "Reativar agora" encerra antes.
- [ ] Desativar a extensão no navegador também depender do desafio — exige a Fase 4/5 (publicação na loja + política de instalação forçada).

**Fase 4 — Segurança e estabilidade** ✅ (exceto forcelist — depende da publicação na loja)
- [x] Política opcional que bloqueia janelas privadas/anônimas (e Tor no Brave), aplicada e removida só com administrador (UAC); remover passa pelo desafio. Testada em chave de registro temporária; a aplicação real exige aprovação de administrador (UAC).
- [ ] Política de instalação forçada (`ExtensionInstallForcelist`) — exige extensão publicada na loja.
- [x] Verificação ao iniciar o Windows (HKCU Run, sem janela): repara o registro com os navegadores e sai. Testado executando o app em modo `--background`.
- [x] Senha de responsável opcional (PBKDF2-SHA256, 210 mil iterações, sal aleatório), pedida para pausar, liberar janelas privadas e desinstalar.
- [x] Verificação de integridade no app (executável, registro de cada navegador, resposta ao `ping`).
- [x] Desinstalação pelo app (reflexão + 3 desafios + senha) e guia manual que funciona sem app e sem senha: [RECUPERACAO.md](RECUPERACAO.md).

**Fase 5 — Distribuição** ✅ (instalador; loja e atualização online ficam para depois)
- [x] Instalador Inno Setup por usuário, sem admin (`installer/NoWayYahweh.iss`, `tools/build-installer.ps1`): app + serviço autocontidos (.NET 10), extensão, ícone, Menu Iniciar, entrada em "Aplicativos instalados"; registra nos navegadores ao terminar.
- [x] Atualização por cima mantendo os dados do usuário.
- [x] Desinstalar pelo Windows passa pela reflexão + 3 desafios (+ senha); cancelar mantém tudo. Concluir remove registro, inicialização, política e dados; o desinstalador apaga os arquivos.
- [x] Testado em ciclo completo: `tests/installer/installer.test.ps1` (22 verificações; restaura o registro e o manifesto anteriores).
- [ ] Extensão publicada na Chrome Web Store (+ `ExtensionInstallForcelist`) — planejado.
- [ ] Atualização online das listas — precisa de um servidor/endereço de publicação; hoje a lista vem no instalador e os domínios extras são locais.
- [ ] Assinatura de código — sem certificado, o Windows SmartScreen avisa na primeira execução do instalador.

## 7. Fase 2 — filtro de imagens: implementado, medido e removido

**O que foi feito.** Imagens e vídeos em qualquer site ficavam borrados até um classificador local
(MobileNetV2 do GantMan/nsfw_model, MIT, em ONNX Runtime; ~12 ms por imagem, 114 MB) liberá-los.
Só pixels iam ao host; nada era gravado. Passou nos testes automatizados no Edge e no Brave.

**Por que saiu (2026-10-30).** Em uso real (redes sociais, vídeo, mensageiros web) os contadores
anônimos mostraram que a parte técnica funcionava — centenas de classificações, zero erros — mas
**cerca de 6% dos vereditos eram "explícito" em conteúdo comum** (rostos de perto, vídeos de pessoas
falando, artes). Nos vídeos, um quadro desses travava o borrão. Mesmo após corrigir o travamento,
o resultado continuou incômodo e o projeto voltou à ideia principal: **só bloquear sites**.

**Lições para uma eventual volta.**
- Medir falsos positivos com fotos comuns de **pessoas** antes de lançar; papéis de parede não bastam.
- Comparar modelos (ex.: ViT do AdamCodd, Apache-2.0, mais pesado e mais preciso) com o mesmo conjunto.
- Sites que redesenham o DOM apagam atributos alheios: o veredito precisa viver na extensão, não só no atributo.
- Ambientes isolados (sandbox) podem virtualizar o registro e o AppData: o que um teste grava lá pode não chegar ao navegador real. Confira a integração no sistema real com `tools/diagnose.ps1`.

## 8. Fase 3 — tela de pausa (o que protege hoje)

**Fluxo.** App → "Pausar proteção…" → reflexão (versículo BLIVRE + reflexão, 20 s) → duração →
desafio(s) → `state.json` recebe `PausedUntil`. O host devolve `pausedUntil` no `hello`; a extensão
sincroniza a cada 1 minuto, desliga o conjunto de regras `keyword` e os domínios extras, e agenda a
volta automática (alarme) — que acontece mesmo se o host sumir.

**O que a pausa libera.** Só as proteções secundárias: palavra no nome do site e domínios extras.
A lista de sites pornográficos conhecidos (`adult_domains`) nunca é pausada — decisão de projeto: não há motivo legítimo para precisar deles.

**Limite honesto.** Enquanto a extensão estiver no modo de desenvolvedor, ela pode ser desligada em
`brave://extensions` sem passar pelo desafio. Fechar esse caminho exige publicar a extensão na Chrome
Web Store e gravar a política `ExtensionInstallForcelist` (admin) — planejado para depois.

**Por que dois conjuntos de regras.** Desligar uma única regra estática (`updateStaticRules`)
foi aceito pela API mas não teve efeito no Edge/Brave 156 para a regra regex; ligar/desligar o conjunto
inteiro (`updateEnabledRulesets`) funciona e está coberto pelo teste E2E.

## 9. Fase 4 — contornos comuns (matriz honesta)

| Contorno | Situação | Como |
|---|---|---|
| Subdomínio, `www`, redirecionamento, conteúdo embutido de site da lista | **Bloqueado** | Regras DNR; testado no E2E (Brave e Edge). |
| Site adulto desconhecido com "porn"/"hentai" no nome | **Bloqueado** | Regra de palavra-chave; testado no E2E. |
| Janela privada/anônima/Tor | **Bloqueável** (opcional) | Política de janelas privadas (admin). Sem ela, depende de "Permitir em modo privado" na extensão. |
| Desligar a extensão em `brave://extensions` | **Não impedido** | Só com publicação na loja + `ExtensionInstallForcelist`. Limite conhecido e aceito por ora. |
| Outro navegador (Firefox, Opera) | **Não coberto** | A extensão é Chromium; ver §2. |
| Editar `state.json` para pausar ou apagar a senha | **Possível** | Arquivo do próprio usuário; é atrito, não tranca (documentado em RECUPERACAO.md). |
| Remover chaves de registro / desinstalar manualmente | **Possível, por design** | O dono sempre recupera o controle. |
| Sem internet / serviço local parado | **Bloqueio continua** | Regras vivem no navegador; o "!" no ícone avisa que o serviço sumiu. |

**Nota de compatibilidade (2026-10-30).** O Brave procura o serviço local na própria chave
(`BraveSoftwareBrave-Browser`) e, pelo comportamento observado em teste, também na do Chrome: com a
chave do Chrome apontando para um manifesto inexistente, o Brave não encontrou o serviço. O app sempre
registra as três chaves; testes que mexem no registro restauram os valores anteriores.

## 10. Fase 5 — distribuição

- **Ícone**: uma cruz partindo uma cauda de súcubo (ponta de coração), minimalista, gerado por
  `tools/icon/IconGen.cs` (vetor → PNGs da extensão, `app.ico` com 7 tamanhos, `docs/icon-512.png`).
- **Versão única**: `src/Directory.Build.props`, `extension/manifest.json` e `installer/NoWayYahweh.iss`;
  o script de build recusa versões diferentes.
- **Local**: `%LOCALAPPDATA%\Programs\NoWayYahweh` (por usuário, sem admin). A extensão fica em
  `...\extension` e é carregada a partir dali no modo de desenvolvedor (ID fixo pela chave do manifesto).
- **Tamanho**: ~44 MB (o runtime .NET vai junto, para o navegador conseguir iniciar o serviço em qualquer PC).
