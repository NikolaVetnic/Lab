#!/usr/bin/env bash
#
# reastem_export.sh
#
# Batch-exports the ROOT (top-level) stems of every REAPER project found under a
# given path, rendering the current TIME SELECTION of each project to WAV.
#
# A "root stem" is a top-level track of the project:
#   * a folder-parent track  -> renders the whole folder (its summed children)
#   * a standalone track      -> renders just that track
# Child tracks inside folders are NOT rendered separately.
#
# For the given PATH the script:
#   1. Finds every .RPP recursively (skipping previous Export_* folders).
#   2. Creates ONE output folder next to the path:  Export_YYYYMMDD-hhmm
#   3. For each project, renders its top-level stems into
#        Export_YYYYMMDD-hhmm/<relative-dir>/<ProjectName>/<TrackName>.wav
#      in WAV 48 kHz / 16-bit, bounded by the project's time selection.
#
# If a project has NO time selection, it is SKIPPED with a warning (never
# rendered), as requested.
#
# Rendering is performed by REAPER in command-line mode (-renderproject); the
# original .RPP files are never modified (a throwaway copy is rendered instead).
#
# Usage:
#   ./reastem_export.sh /path/to/projects
#   ./reastem_export.sh --dry-run /path/to/projects
#
# Options:
#   -n, --dry-run        Prepare and report, but do not render anything.
#   -c, --contains <s>   Only process projects whose file NAME or content
#                        contains the string <s> (case-sensitive).
#       --bits <16|24>   Bit depth (default 16).
#       --srate <hz>     Sample rate (default 48000).
#       --reaper <path>  Path to the REAPER binary (autodetected otherwise).
#   -h, --help           Show this help.
#
set -euo pipefail

# --------------------------------------------------------------------------- #
# Defaults / argument parsing
# --------------------------------------------------------------------------- #
TARGET_PATH=""
DRY_RUN=0
BITS=16
SRATE=48000
CHANNELS=2
CONTAINS=""
HAS_CONTAINS=0
REAPER_BIN="${REAPER_BIN:-}"
TMP_PREFIX=".reastem_render_"

