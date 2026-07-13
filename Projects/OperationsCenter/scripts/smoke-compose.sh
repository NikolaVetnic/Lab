#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

WEB_URL="${WEB_URL:-http://localhost:${WEB_PORT:-8080}}"
API_URL="${API_URL:-http://localhost:${API_PORT:-5000}}"
ADMIN_EMAIL="${SMOKE_ADMIN_EMAIL:-admin@operations-center.local}"
ADMIN_PASSWORD="${DEV_SEED_ADMIN_PASSWORD:-Admin123!}"

cleanup() {
  if [[ "${SMOKE_KEEP_STACK:-0}" != "1" ]]; then
    docker compose down >/dev/null 2>&1 || true
  fi
}

trap cleanup EXIT

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "Missing required command: $1"
    exit 1
  }
}

wait_for_url() {
  local url="$1"
  local attempts="${2:-90}"

  for _ in $(seq 1 "$attempts"); do
    if curl -fsS "$url" >/dev/null 2>&1; then
      return 0
    fi

    sleep 1
  done

  echo "Timed out waiting for $url"
  return 1
}

require_cmd docker
require_cmd curl
require_cmd jq

echo "Building and starting compose stack..."
docker compose up -d --build

echo "Waiting for API health and readiness..."
wait_for_url "$API_URL/health"
wait_for_url "$API_URL/ready"

echo "Checking migration service completion..."
migration_container_id="$(docker compose ps -a -q operations-center-migrations)"
if [[ -z "$migration_container_id" ]]; then
  echo "Could not resolve operations-center-migrations container."
  exit 1
fi

migration_exit_code="$(docker inspect "$migration_container_id" --format '{{.State.ExitCode}}')"
if [[ "$migration_exit_code" != "0" ]]; then
  echo "Migration service failed with exit code $migration_exit_code"
  docker compose logs operations-center-migrations
  exit 1
fi

echo "Checking frontend root and deep-link shell..."
root_html="$(curl -fsS "$WEB_URL/")"
if ! grep -qi '<!doctype html>' <<<"$root_html"; then
  echo "Frontend root did not return the SPA shell."
  exit 1
fi

echo "Logging in through frontend proxy..."
token="$({
  curl -fsS \
    -H 'Content-Type: application/json' \
    -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" \
    "$WEB_URL/api/auth/login"
} | jq -r '.accessToken')"

if [[ -z "$token" || "$token" == "null" ]]; then
  echo "Login did not return an access token."
  exit 1
fi

echo "Listing incidents through frontend proxy..."
incident_list_json="$({
  curl -fsS \
    -H "Authorization: Bearer $token" \
    "$WEB_URL/api/incidents"
})"
incident_count="$(jq 'length' <<<"$incident_list_json")"
echo "Incident count: $incident_count"

echo "Creating incident through frontend proxy..."
created_json="$({
  curl -fsS \
    -X POST \
    -H "Authorization: Bearer $token" \
    -H 'Content-Type: application/json' \
    -d '{"title":"Compose smoke incident","description":"Created by smoke test.","severity":3}' \
    "$WEB_URL/api/incidents"
})"

incident_id="$(jq -r '.id' <<<"$created_json")"
if [[ -z "$incident_id" || "$incident_id" == "null" ]]; then
  echo "Incident creation did not return an id."
  exit 1
fi

echo "Updating incident status..."
curl -fsS \
  -X PATCH \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d '{"status":2}' \
  "$WEB_URL/api/incidents/$incident_id/status" \
  >/dev/null

echo "Loading incident audit timeline..."
audit_count="$({
  curl -fsS \
    -H "Authorization: Bearer $token" \
    "$WEB_URL/api/incidents/$incident_id/audit"
} | jq 'length')"

if [[ "$audit_count" -lt 2 ]]; then
  echo "Expected at least 2 audit entries, got $audit_count."
  exit 1
fi

echo "Checking SignalR negotiate through Nginx..."
negotiate_json="$({
  curl -fsS \
    -X POST \
    -H "Authorization: Bearer $token" \
    "$WEB_URL/hubs/operations/negotiate?negotiateVersion=1"
})"
negotiate_version="$(jq -r '.negotiateVersion' <<<"$negotiate_json")"

if [[ "$negotiate_version" != "1" ]]; then
  echo "Unexpected SignalR negotiate response."
  echo "$negotiate_json"
  exit 1
fi

echo "Checking SPA deep-link fallback..."
deep_link_html="$(curl -fsS "$WEB_URL/incidents/$incident_id")"
if ! grep -qi '<!doctype html>' <<<"$deep_link_html"; then
  echo "Incident deep-link route did not return the SPA shell."
  exit 1
fi

echo "Smoke test passed."
