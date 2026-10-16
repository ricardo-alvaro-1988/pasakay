# Download native SQL backups over SSH; delete server copies only after verification.
# Requires Windows OpenSSH and authorized SSH access to the backup server.
[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9_.-]+@[A-Za-z0-9.-]+$')]
    [string]$Server = 'root@yapasakay.com',
    [ValidatePattern('^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$')]
    [string]$Database,
    [switch]$ListOnly,
    [switch]$KeepServerCopies
)

$ErrorActionPreference = 'Stop'
foreach ($command in @('ssh.exe', 'scp.exe')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Install Windows OpenSSH Client first: missing $command"
    }
}

Write-Host 'Scanning all database backup weeks; no date selection is needed.'
Write-Host "Local folders: $PSScriptRoot\<backup-week>\<database>"
Write-Host 'Only completed backups and metadata older than 30 minutes are included.'
Write-Host 'SSH access is required. No password or private key is stored in this script.'
if (-not $ListOnly -and -not $KeepServerCopies) {
    Write-Host 'Server backup and JSON files will be deleted ONLY after local verification.'
}

# Discover every managed database/week folder; no user input enters remote code.
$remoteScript = @'
import json,re,time
from pathlib import Path
root=Path('/srv/db-backups')
cutoff=time.time()-1800
files=[]
skipped=0
for folder in sorted(root.glob('*/*')):
    db=folder.parent
    if not db.is_dir() or db.is_symlink() or not re.fullmatch(r'[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}',db.name):
        continue
    if not folder.is_dir() or folder.is_symlink():
        continue
    week=folder.name
    if not re.fullmatch(r'\d{4}-\d{2}-\d{2}_to_\d{4}-\d{2}-\d{2}',week):
        continue
    for meta in sorted(folder.glob('*.json')):
        if meta.is_symlink():
            raise RuntimeError('Unexpected metadata symlink')
        entry=json.loads(meta.read_text())
        filename=entry['filename']
        if not re.fullmatch(r'(full|diff|log)_[A-Za-z0-9_]+\.(bak|trn)',filename):
            raise RuntimeError('Unexpected backup filename')
        backup=folder/filename
        if backup.is_symlink():
            raise RuntimeError('Unexpected backup symlink: '+str(backup))
        if not backup.exists():
            # A prior deletion may have stopped between backup and JSON removal.
            files.append(dict(database=db.name,week=week,filename=filename,path=str(backup),
                              sha256=entry['sha256'],bytes=entry['bytes'],missing_remote=True))
            continue
        if not backup.is_file():
            raise RuntimeError('Unexpected backup file type')
        if max(backup.stat().st_mtime,meta.stat().st_mtime)>cutoff:
            skipped+=1
            continue
        if not re.fullmatch(r'[0-9a-fA-F]{64}',entry['sha256']):
            raise RuntimeError('Invalid checksum metadata')
        if backup.stat().st_size!=entry['bytes'] or meta.name!=filename+'.json':
            raise RuntimeError('Backup metadata mismatch')
        files.append(dict(database=db.name,week=week,filename=filename,path=str(backup),
                          sha256=entry['sha256'],bytes=entry['bytes']))
print(json.dumps(dict(files=files,too_recent=skipped)))
'@
$raw = $remoteScript | & ssh.exe -o ConnectTimeout=20 $Server 'python3 -'
if ($LASTEXITCODE -ne 0) { throw 'Could not list server backups. Check SSH access and the server address.' }
$manifest = ($raw -join "`n") | ConvertFrom-Json
$files = @($manifest.files)
if ($Database) { $files = @($files | Where-Object { $_.database -eq $Database }) }
Write-Host ("Eligible backups: {0}; newer than 30 minutes: {1}" -f $files.Count, $manifest.too_recent)
if ($files.Count -eq 0) {
    Write-Host 'No eligible backup files. No local files were changed.'
    return
}
if ($ListOnly) {
    $files | Select-Object week, database, filename, bytes | Format-Table -AutoSize
    return
}

