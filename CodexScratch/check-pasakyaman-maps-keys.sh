#!/usr/bin/env bash
set -Eeuo pipefail

source /etc/pasakyaman/pasakyaman-api.env

browser_key="${Maps__BrowserApiKey:-}"
server_key="${Maps__GoogleApiKey:-}"

if [ -z "${browser_key}" ] || [ -z "${server_key}" ]; then
    echo "maps keys: missing"
    exit 1
fi

if [ "${browser_key}" = "${server_key}" ]; then
    echo "maps env: BrowserApiKey and GoogleApiKey match"
else
    echo "maps env: BrowserApiKey and GoogleApiKey do not match"
fi

js_body="$(mktemp)"
directions_body="$(mktemp)"
trap 'rm -f "${js_body}" "${directions_body}"' EXIT

js_code="$(
    curl -sS \
        -o "${js_body}" \
        -w '%{http_code}' \
        -H 'Referer: https://pasakyaman.com/' \
        "https://maps.googleapis.com/maps/api/js?key=${browser_key}&libraries=places"
)"

if [ "${js_code}" != "200" ]; then
    echo "maps javascript: HTTP ${js_code}"
    exit 1
fi

if grep -Eq 'Google Maps JavaScript API error|InvalidKeyMapError|RefererNotAllowedMapError|ApiNotActivatedMapError|BillingNotEnabledMapError' "${js_body}"; then
    echo "maps javascript: failed"
    grep -Eo 'Google Maps JavaScript API error: [A-Za-z0-9_]+' "${js_body}" | head -n 1 || true
    exit 1
fi

if grep -q 'google.maps' "${js_body}"; then
    echo "maps javascript: ok"
else
    echo "maps javascript: unexpected response"
    exit 1
fi

directions_code="$(
    curl -sS \
        -o "${directions_body}" \
        -w '%{http_code}' \
        "https://maps.googleapis.com/maps/api/directions/json?origin=10.3157,123.8854&destination=10.3000,123.9000&mode=driving&units=metric&key=${server_key}"
)"

if [ "${directions_code}" != "200" ]; then
    echo "directions api: HTTP ${directions_code}"
    exit 1
fi

directions_status="$(
    grep -E '"status"[[:space:]]*:' "${directions_body}" \
        | head -n 1 \
        | sed -E 's/.*"status"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/'
)"

echo "directions api: ${directions_status:-unknown}"

if [ "${directions_status}" != "OK" ]; then
    grep -E '"error_message"[[:space:]]*:' "${directions_body}" \
        | head -n 1 \
        | sed -E 's/.*"error_message"[[:space:]]*:[[:space:]]*"([^"]+)".*/directions error: \1/' || true
    exit 1
fi
