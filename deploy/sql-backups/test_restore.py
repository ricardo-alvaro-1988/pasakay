"""Production-host validation using two uniquely named disposable databases only."""
import datetime as dt
import json
import os
from pathlib import Path
import shutil
import time
import uuid
from backup_manager import Manager, TZ, quote_identifier, quote_string

token = uuid.uuid4().hex[:12]
database = 'CodexBackupVerify_' + token
restored = database + '_restore'
root = Path('/opt/sqlserver/backups') / ('manager-test-' + token)
for path in [root, root / 'out', root / 'stage', root / 'state']:
    path.mkdir(mode=0o755)
os.chown(root / 'stage', 10001, 10001)
config = dict(output_root=str(root / 'out'), state_root=str(root / 'state'),
              staging_host=str(root / 'stage'),
              staging_container='/var/opt/mssql/backups/' + root.name + '/stage',
              ftp_group='dbbackup', sql_env='/opt/sqlserver/.env', container='sqlserver',
              minimum_free_gb=15, not_before='2026-10-10T00:00:00+08:00')
manager = Manager(config)
created = []


def restore_device(record):
    source = root / 'out' / database / record['week'] / record['filename']
    target = root / 'stage' / ('restore-' + record['filename'])
    shutil.copy2(source, target)
    os.chown(target, 10001, 10001)
    os.chmod(target, 0o600)
    return config['staging_container'] + '/' + target.name


try:
    assert not manager.sql(f'SELECT name FROM sys.databases WHERE name IN ({quote_string(database)},{quote_string(restored)})')
    manager.sql(f'CREATE DATABASE {quote_identifier(database)}; ALTER DATABASE {quote_identifier(database)} SET RECOVERY FULL;')
    created.append(database)
    manager.sql('CREATE TABLE dbo.BackupProbe (Id int PRIMARY KEY); INSERT dbo.BackupProbe VALUES (1);', database)
    db = next(d for d in manager.inventory() if d['name'] == database)
    manager.backup(db, 'full', dt.datetime.now(TZ))
    manager.sql('INSERT dbo.BackupProbe VALUES (2);', database)
    manager.backup(db, 'diff', dt.datetime.now(TZ))
    manager.sql('INSERT dbo.BackupProbe VALUES (3);', database)
    time.sleep(1.1)
    target_time = manager.sql('SELECT CONVERT(varchar(33),GETDATE(),126)')
    time.sleep(1.1)
    manager.sql('INSERT dbo.BackupProbe VALUES (4);', database)
    # Simulate interruption after BACKUP LOG succeeds but before publication.
    manager.publish_pending = lambda name: None
    manager.backup(db, 'log', dt.datetime.now(TZ))
    manager = Manager(config)
    assert database in manager.state['pending']
    manager.publish_pending(database)
    records = [json.loads(line) for line in (root / 'state/catalog.jsonl').read_text().splitlines()]
    full, diff, log = records
    assert [r['kind'] for r in records] == ['full', 'diff', 'log']
    assert diff['database_backup_lsn'] == full['checkpoint_lsn']
    assert log['first_lsn'] <= log['last_lsn']
    devices = [restore_device(r) for r in records]
    files = manager.sql('SELECT name,type_desc FROM sys.database_files FOR JSON PATH', database=database, as_json=True)
    moves = []
    for i, file in enumerate(files):
        suffix = '.ldf' if file['type_desc'] == 'LOG' else '.mdf'
        moves.append(f'MOVE {quote_string(file["name"])} TO '+quote_string(f'/var/opt/mssql/data/{restored}_{i}{suffix}'))
    created.append(restored)
    manager.sql(f'RESTORE DATABASE {quote_identifier(restored)} FROM DISK={quote_string(devices[0])} WITH '+', '.join(moves)+', NORECOVERY;')
    manager.sql(f'RESTORE DATABASE {quote_identifier(restored)} FROM DISK={quote_string(devices[1])} WITH NORECOVERY;')
    manager.sql(f'RESTORE LOG {quote_identifier(restored)} FROM DISK={quote_string(devices[2])} WITH STOPAT={quote_string(target_time)}, RECOVERY;')
    rows = manager.sql('SELECT Id FROM dbo.BackupProbe ORDER BY Id FOR JSON PATH', database=restored, as_json=True)
    assert rows == [{'Id': 1}, {'Id': 2}, {'Id': 3}], rows
    manager.sql('DBCC CHECKDB WITH NO_INFOMSGS;', database=restored)
    print('PASS: full + differential + recovered pending log restore to chosen time; later row excluded; CHECKDB passed.', flush=True)
    print('Test artifacts retained at ' + str(root), flush=True)
finally:
    for name in reversed(created):
        # Only the two exact UUID-scoped databases created by this test may be dropped.
        assert name in [database, restored] and name.startswith('CodexBackupVerify_')
        manager.sql(f'IF DB_ID({quote_string(name)}) IS NOT NULL BEGIN '
                    f'ALTER DATABASE {quote_identifier(name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; '
                    f'DROP DATABASE {quote_identifier(name)}; END;')
