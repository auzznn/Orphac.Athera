#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if [[ -n "${ATHERA_E2E_DB+x}" ]]; then
  echo 'e2e:fresh allocates its own database; unset ATHERA_E2E_DB' >&2
  exit 2
fi
db="athera_e2e_$(date -u +%Y%m%d%H%M%S)_$$"
port="${ATHERA_PG_PORT:-55433}"
[[ "$port" =~ ^[0-9]{1,5}$ ]] || { echo 'Invalid local PostgreSQL port' >&2; exit 2; }
admin="${ATHERA_PG_ADMIN_USER:-athera_dev_admin}"
admin_pw="${ATHERA_PG_ADMIN_PASSWORD:-athera_dev_local_admin_pw}"
run_psql() {
  local target="$1"; shift
  if command -v psql >/dev/null 2>&1; then
    PGPASSWORD="$admin_pw" psql -X -h 127.0.0.1 -p "$port" -U "$admin" -d "$target" -v ON_ERROR_STOP=1 "$@"
  else
    [[ "$(docker port athera-greenfield-pg 5432/tcp)" == "127.0.0.1:$port" ]] || { echo 'Local PostgreSQL port mapping mismatch' >&2; return 1; }
    PGPASSWORD="$admin_pw" docker exec -i -e PGPASSWORD athera-greenfield-pg psql -X -U "$admin" -d "$target" -v ON_ERROR_STOP=1 "$@"
  fi
}
run_psql postgres <<SQL
CREATE DATABASE "$db" OWNER athera_migrator;
REVOKE ALL ON DATABASE "$db" FROM PUBLIC;
GRANT CONNECT ON DATABASE "$db" TO athera_app;
SQL
run_psql "$db" -c 'REVOKE CREATE ON SCHEMA public FROM PUBLIC;'
printf 'Fresh local database (retained for inspection): %s\n' "$db"
base="Host=127.0.0.1;Port=$port;Database=$db"
migrator_pw="${ATHERA_PG_MIGRATOR_PASSWORD:-athera_dev_local_migrator_pw}"
app_pw="${ATHERA_PG_APP_PASSWORD:-athera_dev_local_app_pw}"
export ATHERA_DEMO_PASSWORD="${ATHERA_DEMO_PASSWORD:-AtheraDemo!2026}"
ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__Migrator="$base;Username=athera_migrator;Password=\"${migrator_pw//\"/\"\"}\"" dotnet run --project "$ROOT_DIR/src/Orphac.Athera.Api" --no-launch-profile -- --migrate --seed-demo
export ConnectionStrings__Runtime="$base;Username=athera_app;Password=\"${app_pw//\"/\"\"}\""
export PLAYWRIGHT_BASE_URL=http://127.0.0.1:3200
exec npm --prefix "$ROOT_DIR/apps/web" run e2e -- "$@"
