# Branch deployments

Develop on Staging. Yapasakay-Staging watches */Staging and deploys only yapasakay.com. Merge Staging into main to release pricebadz.com, pasakyaman.com, and trygoride.com through the existing Yapasakay job. This repository uses main, not master.

Both jobs reuse existing credentials, GitHub push triggers and the two-minute polling fallback. Existing databases, protected settings and uploads are retained. The public yapasakay.com is the staging target; no new database is created.

Production release numbers advance from Pricebadz; Pasakyaman and Trygoride reuse its version after successful deployment. Yapasakay versions advance independently. Temporary deployment scripts use site, build and commit-specific paths.

Run python3 deploy/check-branch-routing.py on Linux for shell syntax and routing checks. SiteLogs maps yapasakay to Yapasakay-Staging; the other three retain Yapasakay deployment logs.
