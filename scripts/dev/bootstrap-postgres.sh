#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/bootstrap/compose.postgres.dev.yml"

export ATHERA_PG_PORT="${ATHERA_PG_PORT:-55433}"
export ATHERA_PG_ADMIN_USER="${ATHERA_PG_ADMIN_USER:-athera_dev_admin}"
export ATHERA_PG_ADMIN_PASSWORD="${ATHERA_PG_ADMIN_PASSWORD:-athera_dev_local_admin_pw}"
export ATHERA_PG_MIGRATOR_PASSWORD="${ATHERA_PG_MIGRATOR_PASSWORD:-athera_dev_local_migrator_pw}"
export ATHERA_PG_APP_PASSWORD="${ATHERA_PG_APP_PASSWORD:-athera_dev_local_app_pw}"
export ATHERA_PG_DB="${ATHERA_PG_DB:-athera_dev}"

docker compose -p athera-greenfield -f "$COMPOSE_FILE" up -d --wait

run_psql() {
  if command -v psql >/dev/null 2>&1; then
    PGPASSWORD="$ATHERA_PG_ADMIN_PASSWORD" psql -h 127.0.0.1 -p "$ATHERA_PG_PORT" -U "$ATHERA_PG_ADMIN_USER" -d "$ATHERA_PG_DB" -v ON_ERROR_STOP=1 "$@"
  else
    docker exec -i -e PGPASSWORD="$ATHERA_PG_ADMIN_PASSWORD" athera-greenfield-pg psql -U "$ATHERA_PG_ADMIN_USER" -d "$ATHERA_PG_DB" -v ON_ERROR_STOP=1 "$@"
  fi
}

run_psql <<SQL
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
    EXECUTE format(
      'CREATE ROLE athera_migrator LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS',
      '${ATHERA_PG_MIGRATOR_PASSWORD}'
    );
  ELSE
    EXECUTE format(
      'ALTER ROLE athera_migrator WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS',
      '${ATHERA_PG_MIGRATOR_PASSWORD}'
    );
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
    EXECUTE format(
      'CREATE ROLE athera_app LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS',
      '${ATHERA_PG_APP_PASSWORD}'
    );
  ELSE
    EXECUTE format(
      'ALTER ROLE athera_app WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS',
      '${ATHERA_PG_APP_PASSWORD}'
    );
  END IF;
END
\$\$;

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT CREATE, CONNECT ON DATABASE ${ATHERA_PG_DB} TO athera_migrator;
GRANT CONNECT ON DATABASE ${ATHERA_PG_DB} TO athera_app;
ALTER ROLE athera_app SET search_path = pg_catalog, public;
ALTER ROLE athera_migrator SET search_path = pg_catalog, public;
SQL

echo "PostgreSQL 18 disposable instance ready at 127.0.0.1:${ATHERA_PG_PORT}/${ATHERA_PG_DB}"
run_psql -c "SELECT rolname, rolsuper, rolbypassrls, rolcreatedb, rolcreaterole FROM pg_roles WHERE rolname IN ('athera_migrator', 'athera_app') ORDER BY rolname;"
