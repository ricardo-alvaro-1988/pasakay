#!/usr/bin/env bash
set -Eeuo pipefail

timezone="${1:-Asia/Manila}"
app_service="${2:-yapasakay.service}"
app_env="/etc/yapasakay/yapasakay-api.env"
app_start="/etc/yapasakay/start-yapasakay-api.sh"
sql_container="sqlserver"

wait_for_sql() {
    for _ in {1..60}; do
        if docker exec \
            -e SQLUSER="${YP_DB_USER:-}" \
            -e SQLPASS="${YP_DB_PASSWORD:-}" \
            -e SQLDB="${YP_DB_NAME:-master}" \
            "${sql_container}" \
            bash -lc '/opt/mssql-tools18/bin/sqlcmd -S localhost -U "$SQLUSER" -P "$SQLPASS" -d "$SQLDB" -C -Q "SELECT 1" >/dev/null 2>&1'; then
            return 0
        fi
        sleep 2
    done

    docker logs --tail 80 "${sql_container}" || true
    return 1
}

wait_for_app() {
    for _ in {1..30}; do
        if systemctl is-active --quiet "${app_service}" &&
            curl -fsS http://127.0.0.1:5003/health >/dev/null; then
            return 0
        fi
        sleep 1
    done

    systemctl status "${app_service}" --no-pager -l || true
    return 1
}

set_env_line() {
    local key="${1:?key is required}"
    local value="${2:?value is required}"

    if grep -q "^${key}=" "${app_env}"; then
        sed -i "s|^${key}=.*|${key}=${value}|" "${app_env}"
    else
        printf '\n%s=%s\n' "${key}" "${value}" >> "${app_env}"
    fi
}

timedatectl set-timezone "${timezone}"
source "${app_env}"
set_env_line TZ "${timezone}"

if grep -q '^export TZ=' "${app_start}"; then
    sed -i 's|^export TZ=.*|export TZ="${TZ:-Asia/Manila}"|' "${app_start}"
else
    sed -i '/^source \/etc\/yapasakay\/yapasakay-api.env/a export TZ="${TZ:-Asia/Manila}"' "${app_start}"
fi

if docker inspect "${sql_container}" >/dev/null 2>&1; then
    image="$(docker inspect "${sql_container}" --format '{{.Config.Image}}')"
    restart_policy="$(docker inspect "${sql_container}" --format '{{.HostConfig.RestartPolicy.Name}}')"
    network_mode="$(docker inspect "${sql_container}" --format '{{.HostConfig.NetworkMode}}')"
    env_file="$(mktemp)"

    docker inspect "${sql_container}" --format '{{range .Config.Env}}{{println .}}{{end}}' |
        grep -v '^PATH=' |
        grep -v '^TZ=' > "${env_file}"
    printf 'TZ=%s\n' "${timezone}" >> "${env_file}"

    docker stop "${sql_container}" >/dev/null
    docker rm "${sql_container}" >/dev/null
    docker run -d \
        --name "${sql_container}" \
        --restart "${restart_policy:-unless-stopped}" \
        --network "${network_mode:-bridge}" \
        --env-file "${env_file}" \
        -p 127.0.0.1:1433:1433 \
        -v sqlserver_sqlserver_data:/var/opt/mssql \
        -v /opt/sqlserver/backups:/var/opt/mssql/backups \
        "${image}" >/dev/null
    rm -f "${env_file}"

    wait_for_sql
fi

systemctl restart "${app_service}"
wait_for_app

printf 'Host timezone: '
timedatectl show -p Timezone --value
printf 'Host time: '
date
printf 'SQL container time: '
docker exec "${sql_container}" date
