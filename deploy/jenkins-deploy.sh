#!/usr/bin/env bash
set -Eeuo pipefail

package="${1:?package path is required}"
app_dir="${2:-/var/www/yapasakay}"
service="${3:-yapasakay.service}"
build_number="${4:-manual}"
commit="${5:-unknown}"

timestamp="$(date +%Y%m%d%H%M%S)"
release_root="/var/www/releases/yapasakay"
backup_root="/var/www/backups"
release_dir="${release_root}/${timestamp}-${build_number}-${commit:0:8}"
backup_dir="${backup_root}/yapasakay-${timestamp}"
env_file="/etc/yapasakay/yapasakay-api.env"
sudo_cmd=""

if [ "$(id -u)" -ne 0 ]; then
    sudo_cmd="sudo"
fi

if [ -f "${env_file}" ]; then
    # shellcheck disable=SC1090
    source "${env_file}"
fi

upload_dir="${YP_UPLOAD_ROOT:-${Storage__UploadsPath:-/var/lib/yapasakay/uploads}}"
log_dir="${YP_LOG_ROOT:-/var/log/yapasakay}"
release_file="${YP_RELEASE_FILE:-${Release__MetadataPath:-/var/lib/yapasakay/release.json}}"
legacy_uploads="${app_dir}/wwwroot/uploads"

migrate_legacy_uploads() {
    if [ -d "${legacy_uploads}" ]; then
        ${sudo_cmd} mkdir -p "${upload_dir}"
        ${sudo_cmd} rsync -a "${legacy_uploads}/" "${upload_dir}/"
    fi
}

next_release_version() {
    local current_version=""
    if [ -f "${release_file}" ]; then
        current_version="$(${sudo_cmd} sed -nE 's/^[[:space:]]*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "${release_file}" | head -n 1 || true)"
    fi

    if [[ "${current_version}" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
        printf '%s.%s.%s' "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}" "$((BASH_REMATCH[3] + 1))"
    elif [[ "${build_number}" =~ ^[0-9]+$ ]]; then
        printf '1.0.%s' "${build_number}"
    else
        printf '1.0.1'
    fi
}

write_release_metadata() {
    local version
    local updated_at
    version="$(next_release_version)"
    updated_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

    ${sudo_cmd} mkdir -p "$(dirname "${release_file}")"
    ${sudo_cmd} tee "${release_file}.tmp" >/dev/null <<EOF
{
  "app": "Ya! Pasakay",
  "version": "${version}",
  "updatedAtUtc": "${updated_at}",
  "buildNumber": "${build_number}",
  "commit": "${commit}",
  "package": "$(basename "${package}")"
}
EOF
    ${sudo_cmd} mv "${release_file}.tmp" "${release_file}"
    ${sudo_cmd} chmod 0644 "${release_file}"
    echo "Release metadata updated: ${version} ${updated_at}"
}

wait_for_health() {
    for _ in {1..30}; do
        if ${sudo_cmd} systemctl is-active --quiet "${service}" &&
            curl -fsS http://127.0.0.1:5003/health >/dev/null; then
            return 0
        fi
        sleep 1
    done

    ${sudo_cmd} systemctl status "${service}" --no-pager -l || true
    return 1
}

rollback() {
    status=$?
    if [ "$status" -ne 0 ]; then
        echo "Deployment failed. Restoring ${backup_dir}."
        if [ -d "${backup_dir}" ]; then
            ${sudo_cmd} rsync -a --delete \
                --exclude 'wwwroot/uploads/' \
                --exclude 'logs/' \
                "${backup_dir}/" "${app_dir}/" || true
        fi
        ${sudo_cmd} systemctl start "${service}" || true
    fi
    exit "$status"
}
trap rollback EXIT

if [ ! -f "${package}" ]; then
    echo "Package not found: ${package}" >&2
    exit 1
fi

${sudo_cmd} mkdir -p "${release_dir}" "${backup_dir}" "${app_dir}" "${backup_root}" "${upload_dir}" "${log_dir}" "$(dirname "${release_file}")"
${sudo_cmd} tar -xzf "${package}" -C "${release_dir}"

if [ ! -f "${release_dir}/YaPasakay.Api" ]; then
    echo "Package is missing YaPasakay.Api executable." >&2
    exit 1
fi

${sudo_cmd} chmod +x "${release_dir}/YaPasakay.Api"

migrate_legacy_uploads

${sudo_cmd} rsync -a \
    --exclude 'wwwroot/uploads/' \
    --exclude 'logs/' \
    "${app_dir}/" "${backup_dir}/"

${sudo_cmd} systemctl stop "${service}"
migrate_legacy_uploads
${sudo_cmd} rsync -a --delete \
    --exclude 'appsettings.json' \
    --exclude 'appsettings.Production.json' \
    --exclude 'wwwroot/uploads/' \
    --exclude 'logs/' \
    "${release_dir}/" "${app_dir}/"
${sudo_cmd} chmod +x "${app_dir}/YaPasakay.Api"
${sudo_cmd} systemctl start "${service}"

wait_for_health
write_release_metadata

trap - EXIT

${sudo_cmd} rm -f "${package}" /tmp/yapasakay-jenkins-deploy.sh || true
${sudo_cmd} find "${release_root}" -mindepth 1 -maxdepth 1 -type d -printf '%T@ %p\n' \
    | sort -n \
    | head -n -5 \
    | cut -d' ' -f2- \
    | xargs -r ${sudo_cmd} rm -rf || true
${sudo_cmd} find "${backup_root}" -mindepth 1 -maxdepth 1 -type d -name 'yapasakay-*' -printf '%T@ %p\n' \
    | sort -n \
    | head -n -10 \
    | cut -d' ' -f2- \
    | xargs -r ${sudo_cmd} rm -rf || true

echo "Deployed ${package} to ${app_dir}."
