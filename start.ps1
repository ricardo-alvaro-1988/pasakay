$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Start-Window($title, $workDir, $command) {
    Start-Process powershell -WorkingDirectory $workDir -ArgumentList @(
        '-NoExit',
        '-NoProfile',
        '-Command',
        "Write-Host '$title'; $command"
    )
}

Start-Window 'Ya! Pasakay API' "$root\backend" 'dotnet run --project YaPasakay.Api --launch-profile http'
Start-Sleep -Seconds 2
Start-Window 'Ya! Pasakay Operator' "$root\web\admin" 'npm run dev'
Start-Window 'Ya! Pasakay Customer' "$root\web\customer" 'npm run dev'

Start-Sleep -Seconds 4
Start-Process 'http://127.0.0.1:5174'
Start-Process 'http://127.0.0.1:5174/ops/'

Write-Host 'Started:'
Write-Host '  Customer            http://127.0.0.1:5174/'
Write-Host '  Operator / Admin    http://127.0.0.1:5174/ops/'
Write-Host '  API                 http://localhost:5088'
Write-Host 'OTP is 1234. Customer test: 09181110001 Rico. Rider test: 09181110003 Ana (tricycle).'
