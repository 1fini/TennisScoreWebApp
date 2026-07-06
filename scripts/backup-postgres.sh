#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
COMPOSE_FILES="${COMPOSE_FILES:-$COMPOSE_FILE}"
ENV_FILE="${ENV_FILE:-.env.prod}"
BACKUP_DIR="${BACKUP_DIR:-./backups}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

cd "$REPO_DIR"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing environment file: $ENV_FILE" >&2
  exit 1
fi

COMPOSE_ARGS=()
IFS=':' read -r -a compose_files <<< "$COMPOSE_FILES"
for compose_file in "${compose_files[@]}"; do
  if [[ ! -f "$compose_file" ]]; then
    echo "Missing compose file: $compose_file" >&2
    exit 1
  fi
  COMPOSE_ARGS+=("-f" "$compose_file")
done

compose() {
  docker compose --env-file "$ENV_FILE" "${COMPOSE_ARGS[@]}" "$@"
}

mkdir -p "$BACKUP_DIR"

timestamp="$(date -u +"%Y%m%dT%H%M%SZ")"
backup_file="$BACKUP_DIR/tennisscore-postgres-$timestamp.dump.gz"
tmp_file="$backup_file.tmp"

cleanup() {
  rm -f "$tmp_file"
}

trap cleanup EXIT

echo "Creating PostgreSQL backup: $backup_file"

compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --no-owner --no-acl' | gzip > "$tmp_file"

mv "$tmp_file" "$backup_file"
chmod 600 "$backup_file"

if [[ "$RETENTION_DAYS" =~ ^[0-9]+$ ]] && [[ "$RETENTION_DAYS" -gt 0 ]]; then
  find "$BACKUP_DIR" -type f -name "tennisscore-postgres-*.dump.gz" -mtime +"$RETENTION_DAYS" -delete
fi

echo "Backup completed: $backup_file"
