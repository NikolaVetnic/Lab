#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_SCRIPT="$REPO_ROOT/scripts/start-api.sh"
WEB_DIR="$REPO_ROOT/apps/web"
API_HEALTH_URL="${OC_API_HEALTH_URL:-http://localhost:5000/health}"

if [[ ! -x "$API_SCRIPT" ]]; then
  echo "API script is missing or not executable: $API_SCRIPT"
  exit 1
fi

if [[ ! -f "$WEB_DIR/package.json" ]]; then
  echo "Could not find frontend package.json at: $WEB_DIR/package.json"
  exit 1
fi

cleanup() {
  if [[ -n "${API_PID:-}" ]] && kill -0 "$API_PID" 2>/dev/null; then
    echo "Stopping API process ($API_PID)..."
    kill "$API_PID" 2>/dev/null || true
    wait "$API_PID" 2>/dev/null || true
  fi
}

trap cleanup EXIT INT TERM

echo "Starting API..."
cd "$REPO_ROOT"
"$API_SCRIPT" &
API_PID=$!

echo "Waiting for API readiness at $API_HEALTH_URL..."
for _ in {1..90}; do
  if curl -fsS "$API_HEALTH_URL" >/dev/null 2>&1; then
    echo "API is ready."
    break
  fi

  if ! kill -0 "$API_PID" 2>/dev/null; then
    echo "API process exited before becoming ready."
    exit 1
  fi

  sleep 1
done

if ! curl -fsS "$API_HEALTH_URL" >/dev/null 2>&1; then
  echo "API did not become ready in time."
  exit 1
fi

echo "Starting frontend..."
cd "$WEB_DIR"
npm run dev
