#!/usr/bin/env python3
"""Native SQL backups, published to FTPS after verification; no ZIP or automatic deletion."""
import argparse
import datetime as dt
import fcntl
import grp
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid
from zoneinfo import ZoneInfo

CONFIG = Path('/etc/sql-backup-manager/config.json')
TZ = ZoneInfo('Asia/Manila')


def week_name(now):
    sunday = now.date() - dt.timedelta(days=(now.weekday() + 1) % 7)
    return f'{sunday.isoformat()}_to_{(sunday + dt.timedelta(days=7)).isoformat()}'


def data_kind(previous, now, full_matches=True):
    if not previous or previous.get('week') != week_name(now) or not full_matches:
        return 'full'
    return 'diff' if previous.get('data_day') != now.date().isoformat() else None


def quote_identifier(name):
    return '[' + name.replace(']', ']]') + ']'


def quote_string(value):
    return "N'" + str(value).replace("'", "''") + "'"


def atomic_json(path, value, mode=0o600, gid=None):
    tmp = path.with_name(path.name + '.tmp')
    fd = os.open(tmp, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, mode)
    with os.fdopen(fd, 'w') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')
        stream.flush()
        os.fsync(stream.fileno())
    os.chmod(tmp, mode)
    if gid is not None:
        os.chown(tmp, 0, gid)
    os.replace(tmp, path)


