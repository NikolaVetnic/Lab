#!/usr/bin/env bash
set -euo pipefail

# Development-only helper.
# Ensures local PostgreSQL is running, waits for readiness,
# then runs the API in explicit seed mode.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_ROOT="$REPO_ROOT/apps/api/operations-center"
SEED_ARG="--seed"

if [[ "${1:-}" == "--demo" ]]; then
  SEED_ARG="--seed=demo"
fi

cd "$REPO_ROOT"

echo "Ensuring local PostgreSQL service is running (operations-center-postgres)..."
docker compose up -d operations-center-postgres

echo "Waiting for PostgreSQL readiness..."
until docker compose exec -T operations-center-postgres \
  pg_isready -U operations_center -d operations_center; do
  echo "Waiting for PostgreSQL..."
  sleep 1
done

echo "PostgreSQL is ready."
echo "Seeding development data using '$SEED_ARG'..."

cd "$API_ROOT"

ASPNETCORE_ENVIRONMENT=Development \
dotnet run \
  --project src/OperationsCenter/OperationsCenter.Api \
  -- "$SEED_ARG"

echo "Development seed completed."
