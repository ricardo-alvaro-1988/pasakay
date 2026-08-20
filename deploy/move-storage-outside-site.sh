#!/usr/bin/env bash
set -Eeuo pipefail

app_dir="${1:-/var/www/yapasakay}"
service="${2:-yapasakay.service}"
upload_dir="${3:-/var/lib/yapasakay/uploads}"
log_dir="${4:-/var/log/yapasakay}"

env_file="/etc/yapasakay/yapasakay-api.env"
start_script="/etc/yapasakay/start-yapasakay-api.sh"
legacy_uploads="${app_dir}/wwwroot/uploads"
backup_root="/var/www/backups"
timestamp="$(date +%Y%m%d%H%M%S)"
backup_dir="${backup_root}/yapasakay-storage-migration-${timestamp}"

mkdir -p "${upload_dir}" "${log_dir}" "${backup_dir}"

wait_for_health() {
    for _ in {1..30}; do
        if systemctl is-active --quiet "${service}" &&
            curl -fsS http://127.0.0.1:5003/health >/dev/null; then
            return 0
        fi
        sleep 1
    done

    systemctl status "${service}" --no-pager -l || true
    return 1
}

cp "${app_dir}/YaPasakay.Api.dll" "${backup_dir}/" 2>/dev/null || true
cp "${app_dir}/YaPasakay.Api.pdb" "${backup_dir}/" 2>/dev/null || true
cp "${env_file}" "${backup_dir}/yapasakay-api.env" 2>/dev/null || true
cp "${start_script}" "${backup_dir}/start-yapasakay-api.sh" 2>/dev/null || true

sample=""
if [ -d "${legacy_uploads}" ]; then
    sample="$(find "${legacy_uploads}" -type f \( -iname '*.png' -o -iname '*.jpg' -o -iname '*.jpeg' -o -iname '*.webp' -o -iname '*.svg' \) | head -n 1 || true)"
    sample="${sample#"${legacy_uploads}/"}"
fi

systemctl stop "${service}"

if [ -d "${legacy_uploads}" ]; then
    rsync -a "${legacy_uploads}/" "${upload_dir}/"
    mv "${legacy_uploads}" "${backup_dir}/wwwroot-uploads"
fi

install -m 0644 /tmp/YaPasakay.Api.storage.dll "${app_dir}/YaPasakay.Api.dll"
install -m 0644 /tmp/YaPasakay.Api.storage.pdb "${app_dir}/YaPasakay.Api.pdb"

if grep -q '^YP_UPLOAD_ROOT=' "${env_file}"; then
    sed -i "s|^YP_UPLOAD_ROOT=.*|YP_UPLOAD_ROOT=${upload_dir}|" "${env_file}"
else
    printf '\nYP_UPLOAD_ROOT=%s\n' "${upload_dir}" >> "${env_file}"
fi

if grep -q '^YP_LOG_ROOT=' "${env_file}"; then
    sed -i "s|^YP_LOG_ROOT=.*|YP_LOG_ROOT=${log_dir}|" "${env_file}"
else
    printf 'YP_LOG_ROOT=%s\n' "${log_dir}" >> "${env_file}"
fi

if ! grep -q 'Storage__UploadsPath' "${start_script}"; then
    sed -i '/^exec \/var\/www\/yapasakay\/YaPasakay.Api/i export YP_UPLOAD_ROOT="${YP_UPLOAD_ROOT:-/var/lib/yapasakay/uploads}"\
export YP_LOG_ROOT="${YP_LOG_ROOT:-/var/log/yapasakay}"\
export Storage__UploadsPath="${Storage__UploadsPath:-$YP_UPLOAD_ROOT}"' "${start_script}"
fi

systemctl start "${service}"
wait_for_health

printf 'backup=%s\nupload=%s\nlog=%s\nsample=%s\n' "${backup_dir}" "${upload_dir}" "${log_dir}" "${sample}"
