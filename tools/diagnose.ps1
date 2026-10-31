# No way? Yahweh! - diagnostico da integracao com os navegadores.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\diagnose.ps1
# Verifica o registro de cada navegador, o manifesto e o executavel, e conversa com o host
# exatamente como o navegador faz (hello). Nao altera nada.
$ErrorActionPreference = 'Stop'
$origin = 'chrome-extension://olipaknkbpadbdcdaaehfnpolcdeieee/'
$keys = @{
    chrome = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.nowayyahweh.host'
    edge   = 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.nowayyahweh.host'
    brave  = 'HKCU:\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.nowayyahweh.host'
}
$manifestPath = $null
foreach ($b in $keys.Keys) {
    if (Test-Path $keys[$b]) { $manifestPath = (Get-Item $keys[$b]).GetValue(''); "[ok]   registro $b -> $manifestPath" }
    else { "[FALHA] registro $b ausente (abra o app ou use 'Reparar integracao')" }
}
if (-not $manifestPath -or -not (Test-Path $manifestPath)) { "[FALHA] manifesto nao encontrado"; exit 1 }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if (Test-Path $manifest.path) { "[ok]   host: $($manifest.path)" } else { "[FALHA] host nao existe: $($manifest.path)"; exit 1 }

function Frame([string]$json) {
    $body = [Text.Encoding]::UTF8.GetBytes($json)
    [BitConverter]::GetBytes([int]$body.Length) + $body
}
$psi = New-Object Diagnostics.ProcessStartInfo $manifest.path, $origin
$psi.UseShellExecute = $false; $psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
$p = [Diagnostics.Process]::Start($psi)
$in = $p.StandardInput.BaseStream; $out = $p.StandardOutput.BaseStream
foreach ($msg in @('{"type":"hello","browser":"brave","version":"0.0.0"}')) {
    $f = Frame $msg; $in.Write($f, 0, $f.Length); $in.Flush()
    $len = New-Object byte[] 4; [void]$out.Read($len, 0, 4); $n = [BitConverter]::ToInt32($len, 0)
    $buf = New-Object byte[] $n; $read = 0; while ($read -lt $n) { $read += $out.Read($buf, $read, $n - $read) }
    "[resp] " + [Text.Encoding]::UTF8.GetString($buf)
}
$in.Close(); [void]$p.WaitForExit(5000)
$err = $p.StandardError.ReadToEnd(); if ($err) { "[stderr] $err" }
"[ok]   host encerrou com codigo $($p.ExitCode)"
