# PriceBadz production clone

Use one Jenkins build for both production sites:

- `yapasakay.com` -> `/var/www/yapasakay`, `yapasakay.service`, `/etc/yapasakay/yapasakay-api.env`, database `YaPasakay`
- `pricebadz.com` -> `/var/www/pricebadz`, `pricebadz.service`, `/etc/pricebadz/pricebadz-api.env`, database `pricebadzDB`

`pricebadz.com` should point to the same server IP as `yapasakay.com`. The deploy pipeline uses `yapasakay.com` as the SSH host and deploys both folders on that server.

## Database copy

Run this on the production SQL Server host. Replace the SQL login values and file paths if your SQL Server uses different locations.

```bash
sudo mkdir -p /var/opt/mssql/backup

sqlcmd -S localhost -U sa -P '<sql-password>' -Q "BACKUP DATABASE [YaPasakay] TO DISK = N'/var/opt/mssql/backup/yapasakay-to-pricebadz.bak' WITH COPY_ONLY, INIT, COMPRESSION"

sqlcmd -S localhost -U sa -P '<sql-password>' -Q "RESTORE FILELISTONLY FROM DISK = N'/var/opt/mssql/backup/yapasakay-to-pricebadz.bak'"
```

Use the logical data and log names returned by `RESTORE FILELISTONLY` in the restore command:

```bash
sqlcmd -S localhost -U sa -P '<sql-password>' -Q "RESTORE DATABASE [pricebadzDB] FROM DISK = N'/var/opt/mssql/backup/yapasakay-to-pricebadz.bak' WITH MOVE N'<logical-data-name>' TO N'/var/opt/mssql/data/pricebadzDB.mdf', MOVE N'<logical-log-name>' TO N'/var/opt/mssql/data/pricebadzDB_log.ldf', RECOVERY, REPLACE"
```

## Server folders and env

Create the PriceBadz folders and env file before the first Jenkins deploy:

```bash
sudo install -d -m 0755 /etc/pricebadz /var/www/pricebadz /var/www/releases/pricebadz
sudo install -d -m 0755 /var/lib/pricebadz/uploads /var/log/pricebadz
sudo chown -R www-data:www-data /var/lib/pricebadz /var/log/pricebadz
```

For the first clone, copy the existing upload files so restored rows that point at `/uploads/...` still resolve:

```bash
sudo rsync -a /var/lib/yapasakay/uploads/ /var/lib/pricebadz/uploads/
```

Create `/etc/pricebadz/pricebadz-api.env`:

```bash
PublicOrigin=https://pricebadz.com
CorsOrigins__0=https://pricebadz.com
CorsOrigins__1=https://www.pricebadz.com

YP_DB_HOST='127.0.0.1,1433'
YP_DB_NAME='pricebadzDB'
YP_DB_USER='<sql-user>'
YP_DB_PASSWORD='<sql-password>'

Jwt__Issuer=YaPasakay
Jwt__Audience=YaPasakay
Jwt__Key='<32-plus-character-random-production-secret>'

Otp__Mode=Fixed
Otp__AllowDevBypass=false

GoogleAuth__ClientId='<google-web-client-id>'
Maps__BrowserApiKey='<google-browser-api-key>'
Maps__GoogleApiKey='<google-server-api-key>'
Fcm__ServerKey=''

YP_UPLOAD_ROOT=/var/lib/pricebadz/uploads
Storage__UploadsPath=/var/lib/pricebadz/uploads
YP_LOG_ROOT=/var/log/pricebadz
YP_RELEASE_FILE=/var/lib/pricebadz/release.json
Release__MetadataPath=/var/lib/pricebadz/release.json
YP_RELEASE_APP=PriceBadz
YP_HEALTH_URL=http://127.0.0.1:5004/health
```

Secure it after editing:

```bash
sudo chmod 0600 /etc/pricebadz/pricebadz-api.env
```

## Systemd service

Create `/etc/pricebadz/start-pricebadz-api.sh`:

```bash
#!/usr/bin/env bash
set -Eeuo pipefail

source /etc/pricebadz/pricebadz-api.env
export TZ="${TZ:-Asia/Manila}"

: "${YP_DB_HOST:?YP_DB_HOST is required}"
: "${YP_DB_NAME:?YP_DB_NAME is required}"
: "${YP_DB_USER:?YP_DB_USER is required}"
: "${YP_DB_PASSWORD:?YP_DB_PASSWORD is required}"

export ConnectionStrings__Default="Data Source=${YP_DB_HOST};Initial Catalog=${YP_DB_NAME};User ID=${YP_DB_USER};Password=${YP_DB_PASSWORD};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=YaPasakay.Api"
export Jwt__Issuer="${Jwt__Issuer:-YaPasakay}"
export Jwt__Audience="${Jwt__Audience:-YaPasakay}"
export Jwt__Key="${Jwt__Key:?Jwt__Key is required}"
export PublicOrigin="${PublicOrigin:-https://pricebadz.com}"
export CorsOrigins__0="${CorsOrigins__0:-https://pricebadz.com}"
export CorsOrigins__1="${CorsOrigins__1:-https://www.pricebadz.com}"
export YP_UPLOAD_ROOT="${YP_UPLOAD_ROOT:-/var/lib/pricebadz/uploads}"
export Storage__UploadsPath="${Storage__UploadsPath:-$YP_UPLOAD_ROOT}"
export YP_RELEASE_FILE="${YP_RELEASE_FILE:-/var/lib/pricebadz/release.json}"
export Release__MetadataPath="${Release__MetadataPath:-$YP_RELEASE_FILE}"
exec /var/www/pricebadz/YaPasakay.Api
```

Then make it executable:

```bash
sudo chmod 0755 /etc/pricebadz/start-pricebadz-api.sh
```

Create `/etc/systemd/system/pricebadz.service`:

```ini
[Unit]
Description=PriceBadz ASP.NET Core Application
After=network.target docker.service

[Service]
WorkingDirectory=/var/www/pricebadz
ExecStart=/etc/pricebadz/start-pricebadz-api.sh
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5004
KillSignal=SIGINT

[Install]
WantedBy=multi-user.target
```

Enable it, but let the first Jenkins deploy start it after the executable exists:

```bash
sudo systemctl daemon-reload
sudo systemctl enable pricebadz.service
```

## Nginx

Add a server block for `pricebadz.com` and `www.pricebadz.com` that proxies to `http://127.0.0.1:5004`, then issue/renew the SSL certificate for both hostnames. Keep the existing `yapasakay.com` server block on its current Kestrel port.

After the manual DB, env, service, and nginx setup is done, pushing to `main` will deploy the same build to both sites.
