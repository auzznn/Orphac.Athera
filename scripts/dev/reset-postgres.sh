#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/bootstrap/compose.postgres.dev.yml"

docker compose -p athera-greenfield -f "$COMPOSE_FILE" down -v
echo "Stopped disposable PostgreSQL container and removed athera_greenfield_pg_dev volume."
