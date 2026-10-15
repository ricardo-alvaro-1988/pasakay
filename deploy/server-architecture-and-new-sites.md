# YaPasakay server architecture and new websites

## Purpose and evidence

This guide lets Cursor turn a short request into a new branded website using this repository's existing infrastructure. Root `AGENTS.md` directs agents here.

Reviewed against the latest fetched `Staging` repository files on 2026-10-15. No live Linux, Jenkins, DNS, or provider configuration was inspected for this document. Verify recorded server details before deployment. Current executable routing takes precedence over historical examples in older runbooks.

## Simple prompt

> Create a new production website similar to YaPasakay called NewBrand at newbrand.com. Follow AGENTS.md and deploy/server-architecture-and-new-sites.md. Use our shared application and production branch, with its own branding, database, and server resources. Prepare and validate the code and deployment configuration. Ask only for missing information that blocks you.

To request live publication, add:

> Deploy it to our Linux server through Jenkins and connect it to future production-branch deployments. Check the other sites affected by that pipeline too.

Replace the example name/domain with the real brand and a domain you control. With this repository available, "Create a new production website like YaPasakay called NewBrand at newbrand.com" is enough to start. The guide supplies the workflow; the agent still needs authorized GitHub, Jenkins, server, DNS, and provider access as applicable.

For a Yapabili-like site, use the `ricardo-alvaro-1988/PiKap` repository and its own `AGENTS.md` and server guide. The apps share a Linux host but have different projects, builds, configuration, and production branches.

## Architecture

```text
GitHub: ricardo-alvaro-1988/pasakay
  -> Jenkins checkout, npm dependencies, customer/admin builds
  -> .NET 9 API publish: self-contained Linux x64
  -> one tar.gz package -> SSH -> per-site systemd deployments

Browser -> domain DNS -> Linux Nginx HTTPS
  -> site's loopback port -> YaPasakay.Api
     -> customer static site at / and admin assets at /ops
     -> site's SQL Server database and persistent uploads
```

- Backend entry project: `backend/YaPasakay.Api/YaPasakay.Api.csproj`, targeting `net9.0`, with Application and Infrastructure project references.
- Frontends: `web/customer` and `web/admin`. Jenkins runs `npm ci` for each; `deploy/sync-wwwroot.sh` builds and copies output into API `wwwroot`, with admin under `ops`.
- Jenkins publishes a self-contained executable `YaPasakay.Api` with `UseAppHost=true`. Do not substitute Yapabili's framework-dependent `PiKap.Web.dll` deployment.
- Recorded host: `yapasakay.com`, IP `162.35.171.92`; verify current DNS and host access. SQL Server is documented in Docker container `sqlserver`; applications use systemd and Nginx.
- SSH credential reference: `yapasakay-prod-ssh`. Use authorized credential access; do not write secret values into documentation or Git.

## Branch and site inventory

Current routing is defined in [deploy/branch-targets.sh](branch-targets.sh), called by [Jenkinsfile](../Jenkinsfile).

| Branch | Site/service identifier | Loopback port |
| --- | --- | --- |
| Staging | yapasakay | 5003 |
| main | pricebadz | 5004 |
| main | pasakyaman | 5005 |
| main | trygoride | 5006 |
| main | pasakya | 5011 |
| main | parasakay | 5012 |
| main | angkasna | 5013 |

The public `yapasakay.com` is the staging deployment target in this repository. Production means the `main` release stream and its configured targets; it does not mean deploying to the SSH host's namesake website. Job names recorded in [branch-deployments.md](branch-deployments.md) are `Yapasakay-Staging` and `Yapasakay`; verify effective live job branch settings.

For each `<site>`, routing specifies `/var/www/<site>`, `<site>.service`, `/etc/<site>/<site>-api.env`, `http://127.0.0.1:<port>/health`, and `/var/www/releases/<site>`. Historical records list databases `yapasakaydb`, `pricebadzDB`, `pasakyamanDB`, and `trygorideDB`; verify all actual database mappings from protected configuration without exposing credentials.

Persistent paths conventionally include `/var/lib/<site>/uploads`, `/var/log/<site>`, and `/var/lib/<site>/release.json`. The helper supports environment overrides, so inspect actual values. Its default backup root is `/var/www/backups`, with site-prefixed backup directories. Preserve settings, runtime storage, and any data-protection keys across releases.

Jenkins enables GitHub push triggers, two-minute SCM polling, and disables concurrent builds within the job. PR builds with `CHANGE_ID` and unlisted branches do not enter deployment. A new Git branch alone does not create a Jenkins job or configure DNS. Production sites receive the same package sequentially. The pipeline uses Pricebadz's release metadata as the version source for the other production brands; YapasaKay staging advances separately. Inspect the current pipeline before adding a new brand to this rule.

Yapabili's separate pipeline records ports 5007 through 5010 for Yapabili, Pakinaman, Yapabili Demo, and Pasiqlo. Treat those as occupied unless live evidence says otherwise. Check all listening ports and service reservations across the shared host; do not choose a port from an old example.

## Workflow for the agent

