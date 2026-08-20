$ErrorActionPreference = 'Stop'

$deployRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $deployRoot
$apiWwwroot = Join-Path $root 'backend\YaPasakay.Api\wwwroot'
$customerRoot = Join-Path $root 'web\customer'
$adminRoot = Join-Path $root 'web\admin'
$customerDist = Join-Path $customerRoot 'dist'
$adminDist = Join-Path $adminRoot 'dist'
$opsRoot = Join-Path $apiWwwroot 'ops'

function Invoke-NpmBuild($path) {
    Push-Location $path
    try {
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) {
            throw "npm build failed in $path with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

function Remove-ChildrenExceptUploads($path) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null

    Get-ChildItem -LiteralPath $path -Force |
        Where-Object { $_.Name -ne 'uploads' } |
        Remove-Item -Recurse -Force
}

Invoke-NpmBuild $customerRoot
Invoke-NpmBuild $adminRoot

Remove-ChildrenExceptUploads $apiWwwroot

Copy-Item -Path (Join-Path $customerDist '*') -Destination $apiWwwroot -Recurse -Force
New-Item -ItemType Directory -Force -Path $opsRoot | Out-Null
Copy-Item -Path (Join-Path $adminDist '*') -Destination $opsRoot -Recurse -Force

Write-Host "Synced customer app to $apiWwwroot"
Write-Host "Synced admin app to $opsRoot"
