# SQL backup schedule

Installed on `root@yapasakay.com` October 9, 2026. No secrets are stored here.

## Schedule and scope

- Initial full backups: October 9, 2026, started at 20:04 Asia/Manila (UTC+08:00).
- Subsequent full backups: Sundays at midnight, starting October 11.
- Differential backups: other nights at midnight, starting October 10.
- Transaction-log backups: every 15 minutes, after a verified full baseline exists.
- Scope: every online, writable user database. The initial inventory contained 14 application databases. SQL system databases and database snapshots are outside this schedule.
- SIMPLE/BULK_LOGGED databases switch to FULL recovery when first processed, immediately followed by a normal full backup and a log backup.
- A missed midnight data backup is caught up at the next run. A new database or externally replaced full baseline causes a fresh full backup.
- Backups run serially under a lock. One database failure is reported without preventing other databases from being attempted.

## Files and downloads

The existing FTPS root remains `/srv/db-backups`:

```text
/srv/db-backups/
  yapabili_live/
    2026-10-04_to_2026-10-11/
      full_20261009T200409_PHT_<id>.bak
      full_20261009T200409_PHT_<id>.bak.json
      log_20261009T201500_PHT_<id>.trn
      log_20261009T201500_PHT_<id>.trn.json
      diff_20261010T000000_PHT_<id>.bak
      diff_20261010T000000_PHT_<id>.bak.json
```

Week names run Sunday to the following Sunday (end exclusive). Files use Philippine timestamps. Native SQL backup compression is enabled, but ZIP archives are not created.

The SQL service writes into `/opt/sqlserver/backups/managed`, mounted inside the container at `/var/opt/mssql/backups/managed`. Only after SQL backup completion, `RESTORE VERIFYONLY WITH CHECKSUM`, and SHA-256 calculation does an atomic rename publish a backup into FTP. The matching JSON records size, SHA-256, LSNs and the associated full backup. Modification time is reset at publication.

Download completed backup files and their JSON files older than 30 minutes. Compare the local file's SHA-256 with its JSON metadata before manually deleting the server copy. The FTP account can delete files in database/week folders; upload, append, rename, mkdir and chmod commands are blocked. The FTP root itself is not writable. Reupload for a restore requires administrator assistance.

No automatic backup deletion or off-server copying is installed. The private catalog survives FTP deletion, so deleting a downloaded full backup does not reset differential scheduling. Keep full, differential and uninterrupted log chains locally; a restore near a week boundary can require a log file in the following week's folder. Daily downloads provide off-server protection only through the most recent successful download.

Thirteen old database ZIP files were moved to `/var/backups/sql-backup-manager/legacy-20261009`, outside FTP, and retained until the new backup baseline is established. Website-environment archives in FTP were not changed.

## Operations

- Code: `/usr/local/lib/sql-backup-manager/backup_manager.py`.
- Configuration: `/etc/sql-backup-manager/config.json`, root-only.
- Credentials: existing `/opt/sqlserver/.env`, loaded into `SQLCMDPASSWORD`; never put on the command line.
- Original midnight bootstrap: `sql-backup-start.timer` disabled; `activate_now.py` superseded it with immediate initialization and Sunday weeks. `install.py` records the original installation and must not be rerun.
- Repeating schedule: `sql-backup-cycle.timer` and `sql-backup-cycle.service`.
- Old job: `sql-database-ftp-backup.timer` disabled.
- Status: `/var/lib/sql-backup-manager/latest.json` and `journalctl -u sql-backup-cycle.service`.
- Catalog/state: `/var/lib/sql-backup-manager/catalog.jsonl` and `state.json`.
- Log usage before/after each cycle: `/var/lib/sql-backup-manager/usage-YYYY-MM-DD.jsonl`. These samples support a later size review; brief peaks between samples can be missed.
- Errors and usage >=70% are logged. No external notifications or SiteLogs UI changes were installed by this change.
- A free-space reserve of 15 GiB is checked before each backup. A low-disk failure is reported; existing backups are not automatically removed.
- `python3 /usr/local/lib/sql-backup-manager/backup_manager.py --check` checks connectivity/inventory without backup or recovery changes.

SQL Server log-backup history can exist before publication if the process is interrupted. Pending state is written before starting SQL; the next cycle verifies and publishes the completed file before taking another backup. Failed/incomplete files remain private for diagnosis.

## Validation

Eight schedule tests passed, including Friday initialization, Saturday differential selection, Sunday rollover, Monday differential selection, external-full detection and downloaded-file independence.

The isolated SQL test created two UUID-scoped test databases and proved full + differential + log recovery with `STOPAT`. It verified that rows committed before the target time existed and a later row did not. It also simulated interruption after the log backup completed but before publication. `DBCC CHECKDB` passed, and both temporary databases were removed. Test artifacts remain under the private SQL backup mount for inspection.

The installed systemd units passed verification. Running the hardened service before activation exercised its not-before guard and made no production database changes. FTP filesystem permissions were checked. The repeating timer was enabled during immediate activation on October 9.

Initial production cycle completed October 9 at 20:06 Philippine time: all 14 databases have verified full and log backups (28 files with independently checked SHA-256 hashes), FULL recovery, matching full baselines, no pending publications and no cycle errors. The next timer trigger was 20:15. Yapabili live log usage after backup was approximately 35 MB of the existing 10,000 MB allocation.

During activation, separate `ReadWritePaths` bind mounts prevented atomic publication across staging/output paths. The service now uses `ProtectSystem=full` without those separate mounts. File ownership changes happen after publication, allowing interrupted publications to retry SQL verification. Completed initial full backups were recovered and published successfully through the actual service.
