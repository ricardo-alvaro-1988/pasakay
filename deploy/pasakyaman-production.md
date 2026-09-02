# Pasakyaman production clone

Use one Jenkins build for all production sites:

- `yapasakay.com` -> `/var/www/yapasakay`, `yapasakay.service`, `/etc/yapasakay/yapasakay-api.env`, live database from that env file
- `pricebadz.com` -> `/var/www/pricebadz`, `pricebadz.service`, `/etc/pricebadz/pricebadz-api.env`, database `pricebadzDB`
- `pasakyaman.com` -> `/var/www/pasakyaman`, `pasakyaman.service`, `/etc/pasakyaman/pasakyaman-api.env`, database `pasakyamanDB`

`pasakyaman.com` should point to the same server IP as `yapasakay.com`. The deploy pipeline uses `yapasakay.com` as the SSH host and deploys all folders on that server.

## Server settings

For the initial clone, copy `/etc/yapasakay/yapasakay-api.env` to `/etc/pasakyaman/pasakyaman-api.env`, keeping the current OAuth and Maps API values. Change the site-specific values:

```bash
PublicOrigin=https://pasakyaman.com
CorsOrigins__0=https://pasakyaman.com
CorsOrigins__1=https://www.pasakyaman.com
YP_DB_NAME=pasakyamanDB
YP_UPLOAD_ROOT=/var/lib/pasakyaman/uploads
Storage__UploadsPath=/var/lib/pasakyaman/uploads
YP_LOG_ROOT=/var/log/pasakyaman
YP_RELEASE_FILE=/var/lib/pasakyaman/release.json
Release__MetadataPath=/var/lib/pasakyaman/release.json
YP_RELEASE_APP=Pasakyaman
YP_HEALTH_URL=http://127.0.0.1:5005/health
```

Create a matching `pasakyaman.service` that runs on `http://127.0.0.1:5005`, and add an nginx server block for `pasakyaman.com` and `www.pasakyaman.com` proxying to that port.

## Database copy

Clone the currently configured YapasaKay database to `pasakyamanDB` using `BACKUP DATABASE ... WITH COPY_ONLY` and `RESTORE DATABASE ... WITH MOVE`. Use the source database name from `/etc/yapasakay/yapasakay-api.env`; on the current server that database is `yapasakaydb`.

After the database, env file, service, and nginx setup are done, pushing to `main` deploys the same build to all three sites.
