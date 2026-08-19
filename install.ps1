param(
    [switch]$SkipStart
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Need($name, $check) {
    if (-not $check) {
        throw "Missing $name. Install it first, then run Install.bat again."
    }
}

Need 'Node.js (npm)' (Get-Command npm -ErrorAction SilentlyContinue)
Need '.NET SDK' (Get-Command dotnet -ErrorAction SilentlyContinue)

Write-Host 'Restoring API...'
dotnet restore "$root\backend\YaPasakay.Api\YaPasakay.Api.csproj" | Out-Host

Write-Host 'Installing operator portal...'
Push-Location "$root\web\admin"
npm install
Pop-Location

Write-Host 'Installing customer web...'
Push-Location "$root\web\customer"
npm install
Pop-Location

$flutter = 'C:\Users\RicardoAlvaro\flutter\bin\flutter.bat'
if (-not (Test-Path $flutter)) {
    $cmd = Get-Command flutter -ErrorAction SilentlyContinue
    if ($cmd) { $flutter = $cmd.Source }
}
if ((Test-Path $flutter) -and (Test-Path "$root\mobile\rider\pubspec.yaml")) {
    Write-Host 'Installing Flutter rider app...'
    Push-Location "$root\mobile\rider"
    & $flutter pub get
    Pop-Location
}

Write-Host ''
Write-Host 'Install finished.'
if (-not $SkipStart) {
    Write-Host 'Starting API, operator portal, and customer web...'
    & "$root\start.ps1"
}
