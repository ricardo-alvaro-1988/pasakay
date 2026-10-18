param([string]$MobileToolsRoot = 'C:/repositories/mobile-tools')
$ErrorActionPreference = 'Stop'
$appRoot = Split-Path $PSScriptRoot -Parent
if (Test-Path "$MobileToolsRoot/Use-MobileTools.ps1") {
    . "$MobileToolsRoot/Use-MobileTools.ps1"
}
Push-Location $appRoot
try {
    if (!(Test-Path android/key.properties)) { throw 'Provide android/key.properties with the upload keystore credentials first.' }
    & flutter pub get
    if ($LASTEXITCODE -ne 0) { throw 'Flutter dependency resolution failed' }
    & flutter build appbundle --release --build-name=1.0.0 --build-number=1 --dart-define=API_BASE=https://trygoride.com
    if ($LASTEXITCODE -ne 0) { throw 'Flutter app bundle build failed' }
    $destination = Join-Path $appRoot 'releases/1.0.0'
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item build/app/outputs/bundle/release/app-release.aab "$destination/trygoride-passenger-1.0.0.aab"
    Get-FileHash "$destination/trygoride-passenger-1.0.0.aab" -Algorithm SHA256 |
        ForEach-Object { "$($_.Hash.ToLower())  trygoride-passenger-1.0.0.aab" } |
        Set-Content "$destination/SHA256SUMS.txt"
} finally { Pop-Location }
