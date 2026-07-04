#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./scripts/remove-bom.sh [--check] [file-or-directory ...]

Options:
  --check   Report files with UTF-8 BOM and exit 1 if any are found.

Behavior:
  - With paths: scans only provided files/directories recursively.
  - Without paths: scans tracked files from git.
EOF
}

mode="fix"

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
fi

if [[ "${1:-}" == "--check" ]]; then
  mode="check"
  shift
fi

is_text_like() {
  local path="$1"
  local mime

  mime="$(file -b --mime-type "$path" 2>/dev/null || true)"

  case "$mime" in
    text/*|application/json|application/xml|application/javascript|application/x-sh)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

has_utf8_bom() {
  local path="$1"
  local prefix

  prefix="$(xxd -p -l 3 "$path" 2>/dev/null || true)"
  [[ "$prefix" == "efbbbf" ]]
}

collect_candidates() {
  if [[ "$#" -gt 0 ]]; then
    find "$@" -type f
  else
    git ls-files
  fi
}

found=0
fixed=0

while IFS= read -r path; do
  [[ -f "$path" ]] || continue

  if ! is_text_like "$path"; then
    continue
  fi

  if has_utf8_bom "$path"; then
    ((found += 1))

    if [[ "$mode" == "check" ]]; then
      echo "BOM found: $path"
    else
      perl -i -pe 's/^\x{FEFF}//' "$path"
      echo "BOM removed: $path"
      ((fixed += 1))
    fi
  fi
done < <(collect_candidates "$@")

if [[ "$mode" == "check" ]]; then
  if [[ "$found" -gt 0 ]]; then
    echo "Found $found file(s) with UTF-8 BOM."
    exit 1
  fi

  echo "No UTF-8 BOM detected."
  exit 0
fi

echo "Done. Removed BOM from $fixed file(s)."
