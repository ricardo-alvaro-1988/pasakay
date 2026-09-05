# New YaPasakay production site runbook

Use this runbook when the request is:

```text
Create new website for YaPasakay with this domain/name: <domain>
```

Example:

```text
Create new website for YaPasakay with this domain/name: trygoride.com
```

The production server currently hosts separate YaPasakay clones from the same Jenkins package:

- `yapasakay.com` -> `/var/www/yapasakay`, `yapasakay.service`, port `5003`, database `yapasakaydb`
- `pricebadz.com` -> `/var/www/pricebadz`, `pricebadz.service`, port `5004`, database `pricebadzDB`
- `pasakyaman.com` -> `/var/www/pasakyaman`, `pasakyaman.service`, port `5005`, database `pasakyamanDB`
- `trygoride.com` -> `/var/www/trygoride`, `trygoride.service`, port `5006`, database `trygorideDB`

Every new site must be added in two places:

- Server: database, env file, app folders, systemd service, nginx, and SSL.
- Repo: `Jenkinsfile` deployment target list, so the shared package deploys to the new site.

## Naming

Derive these values before making changes:

```bash
domain="<domain-without-scheme>"
site_slug="<lowercase-app-name>"
brand_name="<human-readable-app-name>"
db_name="${site_slug}DB"
port="<next-unused-port-starting-at-5007>"
server_ip="162.35.171.92"
ssh_host="root@yapasakay.com"
```

Run every bash snippet after the DNS check on the production server as `root`, and set these variables in that same SSH shell before using later snippets.

For example:

```bash
domain="example.com"
site_slug="example"
brand_name="Example"
db_name="exampleDB"
port="5007"
```

Use the next free port after checking the server:

```bash
ssh root@yapasakay.com "ss -ltnp | grep -E ':500[0-9]' || true"
```

## DNS gate

Do not create SSL until both the apex and `www` hostnames point to the production server IP.

From Windows:

```powershell
Resolve-DnsName <domain> -Type A
Resolve-DnsName www.<domain> -Type A
Resolve-DnsName yapasakay.com -Type A
```

Expected IP for the new domain and `www` is:

```text
162.35.171.92
```

## Server folders

Create the site folders:

```bash
ssh root@yapasakay.com

install -d -m 0755 "/etc/${site_slug}"
install -d -m 0755 "/var/www/${site_slug}" "/var/www/releases/${site_slug}"
install -d -m 0755 "/var/lib/${site_slug}/uploads" "/var/log/${site_slug}"
```

If the new database is cloned from YapasaKay and existing upload paths need to keep working, copy the source uploads:

```bash
rsync -a /var/lib/yapasakay/uploads/ "/var/lib/${site_slug}/uploads/"
```

## Environment file

Copy the YapasaKay production env file first so SQL credentials, JWT, Maps, FCM, and other shared values are preserved:

```bash
cp /etc/yapasakay/yapasakay-api.env "/etc/${site_slug}/${site_slug}-api.env"
chmod 0600 "/etc/${site_slug}/${site_slug}-api.env"
```

Edit only the site-specific values:

```bash
PublicOrigin=https://${domain}
CorsOrigins__0=https://${domain}
CorsOrigins__1=https://www.${domain}
YP_DB_NAME=${db_name}
YP_UPLOAD_ROOT=/var/lib/${site_slug}/uploads
Storage__UploadsPath=/var/lib/${site_slug}/uploads
YP_LOG_ROOT=/var/log/${site_slug}
YP_RELEASE_FILE=/var/lib/${site_slug}/release.json
Release__MetadataPath=/var/lib/${site_slug}/release.json
YP_RELEASE_APP=${brand_name}
YP_HEALTH_URL=http://127.0.0.1:${port}/health
```

For Google sign-in, either keep the copied `GoogleAuth__ClientId` and add the new origins to that OAuth client, or replace it with a dedicated OAuth web client ID. The client ID in the env file must be the same Google OAuth client that allows these Authorized JavaScript origins:

```text
https://<domain>
https://www.<domain>
```

For Google Maps, if the browser key is referrer-restricted, add:

```text
https://<domain>/*
https://www.<domain>/*
```

## Database clone

SQL Server runs in Docker as container `sqlserver`. Read SQL credentials from `/etc/yapasakay/yapasakay-api.env`; do not paste secrets into shell history or docs.

Create a copy-only backup from the YapasaKay production database:

```bash
source /etc/yapasakay/yapasakay-api.env
source_db="${YP_DB_NAME}"
backup="/var/opt/mssql/backup/${source_db}-to-${db_name}-$(date +%Y%m%d%H%M%S).bak"
sqlcmd="/opt/mssql-tools18/bin/sqlcmd"

docker exec sqlserver "$sqlcmd" \
  -S localhost -U "$YP_DB_USER" -P "$YP_DB_PASSWORD" -C \
  -Q "BACKUP DATABASE [${source_db}] TO DISK = N'${backup}' WITH COPY_ONLY, INIT, COMPRESSION"
```

Read the logical file names:

```bash
docker exec sqlserver "$sqlcmd" \
  -S localhost -U "$YP_DB_USER" -P "$YP_DB_PASSWORD" -C \
  -Q "RESTORE FILELISTONLY FROM DISK = N'${backup}'"
```

Restore into the new database, replacing the logical names from the previous output:

