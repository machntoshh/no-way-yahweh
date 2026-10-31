# Installer lifecycle test: install (silent) -> check files, uninstall entry, browser registration and
# host -> upgrade over it keeping user data -> uninstall attempt is gated by the challenge window
# (cancelling keeps everything) -> manual cleanup.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tests\installer\installer.test.ps1
# ATENÇÃO: instala de verdade para o usuário atual; rode num ambiente de teste.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$setup = Get-ChildItem "$root\artifacts\installer\NoWayYahweh-Setup-*.exe" | Sort-Object LastWriteTime | Select-Object -Last 1
$app = "$env:LOCALAPPDATA\Programs\NoWayYahweh"
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8B1F7C2E-5D4A-4E8B-9C3F-2A6D1E0B7F45}_is1'
if (Test-Path $app) {
    # A real installation (and the browser files it has, like extension\_metadata) would mix into the results.
    Write-Host "Já existe uma instalação em $app. Rode este teste num ambiente limpo (máquina virtual / Windows Sandbox)."
    exit 2
}
$failures = 0
# The installer re-points the real per-user browser keys; put the previous values back at the end.
$browserKeys = 'Google\Chrome', 'Microsoft\Edge', 'BraveSoftware\Brave-Browser' | ForEach-Object { "HKCU:\Software\$_\NativeMessagingHosts\com.nowayyahweh.host" }
$previous = @{}; foreach ($k in $browserKeys) { $previous[$k] = if (Test-Path $k) { (Get-Item $k).GetValue('') } else { $null } }
$hostManifest = "$env:LOCALAPPDATA\NoWayYahweh\com.nowayyahweh.host.json"
$previousManifest = if (Test-Path $hostManifest) { [IO.File]::ReadAllBytes($hostManifest) } else { $null }
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$previousRun = (Get-ItemProperty $runKey -Name NoWayYahweh -ErrorAction SilentlyContinue).NoWayYahweh
function Check([string]$name, [bool]$ok) {
    if ($ok) { "ok      $name" } else { "FALHOU  $name"; $script:failures++ }
}
function Install { (Start-Process $setup.FullName '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru -Wait).ExitCode }

Check 'instalação silenciosa termina com código 0' ((Install) -eq 0)
foreach ($f in 'NoWayYahweh.exe', 'NoWayYahweh.Host.exe', 'extension\manifest.json', 'extension\rules\adult_domains.json',
               'extension\rules\keyword.json', 'extension\verses.json', 'extension\icons\icon-128.png', 'docs\RECUPERACAO.md',
               'docs\PRIVACIDADE.md', 'LICENSE.txt', 'THIRD-PARTY-NOTICES.md', 'licenses\dotnet-runtime-THIRD-PARTY-NOTICES.txt', 'unins000.exe') {
    Check "arquivo instalado: $f" (Test-Path "$app\$f")
}
$startMenu = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
Check 'atalho do app no Menu Iniciar' (Test-Path -LiteralPath "$startMenu\No way$([char]0xFF1F) Yahweh!.lnk")
Check 'atalho da pasta da extensão no Menu Iniciar' (Test-Path -LiteralPath "$startMenu\No way$([char]0xFF1F) Yahweh! - pasta da extensão.lnk")
Check 'pasta _metadata do navegador não é copiada' (-not (Test-Path "$app\extension\_metadata"))
Check 'entrada em "Aplicativos instalados"' (Test-Path $uninstallKey)
Check 'versão registrada é a do produto' ((Get-ItemProperty $uninstallKey).DisplayVersion -eq '1.0.0')

$host_ = "$app\NoWayYahweh.Host.exe"
foreach ($key in 'Google\Chrome', 'Microsoft\Edge', 'BraveSoftware\Brave-Browser') {
    $manifestPath = (Get-Item "HKCU:\Software\$key\NativeMessagingHosts\com.nowayyahweh.host").GetValue('')
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    Check "registro em $key aponta para o host instalado" ($manifest.path -eq $host_)
}

# The installed host answers like a browser would talk to it.
$psi = New-Object Diagnostics.ProcessStartInfo $host_, 'chrome-extension://olipaknkbpadbdcdaaehfnpolcdeieee/'
$psi.UseShellExecute = $false; $psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true
$p = [Diagnostics.Process]::Start($psi)
$body = [Text.Encoding]::UTF8.GetBytes('{"type":"ping"}'); $frame = [BitConverter]::GetBytes([int]$body.Length) + $body
$p.StandardInput.BaseStream.Write($frame, 0, $frame.Length); $p.StandardInput.BaseStream.Flush()
$len = New-Object byte[] 4; [void]$p.StandardOutput.BaseStream.Read($len, 0, 4)
$buf = New-Object byte[] ([BitConverter]::ToInt32($len, 0)); [void]$p.StandardOutput.BaseStream.Read($buf, 0, $buf.Length)
$p.StandardInput.Close(); [void]$p.WaitForExit(5000)
Check 'host instalado responde ao ping' ([Text.Encoding]::UTF8.GetString($buf) -eq '{"type":"pong"}')

# Upgrade over the existing install keeps user data.
$state = "$env:LOCALAPPDATA\NoWayYahweh\state.json"
$marker = Test-Path $state
Check 'atualização por cima termina com código 0' ((Install) -eq 0)
Check 'atualização mantém os dados do usuário' ((Test-Path $state) -eq $marker)
Check 'app continua instalado após atualização' (Test-Path "$app\NoWayYahweh.exe")

# Uninstalling opens the gate (reflection + challenges); cancelling keeps everything.
$uninstaller = Start-Process "$app\unins000.exe" '/VERYSILENT', '/SUPPRESSMSGBOXES' -PassThru
$gate = $null
for ($i = 0; $i -lt 40 -and -not $gate; $i++) {
    Start-Sleep -Milliseconds 500
    $gate = Get-Process NoWayYahweh -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like 'Uma pausa*' } | Select-Object -First 1
}
Check 'desinstalar abre a tela de pausa' ($null -ne $gate)
if ($gate) { [void]$gate.CloseMainWindow(); [void]$gate.WaitForExit(10000) }
[void]$uninstaller.WaitForExit(20000)
Check 'cancelar a tela de pausa cancela a desinstalação' ((Test-Path "$app\NoWayYahweh.exe") -and (Test-Path $uninstallKey))

# Cleanup for the test environment (the gate cannot be solved by a script, by design).
Get-Process NoWayYahweh, NoWayYahweh.Host -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item $app -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $startMenu -Filter 'No way* Yahweh!*.lnk' | Remove-Item -Force -ErrorAction SilentlyContinue
foreach ($k in $browserKeys) {
    if ($null -eq $previous[$k]) { Remove-Item $k -Recurse -Force -ErrorAction SilentlyContinue }
    else { New-Item $k -Force | Out-Null; Set-Item $k -Value $previous[$k] }
}
if ($null -ne $previousManifest) { [IO.File]::WriteAllBytes($hostManifest, $previousManifest) }
if ($null -eq $previousRun) { Remove-ItemProperty $runKey -Name NoWayYahweh -ErrorAction SilentlyContinue }
else { Set-ItemProperty $runKey -Name NoWayYahweh -Value $previousRun }

if ($failures) { "$failures falha(s)"; exit 1 } else { 'tudo ok' }
