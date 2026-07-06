#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"

cd "$REPO_DIR"

test_id="$(date -u +"%Y%m%d%H%M%S")-$$"
work_dir="$(mktemp -d "${TMPDIR:-/tmp}/tennisscore-backup-test.XXXXXX")"
env_file="$work_dir/.env.test"
override_file="$work_dir/docker-compose.test.yml"
backup_dir="$work_dir/backups"
compose_project="tennisscore_backup_test_$test_id"

cleanup() {
  local exit_code=$?
  COMPOSE_PROJECT_NAME="$compose_project" docker compose --env-file "$env_file" -f docker-compose.prod.yml -f "$override_file" down -v --remove-orphans >/dev/null 2>&1 || true
  rm -rf "$work_dir"
  exit "$exit_code"
}

trap cleanup EXIT

cat > "$env_file" <<ENV
API_IMAGE=1fini/tennisscoreapi:latest
MIGRATIONS_IMAGE=1fini/tennisscoreapi-migrations:latest
WEBAPP_IMAGE=1fini/tennisscore-webapp:latest
ASPNETCORE_ENVIRONMENT=Production
ENABLE_HTTPS_REDIRECTION=false
WEBAPP_HTTP_PORT=18080
DB_NAME=tennisscore_test
DB_USER=tennisscore_test
DB_PASSWORD=tennisscore_test_password
ENV

cat > "$override_file" <<YAML
services:
  postgres:
    container_name: tennisscore-postgres-backup-test-$test_id
    restart: "no"
  migrations:
    container_name: tennisscore-migrations-backup-test-$test_id
  api:
    container_name: tennisscore-api-backup-test-$test_id
  webapp:
    container_name: tennisscore-webapp-backup-test-$test_id
    ports: []
YAML

compose() {
  COMPOSE_PROJECT_NAME="$compose_project" docker compose --env-file "$env_file" -f docker-compose.prod.yml -f "$override_file" "$@"
}

echo "Starting isolated PostgreSQL test container"
compose up -d postgres

echo "Waiting for PostgreSQL readiness"
for _ in {1..30}; do
  if compose exec -T postgres sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"' >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

compose exec -T postgres sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"' >/dev/null

echo "Creating probe data"
compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -c "CREATE TABLE backup_probe (id integer PRIMARY KEY, label text NOT NULL);" -c "INSERT INTO backup_probe (id, label) VALUES (1, '\''before-restore'\'');"'

echo "Running backup script"
COMPOSE_PROJECT_NAME="$compose_project" \
COMPOSE_FILES="docker-compose.prod.yml:$override_file" \
ENV_FILE="$env_file" \
BACKUP_DIR="$backup_dir" \
RETENTION_DAYS=0 \
bash ./scripts/backup-postgres.sh

backup_file="$(find "$backup_dir" -type f -name "tennisscore-postgres-*.dump.gz" | head -n 1)"
if [[ -z "$backup_file" ]]; then
  echo "No backup file was created" >&2
  exit 1
fi

echo "Mutating database after backup"
compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -c "DROP TABLE backup_probe;"'

echo "Running restore script"
printf 'RESTORE\n' | COMPOSE_PROJECT_NAME="$compose_project" \
COMPOSE_FILES="docker-compose.prod.yml:$override_file" \
ENV_FILE="$env_file" \
bash ./scripts/restore-postgres.sh "$backup_file"

echo "Verifying restored data"
restored_value="$(compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "SELECT label FROM backup_probe WHERE id = 1;"')"

if [[ "$restored_value" != "before-restore" ]]; then
  echo "Unexpected restored value: $restored_value" >&2
  exit 1
fi

echo "Backup and restore test completed successfully"
