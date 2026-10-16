# Angkasna production

- Domain: `angkasna.com`, with `www.angkasna.com` also configured.
- Jenkins: `Yapasakay`, branch `main`, following Parasakay's production routing.
- Staging still deploys only `yapasakay.com`.
- Service: `angkasna.service`, loopback port `5013`.
- Application: `/var/www/angkasna`.
- Database: `angaksnaDb` (intentional spelling), independently restored from a copy-only Yapasakay backup on October 9, 2026.
- SQL login: `angkasna_linux`, with access to the new database.
- Protected environment: `/etc/angkasna/angkasna-api.env`, mode `0600`.
- Startup: `/etc/angkasna/start-angkasna-api.sh`.
- Independent uploads: `/var/lib/angkasna/uploads`, copied and checksum-verified from Yapasakay.
- Logs: `/var/log/angkasna` and the systemd journal.
- Release metadata: `/var/lib/angkasna/release.json`, sharing the production release version.
- SiteLogs registry: `/var/lib/sitetools-provision/sites.json`, family `pasakay`, deployment job `Yapasakay`.

The environment contains `GoogleAuth__ClientId`, `GoogleAuth__ClientSecret`, `Maps__BrowserApiKey`, and `Maps__GoogleApiKey`. Google sign-in uses the user's new Angkasna credentials. Both Maps keys were temporarily switched back to Yapasakay's working keys on October 9, 2026, at the user's request. Values are not stored in this repository. Database credentials and JWT settings are separate. Public origin, CORS origins, uploads, logs, health URL, and release paths are specific to Angkasna.

Provisioning uses the existing server-side SiteTools worker through `provision-angkasna.py`. Progress is saved in `/var/lib/sitetools-provision/angkasna-progress.json`; never rerun `prepare` against existing resources. `publish` verifies the production Jenkins deployment and registers SiteLogs. `finish` requires both DNS names to resolve exclusively to `162.35.171.92`, then installs TLS and checks HTTPS and Google configuration.

Both names now resolve publicly to `162.35.171.92`. TLS covers both names and HTTP redirects to HTTPS. Jenkins build `5821` deployed version `1.0.135`, commit `051592b5ccbf4f19b61c8922f4ee0cdebfd701bc`.

The new Angkasna Maps key returned `REQUEST_DENIED` for legacy Directions and Places autocomplete, while Geocoding worked. After switching only the Maps keys back to Yapasakay's, Directions, Geocoding, and Places autocomplete all returned `OK`. The service process and public configuration endpoints were checked: Maps uses the restored keys and Google sign-in retains the new Angkasna client ID and secret. The service restart and health check passed. Browser rendering and complete Google sign-in still need a user test.

The provisioning helper describes the initial clone and compares credentials with Yapasakay; do not rerun its initial verification after this intentional credential replacement.