class Manager:
    def __init__(self, config):
        self.cfg = config
        self.root = Path(config['output_root'])
        self.private = Path(config['state_root'])
        self.staging = Path(config['staging_host'])
        self.state_file = self.private / 'state.json'
        self.state = json.loads(self.state_file.read_text()) if self.state_file.exists() else {'databases': {}, 'pending': {}}
        self.gid = grp.getgrnam(config['ftp_group']).gr_gid
        self.env = dict(os.environ)
        for line in Path(config['sql_env']).read_text().splitlines():
            if line.startswith('MSSQL_SA_PASSWORD='):
                self.env['SQLCMDPASSWORD'] = line.split('=', 1)[1].strip().strip('"').strip("'")
        if not self.env.get('SQLCMDPASSWORD'):
            raise RuntimeError('SQL credentials unavailable')

    def sql(self, query, database='master', timeout=1800, as_json=False):
        args = ['docker', 'exec', '-e', 'SQLCMDPASSWORD', self.cfg['container'],
                '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost', '-U', 'sa', '-C', '-b',
                '-l', '15', '-t', str(timeout), '-h', '-1', '-y', '8000', '-w', '65535',
                '-d', database, '-Q', 'SET NOCOUNT ON; SET LOCK_TIMEOUT 15000; ' + query]
        result = subprocess.run(args, env=self.env, text=True, capture_output=True, timeout=timeout + 30)
        if result.returncode:
            # Driver diagnostics can include arbitrary buffers; expose SQL message
            # numbers only, never raw command/driver output or credentials.
            codes = re.findall(r'Msg (\d+), Level (\d+), State (\d+)', result.stdout + result.stderr)
            raise RuntimeError('SQL command failed; message codes=' + str(codes) + '; exit=' + str(result.returncode))
        if as_json:
            return json.loads(''.join(line.rstrip() for line in result.stdout.splitlines()).strip() or '[]')
        return result.stdout.strip()

    def save(self):
        atomic_json(self.state_file, self.state)

    def inventory(self):
        return self.sql("SELECT d.name,d.state_desc,d.recovery_model_desc,d.is_read_only,"
                        "CONVERT(varchar(36),r.database_guid) AS database_guid "
                        "FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id "
                        "WHERE d.database_id>4 AND d.source_database_id IS NULL ORDER BY d.name FOR JSON PATH", as_json=True)

    def history(self, device):
        return self.sql("SELECT TOP(1) b.backup_set_id,CONVERT(varchar(36),b.backup_set_uuid) AS backup_uuid,"
                        "b.database_name,b.type,b.is_copy_only,b.has_backup_checksums,"
                        "CONVERT(varchar(19),b.backup_start_date,126) AS started_ph,"
                        "CONVERT(varchar(19),b.backup_finish_date,126) AS finished_ph,"
                        "CONVERT(varchar(40),b.first_lsn) AS first_lsn,"
                        "CONVERT(varchar(40),b.last_lsn) AS last_lsn,"
                        "CONVERT(varchar(40),b.checkpoint_lsn) AS checkpoint_lsn,"
                        "CONVERT(varchar(40),b.database_backup_lsn) AS database_backup_lsn "
                        "FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily m ON m.media_set_id=b.media_set_id "
                        f"WHERE m.physical_device_name={quote_string(device)} AND b.backup_finish_date IS NOT NULL "
                        "ORDER BY b.backup_finish_date DESC FOR JSON PATH", as_json=True)

    def full_matches(self, database, previous):
        if not previous:
            return False
        rows = self.sql("SELECT TOP(1) CONVERT(varchar(36),backup_set_uuid) AS backup_uuid "
                        f"FROM msdb.dbo.backupset WHERE database_name={quote_string(database)} "
                        "AND type='D' AND is_copy_only=0 ORDER BY backup_finish_date DESC FOR JSON PATH", as_json=True)
        return bool(rows and rows[0]['backup_uuid'] == previous.get('full_uuid'))

    def prepare_folder(self, database, week):
        for folder in [self.root / database, self.root / database / week]:
            if folder.is_symlink():
                raise RuntimeError('Refusing symlink in backup output')
            folder.mkdir(exist_ok=True)
            os.chown(folder, 0, self.gid)
            os.chmod(folder, 0o770)
        return self.root / database / week

    def publish_pending(self, database):
        pending = self.state['pending'].get(database)
        if not pending:
            return
        device = pending['device']
        history = self.history(device)
        if not history:
            active = self.sql("SELECT session_id FROM sys.dm_exec_requests WHERE command LIKE 'BACKUP%' "
                              f"AND database_id=DB_ID({quote_string(database)}) FOR JSON PATH", as_json=True)
            if active:
                raise RuntimeError('Backup still active; preserving pending state')
            # Retain partial files privately, never as downloadable complete backups.
            self.state.setdefault('failed_attempts', []).append(pending)
            del self.state['pending'][database]
            self.save()
            raise RuntimeError('Backup has no completed SQL history; partial file retained privately')
        record = history[0]
        expected = {'full': 'D', 'diff': 'I', 'log': 'L'}[pending['kind']]
        if record['database_name'] != database or record['type'] != expected or record['is_copy_only']:
            raise RuntimeError('Backup metadata does not match intended database/type')
        if not record['has_backup_checksums']:
            raise RuntimeError('Backup checksums are absent')
        prior = self.state['databases'].get(database, {})
        if pending['kind'] == 'diff' and record['database_backup_lsn'] != prior.get('full_checkpoint_lsn'):
            raise RuntimeError('Differential base changed externally; retained backup requires review')
        staged = self.staging / pending['filename']
        directory = self.prepare_folder(database, pending['week'])
        final = directory / pending['filename']
        if staged.exists():
            self.sql(f'RESTORE VERIFYONLY FROM DISK={quote_string(device)} WITH CHECKSUM;')
            with staged.open('rb') as stream:
                digest = hashlib.file_digest(stream, 'sha256').hexdigest()
            size = staged.stat().st_size
            os.replace(staged, final)
            os.chown(final, 0, self.gid)
            os.chmod(final, 0o640)
            os.utime(final, None)  # 30-minute age begins after completion and verification.
        elif final.is_file():
            # Recover a crash between publication and state-save.
            os.chown(final, 0, self.gid)
            os.chmod(final, 0o640)
            with final.open('rb') as stream:
                digest = hashlib.file_digest(stream, 'sha256').hexdigest()
            size = final.stat().st_size
        else:
            raise RuntimeError('Completed backup is missing; refusing to conceal a restore-chain gap')
        metadata = {**record, 'filename': final.name, 'sha256': digest, 'bytes': size,
                    'database_guid': pending['database_guid'], 'timezone': 'Asia/Manila',
                    'week': pending['week'], 'kind': pending['kind'],
                    'published_at': dt.datetime.now(TZ).isoformat(),
                    'full_backup': final.name if pending['kind'] == 'full' else prior.get('full_file'),
                    'full_week': pending['week'] if pending['kind'] == 'full' else prior.get('week')}
        atomic_json(final.with_suffix(final.suffix + '.json'), metadata, 0o640, self.gid)
        with (self.private / 'catalog.jsonl').open('a') as stream:
            stream.write(json.dumps(metadata) + '\n')
            stream.flush()
            os.fsync(stream.fileno())
        current = self.state['databases'].setdefault(database, {})
        current['database_guid'] = pending['database_guid']
        if pending['kind'] == 'full':
            current.update(week=pending['week'], full_uuid=record['backup_uuid'],
                           full_checkpoint_lsn=record['checkpoint_lsn'], full_file=final.name,
                           data_day=pending['day'])
        elif pending['kind'] == 'diff':
            current['data_day'] = pending['day']
        current['last_' + pending['kind']] = record['finished_ph']
        del self.state['pending'][database]
        self.save()
        print(f'OK {database} {pending["kind"]}: {final.relative_to(self.root)} ({size} bytes)', flush=True)

    def backup(self, db, kind, now):
        name = db['name']
        week = week_name(now)
        if shutil.disk_usage(self.root).free < self.cfg['minimum_free_gb'] * 1024**3:
            raise RuntimeError('Free disk below reserve; no backup files deleted automatically')
        filename = f'{kind}_{now.strftime("%Y%m%dT%H%M%S")}_PHT_{uuid.uuid4().hex[:8]}' + ('.trn' if kind == 'log' else '.bak')
        device = self.cfg['staging_container'] + '/' + filename
        self.state['pending'][name] = dict(filename=filename, device=device, kind=kind, week=week,
                                           day=now.date().isoformat(), database_guid=db['database_guid'])
        self.save()
        operation = 'BACKUP LOG' if kind == 'log' else 'BACKUP DATABASE'
        options = 'DIFFERENTIAL, ' if kind == 'diff' else ''
        self.sql(f'{operation} {quote_identifier(name)} TO DISK={quote_string(device)} '
                 f'WITH {options}COMPRESSION, CHECKSUM;')
        self.publish_pending(name)

    def sample(self, db):
        rows = self.sql("SELECT total_log_size_in_bytes/1048576.0 AS total_mb,"
                        "used_log_space_in_bytes/1048576.0 AS used_mb,used_log_space_in_percent AS used_pct "
                        "FROM sys.dm_db_log_space_usage FOR JSON PATH", database=db, timeout=30, as_json=True)
        row = {'time': dt.datetime.now(TZ).isoformat(), 'database': db, **rows[0]}
        with (self.private / ('usage-' + dt.datetime.now(TZ).date().isoformat() + '.jsonl')).open('a') as stream:
            stream.write(json.dumps(row) + '\n')
        if row['used_pct'] >= 70:
            print(f'WARNING {db} transaction log {row["used_pct"]:.1f}% used', flush=True)
        return row

    def run(self, now):
        if now < dt.datetime.fromisoformat(self.cfg['not_before']):
            print('Waiting for configured midnight start; no database changes made.', flush=True)
            return
        summary = {'started_at': now.isoformat(), 'databases': {}, 'errors': []}
        for db in self.inventory():
            name = db['name']
            try:
                if not re.fullmatch(r'[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}', name):
                    raise RuntimeError('Database name needs a reviewed filesystem mapping')
                if db['state_desc'] != 'ONLINE' or db['is_read_only']:
                    raise RuntimeError('Database not online/read-write; policy requires review')
                self.publish_pending(name)
                previous = self.state['databases'].get(name)
                if previous and previous.get('database_guid') != db['database_guid']:
                    previous = None
                    self.state['databases'].pop(name, None)
                before = self.sample(name)
                if db['recovery_model_desc'] != 'FULL':
                    self.sql(f'ALTER DATABASE {quote_identifier(name)} SET RECOVERY FULL;')
                    previous = None
                kind = data_kind(previous, now, self.full_matches(name, previous))
                data_error = None
                if kind:
                    try:
                        self.backup(db, kind, dt.datetime.now(TZ))
                    except Exception as exc:
                        data_error = str(exc)
                        if self.state['pending'].get(name):
                            try:
                                self.publish_pending(name)
                            except Exception:
                                if self.state['pending'].get(name):
                                    raise
                        if not self.state['databases'].get(name, {}).get('full_uuid'):
                            raise RuntimeError(data_error)
                self.backup(db, 'log', dt.datetime.now(TZ))
                after = self.sample(name)
                summary['databases'][name] = {'before': before, 'after': after,
                                               **self.state['databases'][name]}
                if data_error:
                    raise RuntimeError(data_error)
            except Exception as exc:
                message = f'{name}: {exc}'
                summary['errors'].append(message)
                print('ERROR ' + message, flush=True)
        summary['finished_at'] = dt.datetime.now(TZ).isoformat()
        summary['ok'] = not summary['errors']
        atomic_json(self.private / 'latest.json', summary, 0o640, self.gid)
        if summary['errors']:
            raise RuntimeError(f'{len(summary["errors"])} database(s) failed; see latest.json and journal')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--config', type=Path, default=CONFIG)
    parser.add_argument('--check', action='store_true', help='Read-only readiness check')
    args = parser.parse_args()
    config = json.loads(args.config.read_text())
    manager = Manager(config)
    if args.check:
        print(json.dumps({'start': config['not_before'], 'output': str(manager.root),
                          'databases': manager.inventory()}, indent=2))
        if manager.staging.stat().st_dev != manager.root.stat().st_dev:
            raise RuntimeError('Staging and FTP output must share a filesystem')
        return
    with (manager.private / 'run.lock').open('w') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        manager.run(dt.datetime.now(TZ))


if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        print('ERROR ' + str(exc), file=sys.stderr, flush=True)
        sys.exit(1)
