# TryGoRide production clone

TryGoRide is a YaPasakay production clone on the same server as `yapasakay.com`.

- Domain: `trygoride.com` and `www.trygoride.com`
- App folder: `/var/www/trygoride`
- Service: `trygoride.service`
- Kestrel port: `http://127.0.0.1:5006`
- Env file: `/etc/trygoride/trygoride-api.env`
- Database: `trygorideDB`
- Upload folder: `/var/lib/trygoride/uploads`
- Release metadata: `/var/lib/trygoride/release.json`
- Release root: `/var/www/releases/trygoride`

The initial database was cloned from the YapasaKay production database. The env file was copied from `/etc/yapasakay/yapasakay-api.env`, then these site-specific values were changed:

```bash
PublicOrigin=https://trygoride.com
CorsOrigins__0=https://trygoride.com
CorsOrigins__1=https://www.trygoride.com
YP_DB_NAME=trygorideDB
YP_UPLOAD_ROOT=/var/lib/trygoride/uploads
Storage__UploadsPath=/var/lib/trygoride/uploads
YP_LOG_ROOT=/var/log/trygoride
YP_RELEASE_FILE=/var/lib/trygoride/release.json
Release__MetadataPath=/var/lib/trygoride/release.json
YP_RELEASE_APP=TryGoRide
YP_HEALTH_URL=http://127.0.0.1:5006/health
```

## Google setup

The customer web app uses Google Identity Services with a popup callback. The OAuth client configured in `GoogleAuth__ClientId` must allow these Authorized JavaScript origins:

```text
https://trygoride.com
https://www.trygoride.com
```

If the Google Maps browser key is referrer-restricted, it must also allow:

```text
https://trygoride.com/*
https://www.trygoride.com/*
```

TryGoRide uses the same Google Maps API key for both `Maps__BrowserApiKey` and `Maps__GoogleApiKey`, matching the other production clone setup.

## Jenkins

`Jenkinsfile` includes TryGoRide in the `Deploy Production Sites` target list:

```text
trygoride|${DEPLOY_HOST}|/var/www/trygoride|trygoride.service|/etc/trygoride/trygoride-api.env|http://127.0.0.1:5006/health|/var/www/releases/trygoride
```

Pushing to `main` deploys the shared YaPasakay package to TryGoRide along with the other production sites.

## Verification

```bash
systemctl status trygoride.service --no-pager -l
curl -fsS http://127.0.0.1:5006/health
curl -fsS https://trygoride.com/api/public/auth
curl -fsS https://trygoride.com/api/public/maps
```

Use the HTTPS non-www URL when sharing the site:

```text
https://trygoride.com
```
