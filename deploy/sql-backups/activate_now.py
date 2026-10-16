#!/usr/bin/env python3
"""One-time October 9 activation, superseding the original midnight bootstrap."""
import datetime as dt
import json
import os
from pathlib import Path
import shutil
import subprocess
from backup_manager import Manager, TZ, atomic_json, week_name

source = Path(__file__).resolve().parent
config_file = Path('/etc/sql-backup-manager/config.json')
config = json.loads(config_file.read_text())
manager = Manager(config)
assert not manager.state_file.exists(), 'Already started; review existing state'
assert subprocess.run(['systemctl','is-active','--quiet','sql-backup-cycle.service']).returncode != 0
databases = manager.inventory()
assert len(databases) == 14 and all(d['state_desc']=='ONLINE' and not d['is_read_only'] for d in databases)
now = dt.datetime.now(TZ)
assert now.date() == dt.date(2026,10,9), 'Review activation date'
preserve = Path('/var/backups/sql-backup-manager') / ('before-sunday-' + now.strftime('%Y%m%dT%H%M%S'))
preserve.mkdir(mode=0o700)
installed = Path('/usr/local/lib/sql-backup-manager/backup_manager.py')
shutil.copy2(installed, preserve/'backup_manager.py')
shutil.copy2(config_file, preserve/'config.json')
readme = manager.root/'BACKUP-README.txt'
shutil.copy2(readme,preserve/'BACKUP-README.txt')
subprocess.run(['systemctl','disable','--now','sql-backup-start.timer'],check=True)
for db in databases:
    parent = manager.root/db['name']
    old = parent/'2026-10-05_to_2026-10-12'
    target = parent/week_name(now)
    assert parent.resolve().parent == manager.root.resolve() and not parent.is_symlink()
    if old.exists():
        assert not old.is_symlink() and old.resolve().parent == parent.resolve()
        assert not list(old.iterdir()) and not target.exists(), 'Refusing to move populated folders'
        old.rename(target)
    manager.prepare_folder(db['name'],week_name(now))
temporary = installed.with_suffix('.new')
shutil.copy2(source/'backup_manager.py',temporary)
temporary.chmod(0o700)
os.replace(temporary,installed)
config['not_before'] = now.isoformat()
atomic_json(config_file,config)
content = readme.read_text().replace('First full backups: October 10, 2026 at 00:00.',
    'First full backups: October 9, 2026, started immediately.').replace(
    'Weekly full backups: Mondays at 00:00.',
    'Weekly full backups: Sundays at 00:00, starting October 11, 2026.').replace(
    'Differentials: other nights at 00:00.',
    'Differentials: other nights at 00:00, starting October 10, 2026.').replace(
    'Monday to next Monday','Sunday to next Sunday')
readme.write_text(content)
subprocess.run(['systemctl','enable','--now','sql-backup-cycle.timer'],check=True)
subprocess.run(['systemctl','start','--no-block','sql-backup-cycle.service'],check=True)
print(json.dumps({'activated_at':now.isoformat(),'databases':len(databases),'week':week_name(now),'previous_configuration':str(preserve)}))