```bash
docker exec sqlserver "$sqlcmd" \
  -S localhost -U "$YP_DB_USER" -P "$YP_DB_PASSWORD" -C \
  -Q "RESTORE DATABASE [${db_name}] FROM DISK = N'${backup}' WITH MOVE N'<logical-data-name>' TO N'/var/opt/mssql/data/${db_name}.mdf', MOVE N'<logical-log-name>' TO N'/var/opt/mssql/data/${db_name}_log.ldf', RECOVERY, REPLACE"
```

Verify:

```bash
docker exec sqlserver "$sqlcmd" \
  -S localhost -U "$YP_DB_USER" -P "$YP_DB_PASSWORD" -C \
  -Q "SELECT name FROM sys.databases WHERE name = N'${db_name}'"
```

## Startup script

Create `/etc/${site_slug}/start-${site_slug}-api.sh`:

```bash
cat >"/etc/${site_slug}/start-${site_slug}-api.sh" <<EOF
#!/usr/bin/env bash
set -Eeuo pipefail

source /etc/${site_slug}/${site_slug}-api.env
export TZ="\${TZ:-Asia/Manila}"

: "\${YP_DB_HOST:?YP_DB_HOST is required}"
: "\${YP_DB_NAME:?YP_DB_NAME is required}"
: "\${YP_DB_USER:?YP_DB_USER is required}"
: "\${YP_DB_PASSWORD:?YP_DB_PASSWORD is required}"

export ConnectionStrings__Default="Data Source=\${YP_DB_HOST};Initial Catalog=\${YP_DB_NAME};User ID=\${YP_DB_USER};Password=\${YP_DB_PASSWORD};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=YaPasakay.Api"
export Jwt__Issuer="\${Jwt__Issuer:-YaPasakay}"
export Jwt__Audience="\${Jwt__Audience:-YaPasakay}"
export Jwt__Key="\${Jwt__Key:?Jwt__Key is required}"

export PublicOrigin="\${PublicOrigin:-https://${domain}}"
export CorsOrigins__0="\${CorsOrigins__0:-https://${domain}}"
export CorsOrigins__1="\${CorsOrigins__1:-https://www.${domain}}"

for key in GoogleAuth__ClientId GoogleAuth__ClientSecret Maps__BrowserApiKey Maps__GoogleApiKey Fcm__ServerKey; do
    if [ "\${!key+x}" ]; then
        export "\$key"
    fi
done

export YP_UPLOAD_ROOT="\${YP_UPLOAD_ROOT:-/var/lib/${site_slug}/uploads}"
export YP_LOG_ROOT="\${YP_LOG_ROOT:-/var/log/${site_slug}}"
export Storage__UploadsPath="\${Storage__UploadsPath:-\$YP_UPLOAD_ROOT}"
export YP_RELEASE_FILE="\${YP_RELEASE_FILE:-/var/lib/${site_slug}/release.json}"
export Release__MetadataPath="\${Release__MetadataPath:-\$YP_RELEASE_FILE}"
exec /var/www/${site_slug}/YaPasakay.Api
EOF

chmod 0755 "/etc/${site_slug}/start-${site_slug}-api.sh"
```

## Systemd service

Create `/etc/systemd/system/${site_slug}.service`:

```bash
cat >"/etc/systemd/system/${site_slug}.service" <<EOF
[Unit]
Description=${brand_name} ASP.NET Core Application
After=network.target docker.service

[Service]
WorkingDirectory=/var/www/${site_slug}
ExecStart=/etc/${site_slug}/start-${site_slug}-api.sh
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:${port}

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable "${site_slug}.service"
```

Do not start the service until the first Jenkins deployment has copied `YaPasakay.Api` into `/var/www/${site_slug}`.

## Nginx and SSL

Create an HTTP nginx site first:

```bash
cat >"/etc/nginx/sites-available/${site_slug}" <<EOF
server {
    listen 80;
    server_name ${domain} www.${domain};

    client_max_body_size 10m;

    location / {
        proxy_pass http://127.0.0.1:${port};
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection \$connection_upgrade;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }
}
EOF

ln -sfn "/etc/nginx/sites-available/${site_slug}" "/etc/nginx/sites-enabled/${site_slug}"
nginx -t
systemctl reload nginx
```

Issue SSL after DNS points to `162.35.171.92`:

```bash
certbot --nginx -d "${domain}" -d "www.${domain}"
nginx -t
systemctl reload nginx
```

Prefer sharing the HTTPS non-www URL with users:

```text
https://<domain>
```

## Jenkins

Add the new target to `Jenkinsfile` under `Deploy Production Sites`:

```text
${site_slug}|${DEPLOY_HOST}|/var/www/${site_slug}|${site_slug}.service|/etc/${site_slug}/${site_slug}-api.env|http://127.0.0.1:${port}/health|/var/www/releases/${site_slug}
```

Commit and push to `main`, or run the production Jenkins job. Jenkins deploys the same package to every listed target and starts the new service.

## Verification

After Jenkins deploys, check:

```bash
systemctl status "${site_slug}.service" --no-pager -l
journalctl -u "${site_slug}.service" -n 120 --no-pager
curl -fsS "http://127.0.0.1:${port}/health"
curl -fsS "https://${domain}/api/public/auth"
curl -fsS "https://${domain}/api/public/maps"
```

Confirm these browser-critical settings:

- `https://${domain}/api/public/auth` returns the intended `googleClientId`.
- Google OAuth allows `https://${domain}` and `https://www.${domain}`.
- Google Maps browser key allows `https://${domain}/*` and `https://www.${domain}/*`.
- `http://${domain}` redirects to `https://${domain}` after Certbot updates nginx.

Finally, update the brand name, logo, favicon, and app colors from the admin portal for the new database.
