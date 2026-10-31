# Recuperação e desinstalação

O No way? Yahweh! é **uma pausa, não uma prisão**. O dono do computador sempre pode recuperar o
controle. Este guia funciona mesmo que a senha de responsável tenha sido perdida ou que o app não
abra mais.

## Caminho normal (pelo app)

1. Abra o app → **Configurações e segurança** → **Desinstalar…**
2. Passe pela reflexão (20 s), por 3 desafios e, se houver, pela senha de responsável.
3. O app remove: o registro com os navegadores, a verificação ao iniciar o Windows, a política de
   janelas privadas (pede administrador) e a pasta de dados.
4. Remova a extensão em `brave://extensions` (ou `chrome://extensions`, `edge://extensions`).
5. Apague a pasta do programa.

## Caminho manual (sem o app, sem a senha)

Requer apenas acesso de administrador ao próprio computador. Use o Editor do Registro (`regedit`)
ou um PowerShell **como administrador**.

**1. Política de janelas privadas** (só existe se foi ativada) — apague estes *valores* (não as chaves):

| Chave | Valor |
|---|---|
| `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\BraveSoftware\Brave` | `IncognitoModeAvailability`, `TorDisabled` |
| `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Google\Chrome` | `IncognitoModeAvailability` |
| `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge` | `InPrivateModeAvailability` |

**2. Integração com os navegadores** — apague estas chaves:

- `HKEY_CURRENT_USER\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.nowayyahweh.host`
- `HKEY_CURRENT_USER\Software\Google\Chrome\NativeMessagingHosts\com.nowayyahweh.host`
- `HKEY_CURRENT_USER\Software\Microsoft\Edge\NativeMessagingHosts\com.nowayyahweh.host`

**3. Verificação ao iniciar o Windows** — apague o valor `NoWayYahweh` em
`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`.

**4. Dados** — apague a pasta `%LOCALAPPDATA%\NoWayYahweh` (contadores, pausa, senha, domínios extras).

**5. Extensão** — remova em `brave://extensions` / `chrome://extensions` / `edge://extensions`.

Reinicie o navegador. Pronto: nada do No way? Yahweh! fica ativo.

## Só perdi a senha de responsável

Apague a linha `"GuardPasswordHash": ...` de `%LOCALAPPDATA%\NoWayYahweh\state.json` (com o app
fechado) ou apague o arquivo inteiro — os contadores recomeçam do zero. A senha é um ponto de
atrito e de conversa com quem a guarda, não uma tranca absoluta.

## Diagnóstico

`tools\diagnose.ps1` (PowerShell normal) mostra o registro de cada navegador, o executável e se o
serviço local responde. No app: **Verificar integridade**.
