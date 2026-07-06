#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-.env.prod}"

usage() {
  cat <<USAGE
Usage: ./scripts/restore-postgres.sh <backup-file.dump.gz>

Restores a PostgreSQL custom-format backup into the running postgres service.
This is destructive: existing database objects can be dropped and recreated.
USAGE
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

backup_file="${1:-}"

if [[ -z "$backup_file" ]]; then
  usage >&2
  exit 2
fi

if [[ ! -f "$backup_file" ]]; then
  echo "Backup file not found: $backup_file" >&2
  exit 1
fi

backup_file="$(cd -- "$(dirname -- "$backup_file")" && pwd)/$(basename -- "$backup_file")"

cd "$REPO_DIR"

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "Missing compose file: $COMPOSE_FILE" >&2
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing environment file: $ENV_FILE" >&2
  exit 1
fi

compose() {
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
}

echo "About to restore backup into the PostgreSQL service."
echo "Backup file: $backup_file"
echo
echo "This operation is destructive. Type RESTORE to continue:"
read -r confirmation

if [[ "$confirmation" != "RESTORE" ]]; then
  echo "Restore cancelled."
  exit 0
fi

echo "Restoring PostgreSQL backup..."

gzip -dc "$backup_file" | compose exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists --no-owner --no-acl --single-transaction --exit-on-error'

echo "Restore completed."
