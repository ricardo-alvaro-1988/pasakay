#!/usr/bin/env bash
set -Eeuo pipefail

deploy_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "${deploy_root}/.." && pwd)"

api_wwwroot="${root}/backend/YaPasakay.Api/wwwroot"
customer_root="${root}/web/customer"
admin_root="${root}/web/admin"
customer_dist="${customer_root}/dist"
admin_dist="${admin_root}/dist"
ops_root="${api_wwwroot}/ops"

npm --prefix "${customer_root}" run build
npm --prefix "${admin_root}" run build

mkdir -p "${api_wwwroot}"
find "${api_wwwroot}" -mindepth 1 -maxdepth 1 ! -name uploads -exec rm -rf {} +

cp -a "${customer_dist}/." "${api_wwwroot}/"
mkdir -p "${ops_root}"
cp -a "${admin_dist}/." "${ops_root}/"

echo "Synced customer app to ${api_wwwroot}"
echo "Synced admin app to ${ops_root}"