function Ensure-LocalFolder([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        $item = Get-Item -LiteralPath $Path -Force
        if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Destination must be a normal directory: $Path"
        }
    } else {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Test-Backup([string]$Path, $Entry) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing linked file: $Path" }
    if ($item.Length -ne [long]$Entry.bytes) { return $false }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Entry.sha256
}

$downloaded = 0
$existing = 0
$failures = 0
$deleted = 0
foreach ($entry in $files) {
    $temporaryBackup = $null
    $temporaryMetadata = $null
    try {
        if ($entry.database -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$' -or
            $entry.week -notmatch '^\d{4}-\d{2}-\d{2}_to_\d{4}-\d{2}-\d{2}$' -or
            $entry.filename -notmatch '^(full|diff|log)_[A-Za-z0-9_]+\.(bak|trn)$') {
            throw 'Invalid manifest filename.'
        }
        $week = $entry.week
        $destination = Join-Path $PSScriptRoot $week
        Ensure-LocalFolder $destination
        $remotePath = "/srv/db-backups/$($entry.database)/$week/$($entry.filename)"
        if ($entry.path -ne $remotePath) { throw 'Unexpected manifest path.' }
        $folder = Join-Path $destination $entry.database
        Ensure-LocalFolder $folder
        $localFile = Join-Path $folder $entry.filename
        $localMetadata = "$localFile.json"
        $alreadyVerified = Test-Backup $localFile $entry
        if ($entry.missing_remote -and -not $alreadyVerified) {
            throw 'Server backup is missing and no verified local copy exists; metadata retained for review.'
        }
        if ((Test-Path -LiteralPath $localFile) -and -not $alreadyVerified) {
            throw "Existing local file differs from server checksum; preserved for review: $localFile"
        }
        if (Test-Path -LiteralPath $localMetadata) {
            if ((Get-Item -LiteralPath $localMetadata -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Refusing linked metadata file.'
            }
        }
        $suffix = '.' + [guid]::NewGuid().ToString('N') + '.partial'
        $temporaryMetadata = $localMetadata + $suffix
        & scp.exe -q -o ConnectTimeout=20 "${Server}:$remotePath.json" $temporaryMetadata
        if ($LASTEXITCODE -ne 0) { throw 'Metadata download failed.' }
        $metadata = Get-Content -LiteralPath $temporaryMetadata -Raw | ConvertFrom-Json
        if ($metadata.filename -ne $entry.filename -or $metadata.sha256 -ne $entry.sha256 -or
            [long]$metadata.bytes -ne [long]$entry.bytes) { throw 'Downloaded metadata mismatch.' }
        if (-not $alreadyVerified) {
            $temporaryBackup = $localFile + $suffix
            Write-Host "Downloading $($entry.database)/$($entry.filename)"
            & scp.exe -q -o ConnectTimeout=20 "${Server}:$remotePath" $temporaryBackup
            if ($LASTEXITCODE -ne 0) { throw 'Backup download failed.' }
            if (-not (Test-Backup $temporaryBackup $entry)) { throw 'Downloaded backup checksum mismatch.' }
            Move-Item -LiteralPath $temporaryBackup -Destination $localFile
            $downloaded++
        } else {
            $existing++
        }
        Move-Item -LiteralPath $temporaryMetadata -Destination $localMetadata -Force
        Write-Host "Verified: $($entry.database)/$($entry.filename)"
        if (-not $KeepServerCopies) {
            foreach ($savedPath in @($localFile, $localMetadata)) {
                $savedStream = [IO.File]::Open($savedPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::Read)
                try { $savedStream.Flush($true) } finally { $savedStream.Dispose() }
            }
            # Re-read final local files, not just the temporary downloads.
            if (-not (Test-Backup $localFile $entry)) { throw 'Final local backup verification failed; server copy retained.' }
            $savedMetadata = Get-Content -LiteralPath $localMetadata -Raw | ConvertFrom-Json
            if ($savedMetadata.filename -ne $entry.filename -or $savedMetadata.sha256 -ne $entry.sha256 -or
                [long]$savedMetadata.bytes -ne [long]$entry.bytes) {
                throw 'Final local metadata verification failed; server copy retained.'
            }
            $request = @{ database=$entry.database; week=$week; filename=$entry.filename;
                sha256=$entry.sha256; bytes=[long]$entry.bytes }
            $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json -Compress)))
            $deleteScript = @'
import base64,hashlib,json,re,time,fcntl
from pathlib import Path
r=json.loads(base64.b64decode('__REQUEST__'))
root=Path('/srv/db-backups')
assert re.fullmatch(r'[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}',r['database'])
assert re.fullmatch(r'\d{4}-\d{2}-\d{2}_to_\d{4}-\d{2}-\d{2}',r['week'])
assert re.fullmatch(r'(full|diff|log)_[A-Za-z0-9_]+\.(bak|trn)',r['filename'])
folder=root/r['database']/r['week']
assert root.resolve()==root and not folder.parent.is_symlink() and not folder.is_symlink()
assert folder.resolve()==folder and folder.parent.parent==root
backup=folder/r['filename']
meta=folder/(r['filename']+'.json')
# Coordinate deletion by multiple downloader instances without touching backup state.
with open('/var/lib/sql-backup-manager/download-delete.lock','a') as lock:
    fcntl.flock(lock,fcntl.LOCK_EX)
    if not backup.exists() and not meta.exists():
        print('ALREADY_REMOVED')
    else:
        assert not backup.is_symlink() and not meta.is_symlink()
        assert meta.is_file(), 'Missing remote metadata; no deletion'
        data=json.loads(meta.read_text())
        assert all(data[k]==r[k] for k in ('filename','sha256','bytes')), 'Remote metadata changed'
        assert meta.stat().st_mtime <= time.time()-1800, 'Metadata too recent'
        if backup.exists():
            assert backup.is_file() and backup.stat().st_size==r['bytes']
            assert backup.stat().st_mtime <= time.time()-1800, 'Backup too recent'
            with backup.open('rb') as stream:
                assert hashlib.file_digest(stream,'sha256').hexdigest().lower()==r['sha256'].lower(), 'Remote checksum changed'
            backup.unlink()
        # Also permits retry after interruption between deleting the backup and JSON.
        meta.unlink()
        print('DELETED_VERIFIED_PAIR')
'@
            $deleteScript = $deleteScript.Replace('__REQUEST__', $encoded)
            $deleteResult = $deleteScript | & ssh.exe -o ConnectTimeout=20 $Server 'python3 -'
            if ($LASTEXITCODE -ne 0) { throw 'Server deletion could not be confirmed; local copies remain safe. Review server files.' }
            if (($deleteResult -join '').Trim() -notin @('DELETED_VERIFIED_PAIR','ALREADY_REMOVED')) {
                throw 'Unexpected deletion response; check server files.'
            }
            $deleted++
            Write-Host "Removed verified server copy: $($entry.database)/$($entry.filename)"
        }
    } catch {
        $failures++
        Write-Warning $_.Exception.Message
        # Partial downloads remain for inspection; they never replace valid backups.
    }
}
Write-Host "Finished. Downloaded: $downloaded; already verified: $existing; server pairs removed: $deleted; failed: $failures"
Write-Host "Saved under: $PSScriptRoot\<backup-week>\<database>"
if ($KeepServerCopies) { Write-Host 'Server files were not deleted.' }
Write-Host 'Preserve your local full backup and required differential/log chain. Keep another copy on separate storage.'
if ($failures -gt 0) { throw "$failures backup(s) failed. Review warnings and rerun." }
