#!/usr/bin/env bash
set -euo pipefail

docker compose up -d operations-center-postgres

until docker compose exec -T operations-center-postgres pg_isready -U operations_center -d operations_center; do
  echo "Waiting for PostgreSQL..."
  sleep 1
done

cd apps/api/operations-center
dotnet run --project src/OperationsCenter.Api
