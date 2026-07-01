#!/usr/bin/env bash

# Local-development script only. Do not use automatic migration execution in production.
set -euo pipefail

docker compose up -d operations-center-postgres

until docker compose exec -T operations-center-postgres \
  pg_isready -U operations_center -d operations_center; do
  echo "Waiting for PostgreSQL..."
  sleep 1
done

cd apps/api/operations-center

dotnet ef database update \
  --project src/OperationsCenter.Infrastructure \
  --startup-project src/OperationsCenter.Api

dotnet run --project src/OperationsCenter.Api
