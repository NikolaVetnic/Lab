#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOLUTION_DIR="$REPO_ROOT/apps/api/operations-center"
SOLUTION_FILE="$SOLUTION_DIR/OperationsCenter.slnx"

if [[ ! -f "$SOLUTION_FILE" ]]; then
  echo "Could not find solution: $SOLUTION_FILE"
  exit 1
fi

cd "$SOLUTION_DIR"

echo "Restoring packages..."
dotnet restore "$SOLUTION_FILE"

echo "Building solution..."
dotnet build "$SOLUTION_FILE" \
  --configuration Release \
  --no-restore

echo "Running tests..."
dotnet test "$SOLUTION_FILE" \
  --configuration Release \
  --no-build \
  --logger "console;verbosity=normal"

echo "All tests passed."
