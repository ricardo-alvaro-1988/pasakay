#!/usr/bin/env bash
set -euo pipefail
branch="${1:?branch required}"
host="${DEPLOY_HOST:?deployment host required}"
case "$branch" in
    Staging|origin/Staging) sites='yapasakay:5003' ;;
    main|origin/main) sites='pricebadz:5004 pasakyaman:5005 trygoride:5006 pasakya:5011' ;;
    *) echo "Branch is not a deployment target" >&2; exit 1 ;;
esac
for target in $sites; do
    site="${target%:*}"
    port="${target##*:}"
    printf '%s|%s|/var/www/%s|%s.service|/etc/%s/%s-api.env|http://127.0.0.1:%s/health|/var/www/releases/%s\n' \
        "$site" "$host" "$site" "$site" "$site" "$site" "$port" "$site"
done
