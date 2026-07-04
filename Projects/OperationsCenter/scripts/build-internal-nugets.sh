#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOLUTION_DIR="$REPO_ROOT/apps/api/operations-center"
SOLUTION_FILE="$SOLUTION_DIR/OperationsCenter.slnx"
OUTPUT_DIR="$SOLUTION_DIR/artifacts/packages"
CONFIGURATION="${1:-Release}"

PACKAGE_PROJECTS=(
  "$SOLUTION_DIR/src/BuildingBlocks/BuildingBlocks.Cqrs.Abstractions/BuildingBlocks.Cqrs.Abstractions.csproj"
  "$SOLUTION_DIR/src/BuildingBlocks/BuildingBlocks.Cqrs/BuildingBlocks.Cqrs.csproj"
)

if [[ ! -f "$SOLUTION_FILE" ]]; then
  echo "Could not find solution: $SOLUTION_FILE"
  exit 1
fi

for project in "${PACKAGE_PROJECTS[@]}"; do
  if [[ ! -f "$project" ]]; then
    echo "Could not find packable project: $project"
    exit 1
  fi
done

mkdir -p "$OUTPUT_DIR"

cd "$SOLUTION_DIR"

echo "Restoring packages..."
dotnet restore "$SOLUTION_FILE"

for project in "${PACKAGE_PROJECTS[@]}"; do
  echo "Packing $(basename "$project" .csproj)..."
  dotnet pack "$project" \
    --configuration "$CONFIGURATION" \
    --no-restore \
    --output "$OUTPUT_DIR"
done

echo "Packages written to $OUTPUT_DIR"
