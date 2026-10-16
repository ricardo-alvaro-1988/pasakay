#!/usr/bin/env python3
"""Install on the existing SQL/FTPS host after test_restore.py passes."""
import datetime as dt
import grp
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
from backup_manager import Manager, TZ

source = Path(__file__).resolve().parent
start = dt.datetime(2026, 10, 10, 0, 0, tzinfo=TZ)
assert dt.datetime.now(TZ) < start, 'Review first-start time before installation'
group = grp.getgrnam('dbbackup').gr_gid
install_root = Path('/usr/local/lib/sql-backup-manager')
config_root = Path('/etc/sql-backup-manager')
state_root = Path('/var/lib/sql-backup-manager')
output = Path('/srv/db-backups')
stage = Path('/opt/sqlserver/backups/managed')
preserve = Path('/var/backups/sql-backup-manager/legacy-20261009')
assert not install_root.exists(), 'Already installed; review instead of overwriting'
assert output.resolve() == output and stage.parent.resolve() == stage.parent
assert subprocess.run(['systemctl','is-active','--quiet','sql-database-ftp-backup.service']).returncode != 0

for directory in [install_root, config_root, state_root, stage, preserve]:
    directory.mkdir(mode=0o700, parents=True, exist_ok=True)
    directory.chmod(0o700)
os.chown(stage,10001,10001)
config = dict(output_root=str(output),state_root=str(state_root),staging_host=str(stage),
              staging_container='/var/opt/mssql/backups/managed',ftp_group='dbbackup',
              sql_env='/opt/sqlserver/.env',container='sqlserver',minimum_free_gb=15,
              not_before=start.isoformat())
config_file = config_root/'config.json'
config_file.write_text(json.dumps(config,indent=2)+'\n')
config_file.chmod(0o600)
shutil.copy2(source/'backup_manager.py',install_root/'backup_manager.py')
(install_root/'backup_manager.py').chmod(0o700)
manager = Manager(config)
databases = manager.inventory()
assert len(databases) >= 14
assert all(d['state_desc']=='ONLINE' and not d['is_read_only'] for d in databases)
assert not any(d['name'].startswith('CodexBackupVerify_') for d in databases), 'Disposable test database still present'
assert manager.staging.stat().st_dev == manager.root.stat().st_dev

for name in ['sql-backup-cycle.service','sql-backup-cycle.timer','sql-backup-start.service','sql-backup-start.timer']:
    target = Path('/etc/systemd/system')/name
    assert not target.exists(), 'Existing unit requires review: '+name
    shutil.copy2(source/name,target)
    target.chmod(0o644)
subprocess.run(['systemd-analyze','verify',*[str(Path('/etc/systemd/system')/n) for n in
               ['sql-backup-cycle.service','sql-backup-cycle.timer','sql-backup-start.service','sql-backup-start.timer']]],check=True)

# Permit manual removal in database/week folders after checksum-verified download.
# Keep uploads, renames and chmod disabled. The FTP chroot root remains non-writable.
ftp = Path('/etc/vsftpd.conf')
shutil.copy2(ftp,preserve/'vsftpd.conf.before')
text = ftp.read_text()
text = re.sub(r'^write_enable=.*$', 'write_enable=YES', text, flags=re.M)
denied = {'STOR','STOU','APPE','RNFR','RNTO','MKD','XMKD','SITE'}
match = re.search(r'^cmds_denied=(.*)$',text,re.M)
if match:
    denied.update(match.group(1).split(','))
    text = text[:match.start()]+'cmds_denied='+','.join(sorted(denied))+text[match.end():]
else:
    text += '\ncmds_denied='+','.join(sorted(denied))+'\n'
ftp.write_text(text)
try:
    subprocess.run(['systemctl','restart','vsftpd'],check=True)
    subprocess.run(['systemctl','is-active','--quiet','vsftpd'],check=True)
except Exception:
    shutil.copy2(preserve/'vsftpd.conf.before',ftp)
    subprocess.run(['systemctl','restart','vsftpd'])
    raise

subprocess.run(['systemctl','disable','--now','sql-database-ftp-backup.timer'],check=True)
shutil.copy2('/usr/local/sbin/sql-database-ftp-backup.sh',preserve/'sql-database-ftp-backup.sh.before')
shutil.copy2('/etc/systemd/system/sql-database-ftp-backup.service',preserve/'sql-database-ftp-backup.service.before')
shutil.copy2('/etc/systemd/system/sql-database-ftp-backup.timer',preserve/'sql-database-ftp-backup.timer.before')
manifest = []
names = {d['name'] for d in databases}
for path in output.iterdir():
    match = re.fullmatch(r'(.+)-(\d{8})\.zip',path.name)
    if not match or match.group(1) not in names:
        continue
    assert path.is_file() and not path.is_symlink() and path.parent.resolve() == output
    destination = preserve/path.name
    assert not destination.exists()
    manifest.append({'filename':path.name,'bytes':path.stat().st_size})
    shutil.move(str(path),str(destination))
    destination.chmod(0o600)
(preserve/'moved-files.json').write_text(json.dumps(manifest,indent=2)+'\n')

for db in databases:
    manager.prepare_folder(db['name'], '2026-10-05_to_2026-10-12')
readme = output/'BACKUP-README.txt'
readme.write_text('''SQL SERVER DATABASE BACKUPS -- Philippine time (UTC+08:00)

First full backups: October 10, 2026 at 00:00.
Weekly full backups: Mondays at 00:00.
Differentials: other nights at 00:00.
Transaction-log backups: every 15 minutes after a valid full baseline.

Folder: DATABASE/YYYY-MM-DD_to_YYYY-MM-DD/ (Monday to next Monday, end exclusive).
full_*.bak = full database; diff_*.bak = differential; log_*.trn = transaction log.
Each file has a matching .json with SHA-256, size, backup LSNs and full-backup reference.
SQL native backup compression is used; these files are NOT ZIP archives.

Only completed, checksum-verified backup files are published here.
Download files whose modification time is more than 30 minutes ago, including .json files.
Verify the SHA-256 hash against the .json BEFORE deleting your server copy.
FTP permits download and manual deletion in database/week folders; uploads are disabled.
No automatic backup deletion. No automatic ZIP creation or off-server transfer.
Preserve every required full/differential/log chain on your local machine.
Deleted local server files must be reuploaded by an administrator before a server-side restore.
If you download only daily, off-server protection extends only through your last download.

The old database ZIPs have been moved to protected temporary storage outside FTP;
they have NOT been destroyed. Website-environment archives were not changed.
''')
os.chown(readme,0,group)
readme.chmod(0o640)
subprocess.run(['systemctl','daemon-reload'],check=True)
subprocess.run(['systemctl','enable','--now','sql-backup-start.timer'],check=True)
# Check the real hardened service now: the not-before gate must do no database work.
subprocess.run(['systemctl','start','sql-backup-cycle.service'],check=True)
assert not manager.state_file.exists(), 'Premature backup state created'
print(json.dumps({'start':start.isoformat(),'database_count':len(databases),
                  'databases':[d['name'] for d in databases],
                  'old_database_zips_moved':len(manifest),'legacy_location':str(preserve),
                  'output_root':str(output),'production_backups_started':False},indent=2))