1. Establish the name, domain, reference application, and requested scope. Default a YaPasakay-like site to this shared application with separate data. Ask for missing essential decisions/access while doing independent preparation. Do not invent credentials or live server facts.
2. Fetch and inspect current source and routing. Preserve teammates' changes and unrelated local files. Prepare changes on an isolated branch/checkout if the working tree is dirty. Use normal fast-forward pushes; if the remote advances, integrate it and recheck the diff.
3. Record the target in `deploy/<site>-production.md`: brand, domain/aliases, repository, production branch/job, unused port, service/user, application path, database/login, protected configuration, persistent storage, and verified/pending status. Discover current Linux version, Nginx layout, available capacity, and provider requirements through authorized access.
4. Inspect the existing brand/admin settings and frontend/API behavior. Set the site's name, logos, favicon, theme, public origin, CORS, and integration configuration. Verify the implementation supports any new host mappings. Keep existing brands correct.
5. Default to a fresh database, a dedicated application login, and minimal required seed data. Do not copy real users, rides, wallets, uploads, or credentials simply because the historical runbook clones a site. Clone data only if explicitly requested, with the intended source and destination verified. Prepare schema changes and backup/recovery appropriate to the application.
6. Prepare reviewable server configuration: separate directories, runtime user/permissions, protected environment file, startup script, systemd service, loopback binding, uploads/logs/keys, Nginx domain routing, DNS, and HTTPS. The current runbook's environment conventions include `PublicOrigin`, `CorsOrigins__*`, `YP_DB_*`, `YP_UPLOAD_ROOT`, `Storage__UploadsPath`, `YP_LOG_ROOT`, `YP_RELEASE_FILE`, and `Release__MetadataPath`; verify the current startup code before using them. Keep secrets out of Git, console output, and shell tracing.
7. Configure brand-appropriate Google sign-in, Maps restrictions, notifications, payment settings, and other integrations actually used. Inspect `/api/public/auth` and `/api/public/maps` and verify the new origins in the provider console. Do not assume another site's OAuth registration automatically allows a new domain. Do not borrow Yapabili's callback or configuration names without checking this application.
8. Add the new production target to `deploy/branch-targets.sh`. Review `Jenkinsfile` for version-source behavior and other site-specific conditions. Update `deploy/check-branch-routing.py`, documentation, and applicable logs/admin mappings. Keep `Staging` routed to its existing target. Provision the target before the pipeline tries to deploy to it. Use a separate job only when independent release timing is requested and explicitly designed.
9. Run relevant frontend/backend checks, a publish matching Jenkins, and `python deploy/check-branch-routing.py` in an environment with Bash. Check the artifact for secrets/runtime data. Validate Nginx syntax and service settings before activation. Inspect the deployment helper's preservation, health, rollback, and retention behavior; application-file rollback does not restore database changes or already deployed sibling sites.
10. Follow the user's scope: prepare-only requests stop at a concrete reviewable implementation; explicit deployment requests permit execution with available authorized access. Respect tool approval requirements. Explain that a production pipeline run affects all its configured sites. Missing access/provider settings are specific blockers, not grounds to claim success. This documentation request alone does not authorize live server changes.
11. After deployment, verify the full pipeline result, service health, HTTPS/redirects, frontend/admin assets, brand identity, database isolation, uploads, sign-in, maps, and core ride flows as applicable. Check other sites affected by the shared build. Report URL, job/build, commit, branch, service/port, database name, verification results, and remaining limitations. Update the per-site record without secrets.

## Corrections to historical runbooks

[new-production-site.md](new-production-site.md) contains useful provisioning examples but is not an unconditional script to execute:

- Routing now lives in `deploy/branch-targets.sh`; an old instruction to add a literal target list to `Jenkinsfile` is outdated.
- Its four-site inventory is incomplete; current routing above has seven targets across staging and production.
- Port `5007` is an old example and is recorded as Yapabili's port on the shared host. Verify an unused port across all services.
- Its database-clone and environment-copy recipe describes a specific cloning approach. Fresh isolated data/configuration is the default for a new brand; copying existing live data requires an explicit request.
- Do not execute restore commands using `REPLACE` against an existing database as routine provisioning. Resolve the intended new database and inspect its state first.
- Existing domains, credentials references, and provider settings in examples must be checked; possession of documentation is not access authorization.

## Sources

- [Jenkins pipeline](../Jenkinsfile), [branch targets](branch-targets.sh), [routing checks](check-branch-routing.py)
- [Frontend build/sync](sync-wwwroot.sh), [deployment helper](jenkins-deploy.sh), [backend project](../backend/YaPasakay.Api/YaPasakay.Api.csproj)
- [Historical provisioning runbook](new-production-site.md), [branch deployments](branch-deployments.md)
- [Pricebadz](pricebadz-production.md), [Pasakyaman](pasakyaman-production.md), [Trygoride](trygoride-production.md), [Pasakya](pasakya-production.md) deployment records

Website work does not imply mobile app builds. Every mobile brand/role needs its own identity and signing/provider configuration; inspect the relevant mobile project before any separate release task.
