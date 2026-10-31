# Mostra onde o No way? Yahweh! está instalado (ou se não está).
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\where-installed.ps1
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8B1F7C2E-5D4A-4E8B-9C3F-2A6D1E0B7F45}_is1'
if (Test-Path $key) {
    $entry = Get-ItemProperty $key
    "Instalado: versão $($entry.DisplayVersion)"
    "Pasta:     $($entry.InstallLocation)"
    "Extensão:  $(Join-Path $entry.InstallLocation 'extension')"
    "Existe:    $(Test-Path (Join-Path $entry.InstallLocation 'extension\manifest.json'))"
} else {
    'Não há instalação registrada para este usuário (o instalador ainda não foi concluído).'
}
"LOCALAPPDATA = $env:LOCALAPPDATA"
"Programs\NoWayYahweh existe: $(Test-Path "$env:LOCALAPPDATA\Programs\NoWayYahweh")"
