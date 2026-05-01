#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "[1/4] Starting infra services"
docker compose -f "${ROOT_DIR}/infra/docker-compose.yml" up -d

echo "[2/4] Ensuring database role and database exist"
if docker exec escaperoom-postgres psql -U escaperoom -d escaperoom -tAc "SELECT 1" >/dev/null 2>&1; then
  ADMIN_USER="escaperoom"
  ADMIN_DB="escaperoom"
elif docker exec escaperoom-postgres psql -U postgres -d postgres -tAc "SELECT 1" >/dev/null 2>&1; then
  ADMIN_USER="postgres"
  ADMIN_DB="postgres"
else
  echo "Could not connect to Postgres with either escaperoom or postgres user."
  echo "If this is stale local state, run: docker compose -f infra/docker-compose.yml down -v"
  exit 1
fi

ROLE_EXISTS="$(docker exec escaperoom-postgres psql -U "${ADMIN_USER}" -d "${ADMIN_DB}" -tAc "SELECT 1 FROM pg_roles WHERE rolname='escaperoom'")"
if [[ "${ROLE_EXISTS}" != "1" ]]; then
  docker exec escaperoom-postgres psql -U "${ADMIN_USER}" -d "${ADMIN_DB}" -v ON_ERROR_STOP=1 -c "CREATE ROLE escaperoom WITH LOGIN PASSWORD 'escaperoom';"
fi

DB_EXISTS="$(docker exec escaperoom-postgres psql -U "${ADMIN_USER}" -d "${ADMIN_DB}" -tAc "SELECT 1 FROM pg_database WHERE datname='escaperoom'")"
if [[ "${DB_EXISTS}" != "1" ]]; then
  docker exec escaperoom-postgres psql -U "${ADMIN_USER}" -d "${ADMIN_DB}" -v ON_ERROR_STOP=1 -c "CREATE DATABASE escaperoom OWNER escaperoom;"
fi

echo "[3/4] Applying migrations"
(
  cd "${ROOT_DIR}/src/backend"
  dotnet tool restore
  dotnet ef database update --project EscapeRoom.Infrastructure --startup-project EscapeRoom.Api
)

echo "[4/4] Seeding baseline users and sample room"
(
  cd "${ROOT_DIR}/src/backend/EscapeRoom.Api"
  dotnet run -- --seed
)

echo "Bootstrap complete."