print_help() {
    awk 'NR>1 && /^#/ {sub(/^# ?/, ""); print; next} NR>1 {exit}' "$0"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -n|--dry-run) DRY_RUN=1; shift ;;
        -c|--contains) [[ $# -ge 2 ]] || { echo "Error: $1 needs a value." >&2; exit 2; }; CONTAINS="$2"; HAS_CONTAINS=1; shift 2 ;;
        --bits)   [[ $# -ge 2 ]] || { echo "Error: $1 needs a value." >&2; exit 2; }; BITS="$2"; shift 2 ;;
        --srate)  [[ $# -ge 2 ]] || { echo "Error: $1 needs a value." >&2; exit 2; }; SRATE="$2"; shift 2 ;;
        --reaper) [[ $# -ge 2 ]] || { echo "Error: $1 needs a value." >&2; exit 2; }; REAPER_BIN="$2"; shift 2 ;;
        -h|--help) print_help; exit 0 ;;
        -*) echo "Error: unknown option '$1'." >&2; exit 2 ;;
        *)
            if [[ -z "$TARGET_PATH" ]]; then TARGET_PATH="$1"
            else echo "Error: unexpected extra argument '$1'." >&2; exit 2; fi
            shift ;;
    esac
done

# --------------------------------------------------------------------------- #
# Validation
# --------------------------------------------------------------------------- #
if [[ -z "$TARGET_PATH" ]]; then
    echo "Error: a path is required." >&2
    exit 2
fi
if [[ ! -e "$TARGET_PATH" ]]; then
    echo "Error: path '$TARGET_PATH' does not exist." >&2
    exit 2
fi
if [[ "$BITS" != "16" && "$BITS" != "24" ]]; then
    echo "Error: --bits must be 16 or 24." >&2
    exit 2
fi
if ! command -v python3 >/dev/null 2>&1; then
    echo "Error: python3 is required but was not found on PATH." >&2
    exit 2
fi

# Determine the search root (a directory) and the initial file list.
if [[ -d "$TARGET_PATH" ]]; then
    ROOT="$TARGET_PATH"
else
    ROOT="$(dirname "$TARGET_PATH")"
fi
ROOT="$(cd "$ROOT" && pwd)"   # absolute

# Locate the REAPER binary (unless dry-run).
if [[ -z "$REAPER_BIN" ]]; then
    for cand in \
        "/Applications/REAPER.app/Contents/MacOS/REAPER" \
        "/Applications/REAPER64.app/Contents/MacOS/REAPER" \
        "$HOME/Applications/REAPER.app/Contents/MacOS/REAPER"; do
        [[ -x "$cand" ]] && { REAPER_BIN="$cand"; break; }
    done
fi
if [[ $DRY_RUN -eq 0 ]]; then
    if [[ -z "$REAPER_BIN" || ! -x "$REAPER_BIN" ]]; then
        echo "Error: REAPER binary not found. Use --reaper <path> or set REAPER_BIN." >&2
        exit 2
    fi
fi

# --------------------------------------------------------------------------- #
# Collect .RPP files (recursive; skip our own Export_* output trees + temp files)
# --------------------------------------------------------------------------- #
RPP_FILES=()
while IFS= read -r -d '' f; do
    case "$f" in
        */Export_[0-9]*/*) continue ;;                 # inside a previous export
    esac
    [[ "$(basename "$f")" == "$TMP_PREFIX"* ]] && continue
    RPP_FILES+=("$f")
done < <(find "$TARGET_PATH" -type f -iname '*.rpp' -print0 | sort -z)

total_found=${#RPP_FILES[@]}

# Optional case-sensitive filter (-c/--contains): matches the file NAME or the
# file content.
if [[ $HAS_CONTAINS -eq 1 ]]; then
    FILTERED=()
    for f in "${RPP_FILES[@]}"; do
        if [[ "$(basename "$f")" == *"$CONTAINS"* ]] || grep -qF -e "$CONTAINS" -- "$f"; then
            FILTERED+=("$f")
        fi
    done
    RPP_FILES=("${FILTERED[@]+"${FILTERED[@]}"}")
fi

TS="$(date +%Y%m%d-%H%M)"
EXPORT_ROOT="$ROOT/Export_$TS"

echo "==================================================================="
echo "ReaStem exporter"
echo "  Search path   : $TARGET_PATH"
echo "  Export folder : $EXPORT_ROOT"
echo "  Format        : WAV ${SRATE} Hz / ${BITS}-bit / ${CHANNELS}ch"
echo "  Bounds        : project time selection"
echo "  Dry run       : $([[ $DRY_RUN -eq 1 ]] && echo yes || echo no)"
if [[ $HAS_CONTAINS -eq 1 ]]; then
    echo "  Contains      : '$CONTAINS' (case-sensitive; ${#RPP_FILES[@]}/${total_found} matched)"
fi
echo "  REAPER        : ${REAPER_BIN:-<dry-run>}"
echo "  .RPP found    : ${#RPP_FILES[@]}"
echo "==================================================================="

if [[ ${#RPP_FILES[@]} -eq 0 ]]; then
    echo "Nothing to do."
    exit 0
fi

[[ $DRY_RUN -eq 0 ]] && mkdir -p "$EXPORT_ROOT"

rendered_projects=0
skipped_projects=0

for file in "${RPP_FILES[@]}"; do
    echo
    echo ">>> Project: $file"

    src_dir="$(cd "$(dirname "$file")" && pwd)"
    tmp_rpp="$src_dir/${TMP_PREFIX}$$_$(basename "$file")"

    # Prepare a render-ready temp copy (in the SAME dir, so relative media paths
    # keep resolving). Python bakes render settings + selects top-level tracks,
    # and refuses (exit 3) if there is no time selection.
    set +e
    render_dir="$(python3 - "$file" "$tmp_rpp" "$ROOT" "$EXPORT_ROOT" "$DRY_RUN" "$BITS" "$SRATE" "$CHANNELS" <<'PY'
import base64, os, sys

orig, tmp, root, export_root, dry, bits, srate, channels = sys.argv[1:9]
dry = dry == "1"; bits = int(bits); srate = int(srate); channels = int(channels)

def log(*a):
    print(*a, file=sys.stderr)

with open(orig, "r", newline="") as fh:
    lines = fh.readlines()

# ---- Time-selection check (project-level SELECTION start end). --------------
sel_start = sel_end = None
depth = 0
for raw in lines:
    s = raw.strip()
    if s.startswith("<"):
        depth += 1
    elif s == ">":
        depth -= 1
    elif depth == 1 and s.startswith("SELECTION "):
        parts = s.split()
        try:
            sel_start, sel_end = float(parts[1]), float(parts[2])
        except (IndexError, ValueError):
            pass
        break

if sel_start is None or sel_end is None or abs(sel_end - sel_start) <= 1e-9:
    log("    ! No time selection -> SKIPPED (nothing rendered).")
    sys.exit(3)

lo, hi = sorted((sel_start, sel_end))   # REAPER treats the pair as min/max
log(f"    Time selection : {lo:.3f} -> {hi:.3f} s")

# ---- Per-project output directory (mirrors the tree, avoids name clashes). --
rel_dir = os.path.relpath(os.path.dirname(os.path.abspath(orig)), root)
stem = os.path.splitext(os.path.basename(orig))[0]
sub = os.path.normpath(os.path.join(export_root, rel_dir, stem)) if rel_dir != "." \
      else os.path.join(export_root, stem)

# ---- Build the render-config + settings. ------------------------------------
wav_cfg = base64.b64encode(b"evaw" + bytes([bits, 0, 0])).decode("ascii")

RENDER_TOKENS = [
    f'RENDER_FILE "{sub}"',
    "RENDER_PATTERN $track",
    f"RENDER_FMT 0 {channels} {srate}",
    "RENDER_RANGE 2 0 0 0 0",   # 2 = time selection
    "RENDER_STEMS 3",           # 3 = stems (selected tracks), no master mix
    "RENDER_NORMALIZE 0",
]
STRIP = ("RENDER_FILE", "RENDER_PATTERN", "RENDER_FMT", "RENDER_RANGE",
         "RENDER_STEMS", "RENDER_TAILFLAG", "RENDER_TAIL", "RENDER_TAILMS",
         "RENDER_NORMALIZE")

# ---- Rewrite: select top-level tracks, drop old render tokens, inject new. ---
out = []
stack = []              # block-name stack
folder_depth = 0        # REAPER folder nesting (via ISBUS)
skip_cfg = 0            # >0 while skipping an existing <RENDER_CFG ...> block
top_names = []

for i, raw in enumerate(lines):
    s = raw.strip()

    # Skip an existing RENDER_CFG block entirely.
    if skip_cfg:
        if s.startswith("<"):
            skip_cfg += 1
        elif s == ">":
            skip_cfg -= 1
        continue
    if s.startswith("<RENDER_CFG"):
        skip_cfg = 1
        continue

    if s.startswith("<"):
        name = s[1:].split()[0] if len(s) > 1 else ""
        if name == "TRACK":
            entry = {"name": "TRACK", "top": folder_depth == 0,
                     "isbus": (0, 0), "sel_done": False}
            stack.append(entry)
            out.append(raw)
            indent = raw[: len(raw) - len(raw.lstrip())]
            out.append(f"{indent}  SEL {1 if entry['top'] else 0}\n")
            if entry["top"]:
                # capture the track name for logging (scan ahead a little)
                nm = "(unnamed)"
                for la in lines[i + 1:i + 40]:
                    ls = la.strip()
                    if ls.startswith("NAME "):
                        rest = ls[5:].strip()
                        if rest[:1] in ("\"", "'", chr(96)):
                            q = rest[0]; e = rest.find(q, 1)
                            nm = rest[1:e] if e != -1 else rest.strip(q)
                        else:
                            nm = rest.split()[0] if rest else "(unnamed)"
                        break
                    if ls.startswith("<TRACK") or ls == ">":
                        break
                top_names.append(nm)
            continue
        stack.append({"name": name})
        out.append(raw)
        continue

    if s == ">":
        entry = stack.pop() if stack else {}
        if entry.get("name") == "TRACK":
            a, b = entry["isbus"]
            if a == 1:
                folder_depth += 1
            elif a == 2:
                folder_depth += b
        out.append(raw)
        continue

    top = stack[-1] if stack else None
    inside_track = top is not None and top.get("name") == "TRACK"

    if inside_track and s.startswith("SEL"):
        continue                                   # already injected our SEL
    if inside_track and s.startswith("ISBUS"):
        p = s.split()
        try:
            top["isbus"] = (int(p[1]), int(p[2]))
        except (IndexError, ValueError):
            pass
        out.append(raw)
        continue

    # Drop project-level render tokens we are going to redefine.
    if len(stack) == 1 and any(s.startswith(t + " ") or s == t for t in STRIP):
        continue

    out.append(raw)

# Inject our render tokens right after the <REAPER_PROJECT ...> opening line.
inject = "".join(f"  {t}\n" for t in RENDER_TOKENS)
inject += f"  <RENDER_CFG\n    {wav_cfg}\n  >\n"
if out:
    out.insert(1, inject)

log(f"    Top-level stems: {len(top_names)} -> " + ", ".join(top_names))

if not dry:
    os.makedirs(sub, exist_ok=True)
    with open(tmp, "w", newline="") as fh:
        fh.writelines(out)

print(sub)     # stdout: the render output directory
PY
)"
    prep_rc=$?
    set -e

    if [[ $prep_rc -eq 3 ]]; then
        skipped_projects=$((skipped_projects + 1))
        continue
    elif [[ $prep_rc -ne 0 ]]; then
        echo "    ! Failed to prepare project (python exit $prep_rc) - skipped." >&2
        skipped_projects=$((skipped_projects + 1))
        continue
    fi

    if [[ $DRY_RUN -eq 1 ]]; then
        echo "    (dry-run) would render stems into: $render_dir"
        rendered_projects=$((rendered_projects + 1))
        continue
    fi

    echo "    Rendering into: $render_dir"
    "$REAPER_BIN" -renderproject "$tmp_rpp" >/dev/null 2>&1 || true
    rm -f "$tmp_rpp"

    # Report produced files.
    count=0
    while IFS= read -r -d '' w; do
        echo "        + $(basename "$w")"
        count=$((count + 1))
    done < <(find "$render_dir" -maxdepth 1 -type f -iname '*.wav' -print0 2>/dev/null | sort -z)
    if [[ $count -eq 0 ]]; then
        echo "    ! No stems were produced (check the project)." >&2
        skipped_projects=$((skipped_projects + 1))
    else
        echo "    Rendered $count stem(s)."
        rendered_projects=$((rendered_projects + 1))
    fi
done

echo
echo "-------------------------------------------------------------------"
echo "Done. Projects rendered: $rendered_projects, skipped: $skipped_projects"
if [[ $DRY_RUN -eq 0 ]]; then
    echo "Output: $EXPORT_ROOT"
fi
