# Website architecture and new production sites

For requests to create a website similar to YaPasakay or Yapabili, or to configure Linux hosting, Jenkins, or production deployment, first read [the server and new-site guide](deploy/server-architecture-and-new-sites.md).

- This repository is `ricardo-alvaro-1988/pasakay`, the YaPasakay application. Its production branch is `main`; `Staging` deploys the public `yapasakay.com` site. Verify current routing before release.
- Default a new YaPasakay-like website to the shared application with its own domain, database, configuration, service, and persistent storage. Follow the guide and ask only for missing decisions or access that block progress.
- Yapabili is a separate application in `ricardo-alvaro-1988/PiKap`, using `master` for production. Inspect that repository and its guide for Yapabili requests; do not mix its build, settings, or pipeline with this application.
- Existing provisioning runbooks contain historical examples. Use current executable routing and the guide's corrections; inspect live state before assigning ports or changing services.
- Preserve other contributors' changes. Keep unrelated local work out of commits; use an isolated checkout when necessary, incorporate latest remote changes, and never force-push shared branches.
- Follow the user's requested scope. Preparing documentation or a website does not itself request publication. An explicit deployment request authorizes deployment within that scope; explain which sites the shared pipeline affects.
- Every mobile brand and role is a separate app identity. Website work does not authorize mobile builds or publication. Never reuse another app's package ID, branding, private signing keys, or provider registration to create a new app.
