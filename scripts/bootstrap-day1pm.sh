#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "[1/4] Starting infra services"
DOCKER_AVAILABLE=1
if ! command -v docker >/dev/null 2>&1; then
  DOCKER_AVAILABLE=0
elif ! docker info >/dev/null 2>&1; then
  DOCKER_AVAILABLE=0
fi

if [[ "${DOCKER_AVAILABLE}" == "1" ]]; then
  docker compose -f "${ROOT_DIR}/infra/docker-compose.yml" up -d
else
  echo "Docker is not available. Falling back to local Postgres on port 5432."
fi

echo "[2/4] Ensuring database role and database exist"
if [[ "${DOCKER_AVAILABLE}" == "1" ]] && docker exec escaperoom-postgres psql -U escaperoom -d escaperoom -tAc "SELECT 1" >/dev/null 2>&1; then
  DB_MODE="docker"
  ADMIN_USER="escaperoom"
  ADMIN_DB="escaperoom"
elif [[ "${DOCKER_AVAILABLE}" == "1" ]] && docker exec escaperoom-postgres psql -U postgres -d postgres -tAc "SELECT 1" >/dev/null 2>&1; then
  DB_MODE="docker"
  ADMIN_USER="postgres"
  ADMIN_DB="postgres"
elif command -v psql >/dev/null 2>&1; then
  DB_MODE="local"
else
  echo "Could not connect to dockerized Postgres and local 'psql' is unavailable."
  echo "Install psql, or start Docker Desktop and re-run."
  exit 1
fi

if [[ "${DB_MODE}" == "docker" ]]; then
  ROLE_EXISTS="$(docker exec escaperoom-postgres psql -U "${ADMIN_USER}" -d "${ADMIN_DB}" -tAc "SELECT 1 FROM pg_roles WHERE rolname='escaperoom'")"
  if [[ "${ROLE_EXISTS}" != "1" ]]; then
    docker exec escaperoom-postgres psql -U "${ADMIN_USER}" -d "${ADMIN_DB}" -v ON_ERROR_STOP=1 -c "CREATE ROLE escaperoom WITH LOGIN PASSWORD 'escaperoom';"
  fi

  DB_EXISTS="$(docker exec escaperoom-postgres psql -U "${ADMIN_USER}" -d "${ADMIN_DB}" -tAc "SELECT 1 FROM pg_database WHERE datname='escaperoom'")"
  if [[ "${DB_EXISTS}" != "1" ]]; then
    docker exec escaperoom-postgres psql -U "${ADMIN_USER}" -d "${ADMIN_DB}" -v ON_ERROR_STOP=1 -c "CREATE DATABASE escaperoom OWNER escaperoom;"
  fi
else
  psql -v ON_ERROR_STOP=1 -d postgres -c "DO \$\$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'escaperoom') THEN CREATE ROLE escaperoom WITH LOGIN PASSWORD 'escaperoom'; END IF; END \$\$;"
  DB_EXISTS_LOCAL="$(psql -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='escaperoom'")"
  if [[ "${DB_EXISTS_LOCAL}" != "1" ]]; then
    psql -v ON_ERROR_STOP=1 -d postgres -c "CREATE DATABASE escaperoom OWNER escaperoom;"
  fi
fi

echo "[3/4] Applying migrations"
(
  cd "${ROOT_DIR}/src/backend"
  dotnet tool restore
  if [[ "${DB_MODE}" == "docker" ]]; then
    ConnectionStrings__Postgres="Host=localhost;Port=55432;Database=escaperoom;Username=escaperoom;Password=escaperoom" \
      dotnet ef database update --project EscapeRoom.Infrastructure --startup-project EscapeRoom.Api
  else
    ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=escaperoom;Username=escaperoom;Password=escaperoom" \
      dotnet ef database update --project EscapeRoom.Infrastructure --startup-project EscapeRoom.Api
  fi
)

echo "[4/4] Seeding baseline users and sample room"
(
  cd "${ROOT_DIR}/src/backend/EscapeRoom.Api"
  if [[ "${DB_MODE}" == "docker" ]]; then
    ConnectionStrings__Postgres="Host=localhost;Port=55432;Database=escaperoom;Username=escaperoom;Password=escaperoom" \
      dotnet run -- --seed
  else
    ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=escaperoom;Username=escaperoom;Password=escaperoom" \
      dotnet run -- --seed
  fi
)

echo "Bootstrap complete."
