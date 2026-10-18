param(
    [string]$Bundletool = "$PSScriptRoot/../build/verification/bundletool.jar",
    [string]$MobileToolsRoot = 'C:/repositories/mobile-tools'
)
$ErrorActionPreference = 'Stop'
if (Test-Path "$MobileToolsRoot/Use-MobileTools.ps1") { . "$MobileToolsRoot/Use-MobileTools.ps1" }
$appRoot = Split-Path $PSScriptRoot -Parent
$releaseDir = Join-Path $appRoot 'releases/1.0.0'
$bundle = Join-Path $releaseDir 'trygoride-passenger-1.0.0.aab'
if (!(Test-Path $Bundletool)) { throw 'Provide the official bundletool-all jar using -Bundletool.' }
$signature = & jarsigner -verify $bundle 2>&1
if ($LASTEXITCODE -ne 0 -or ($signature -join "`n") -notmatch 'jar verified') { throw 'AAB signature verification failed' }
$actualCertificate = & keytool -printcert -jarfile $bundle
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect AAB signing certificate' }
$expectedCertificate = & keytool -printcert -file "$releaseDir/upload-certificate.pem"
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect upload certificate' }
$actualFingerprint = ($actualCertificate | Select-String 'SHA256:').ToString().Trim()
$expectedFingerprint = ($expectedCertificate | Select-String 'SHA256:').ToString().Trim()
if ($actualFingerprint -ne $expectedFingerprint) { throw 'Bundle is not signed by the documented upload key' }
& java -jar $Bundletool validate --bundle=$bundle
if ($LASTEXITCODE -ne 0) { throw 'Bundletool validation failed' }
$manifestText = & java -jar $Bundletool dump manifest --bundle=$bundle --module=base
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect bundle manifest' }
[xml]$manifest = $manifestText -join "`n"
$android = 'http://schemas.android.com/apk/res/android'
if ($manifest.manifest.package -ne 'com.trygoride.passenger') { throw 'Wrong application ID' }
if ($manifest.manifest.GetAttribute('versionName', $android) -ne '1.0.0') { throw 'Wrong version name' }
if ($manifest.manifest.GetAttribute('versionCode', $android) -ne '1') { throw 'Wrong version code' }
if ($manifest.manifest.application.GetAttribute('label', $android) -ne 'TryGoRide') { throw 'Wrong app label' }
if ($manifest.manifest.application.GetAttribute('debuggable', $android) -eq 'true') { throw 'App is debuggable' }
$maps = $manifest.manifest.application.'meta-data' | Where-Object { $_.GetAttribute('name', $android) -eq 'com.google.android.geo.API_KEY' }
if (!$maps -or [string]::IsNullOrWhiteSpace($maps.GetAttribute('value', $android))) { throw 'Missing Maps API key' }
$expectedHash = (Get-Content "$releaseDir/SHA256SUMS.txt").Split(' ')[0]
if ((Get-FileHash $bundle -Algorithm SHA256).Hash.ToLower() -ne $expectedHash) { throw 'AAB checksum mismatch' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($bundle)
try {
    foreach ($asset in @('app-icon.png', 'logo-circle.png')) {
        $entry = $zip.GetEntry("base/assets/flutter_assets/assets/$asset")
        if (!$entry) { throw "Missing bundled brand asset: $asset" }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $assetHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose(); $stream.Dispose() }
        if ($assetHash -ne (Get-FileHash "$appRoot/assets/$asset" -Algorithm SHA256).Hash) {
            throw "Bundled brand asset does not match the verified source: $asset"
        }
    }
} finally { $zip.Dispose() }
Write-Output 'PASS: bundle structure, signature, upload certificate, package, version, app name, release mode, Maps key presence, checksum, bundled TryGoRide logo assets.'
Write-Output $actualFingerprint
