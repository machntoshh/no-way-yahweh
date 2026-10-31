# Builds artifacts\installer\NoWayYahweh-Setup-<version>.exe
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\build-installer.ps1 [-Iscc <caminho do ISCC.exe>]
# Ferramentas procuradas em: parâmetro / variáveis de ambiente (ISCC, DOTNET_ROOT) / PATH / locais padrão.
param([string]$Iscc = $env:ISCC)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# First dotnet that actually has an SDK (a system-wide one may have runtimes only).
$candidates = @()
if ($env:DOTNET_ROOT) { $candidates += Join-Path $env:DOTNET_ROOT 'dotnet.exe' }
$candidates += @(Get-Command dotnet -All -ErrorAction SilentlyContinue | ForEach-Object Source)
$dotnet = $candidates | Where-Object { (Test-Path $_) -and (& $_ --list-sdks) } | Select-Object -First 1
if (-not $dotnet) { throw 'Nenhum .NET SDK encontrado (precisa do .NET 10 SDK no PATH ou em DOTNET_ROOT)' }

if (-not $Iscc) {
    $Iscc = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | ForEach-Object Source),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $Iscc -or -not (Test-Path $Iscc)) { throw 'ISCC.exe não encontrado: instale o Inno Setup 6 ou informe -Iscc / variável ISCC' }

# Versions must agree before shipping.
$version = ([xml](Get-Content "$root\src\Directory.Build.props")).Project.PropertyGroup.Version
$manifest = (Get-Content "$root\extension\manifest.json" -Raw | ConvertFrom-Json).version
$iss = Select-String -Path "$root\installer\NoWayYahweh.iss" -Pattern '#define AppVersion "([^"]+)"' | ForEach-Object { $_.Matches[0].Groups[1].Value }
if ($version -ne $manifest -or $version -ne $iss) { throw "Versões diferentes: props=$version manifest=$manifest iss=$iss" }

$publish = "$root\artifacts\publish"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
foreach ($project in 'NoWayYahweh.App', 'NoWayYahweh.Host') {
    & $dotnet publish "$root\src\$project" -c Release -o $publish -p:DebugType=none --nologo
    if ($LASTEXITCODE -ne 0) { throw "publish de $project falhou" }
}

& $Iscc /Q "$root\installer\NoWayYahweh.iss"
if ($LASTEXITCODE -ne 0) { throw 'ISCC falhou' }
Get-Item "$root\artifacts\installer\NoWayYahweh-Setup-$version.exe" | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
