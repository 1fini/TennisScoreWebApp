#!/usr/bin/env bash
set -Eeuo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-.env.prod}"
SKIP_MIGRATION=false

usage() {
  cat <<USAGE
Usage: ./deploy.sh [--skip-migration]

Options:
  --skip-migration  Pull images and restart the stack without running EF migrations.
  -h, --help        Show this help message.
USAGE
}

for arg in "$@"; do
  case "$arg" in
    --skip-migration)
      SKIP_MIGRATION=true
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $arg" >&2
      usage
      exit 2
      ;;
  esac
done

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

on_error() {
  local exit_code=$?
  echo
  echo "Deployment failed with exit code $exit_code." >&2
  echo "Attempting to restart existing containers without recreating the stack..." >&2

  if compose restart traefik api webapp; then
    echo "Existing containers restarted. Current status:" >&2
    compose ps >&2 || true
  elif compose up -d --no-recreate; then
    echo "Existing stack checked without recreation. Current status:" >&2
    compose ps >&2 || true
  else
    echo "Recovery command also failed. Recent logs:" >&2
    compose logs --tail=80 >&2 || true
  fi

  exit "$exit_code"
}

trap on_error ERR

echo "Validating compose configuration"
compose config >/dev/null

echo "Pulling latest images"
compose pull

if [[ "$SKIP_MIGRATION" == false ]]; then
  echo "Running database migrations"
  compose --profile migrations run --rm migrations
else
  echo "Skipping database migrations"
fi

echo "Starting application stack"
compose up -d --remove-orphans

echo "Pruning unused images"
docker image prune -f

echo "Deployment completed"
compose ps
