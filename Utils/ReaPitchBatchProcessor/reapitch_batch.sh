#!/usr/bin/env bash
#
# reapitch_batch.sh
#
# Batch-adjusts the ReaPitch (Cockos) pitch/formant shift on every track of every
# .RPP file found under a given path (recursively).
#
# For each track:
#   * If the track has no ReaPitch plugin  -> nothing is changed.
#   * If the track has ReaPitch            -> the pitch shift is offset by <value>
#                                             semitones.
#   * If, in addition, the formant value is NON-ZERO and equal to the NEGATIVE of
#     the current pitch shift (i.e. formant mirrors pitch), the formant is set to
#     the NEGATIVE of the NEW pitch value so it keeps mirroring the pitch.
#
# The original .RPP file is never modified. For each input file a NEW file is
# written next to it with a "ReaPitchBP_" prefix, e.g.
#   Song.RPP  ->  ReaPitchBP_Song.RPP
#
# ReaPitch stores each parameter as a normalized float in [0, 1] where
#   normalized = semitones / 36 + 0.5     (range [-18, +18] semitones)
# The parameters live in the middle base64 line of the VST state chunk:
#   pitch  -> byte offset 48
#   formant-> byte offset 64
#
# With --pitch-midi, every MIDI item on any track is ALSO transposed by <value>
# semitones (note-on/off and poly-aftertouch events; notes clamp to 0..127).
#
# With --add-reapitch <track...>, only the NAMED tracks that have no ReaPitch get
# one added, with the pitch set to <value> semitones and the formant left at 0.
#
# Usage:
#   ./reapitch_batch.sh -v 5   /path/to/projects
#   ./reapitch_batch.sh --value -3 --dry-run /path/to/projects
#   ./reapitch_batch.sh -v 5 --add-reapitch Vocals Guitar /path/to/projects
#
# Options:
#   -v, --value <n>   Semitone offset to apply, range [-24, 24]. (required)
#   -n, --dry-run     Show what would change without writing any files.
#       --verbose     Print detailed per-track diagnostics instead of the
#                     default one-line-per-track table.
#       --pitch-midi  Also transpose MIDI notes on every track by <value> st.
#       --add-reapitch <track...>
#                     Add ReaPitch (pitch=<value>, formant=0) to the NAMED tracks
#                     that don't already have it.
#   -h, --help        Show this help.
#
set -euo pipefail

# --------------------------------------------------------------------------- #
# Argument parsing
# --------------------------------------------------------------------------- #
VALUE=""
TARGET_PATH=""
DRY_RUN=0
VERBOSE=0
PITCH_MIDI=0
ADD_REAPITCH=0
ADD_TRACKS=()
PREFIX="ReaPitchBP_"

print_help() {
    awk 'NR>1 && /^#/ {sub(/^# ?/, ""); print; next} NR>1 {exit}' "$0"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -v|--value)
            [[ $# -ge 2 ]] || { echo "Error: $1 requires an argument." >&2; exit 2; }
            VALUE="$2"; shift 2 ;;
        -n|--dry-run)
            DRY_RUN=1; shift ;;
        --verbose)
            VERBOSE=1; shift ;;
        --pitch-midi)
            PITCH_MIDI=1; shift ;;
        --add-reapitch)
            ADD_REAPITCH=1; shift
            # Collect the following bare words as track names (stop at next option).
            while [[ $# -gt 0 && "$1" != -* ]]; do
                ADD_TRACKS+=("$1"); shift
            done ;;
        -h|--help)
            print_help; exit 0 ;;
        -*)
            echo "Error: unknown option '$1'." >&2; exit 2 ;;
        *)
            if [[ -z "$TARGET_PATH" ]]; then
                TARGET_PATH="$1"
            else
                echo "Error: unexpected extra argument '$1'." >&2; exit 2
            fi
            shift ;;
    esac
done

# If the path was given AFTER the --add-reapitch names (no option in between),
# the last collected name is actually the path; move it back.
if [[ -z "$TARGET_PATH" && ${#ADD_TRACKS[@]} -gt 0 ]]; then
    last_idx=$(( ${#ADD_TRACKS[@]} - 1 ))
    TARGET_PATH="${ADD_TRACKS[$last_idx]}"
    unset "ADD_TRACKS[$last_idx]"
fi

# --------------------------------------------------------------------------- #
# Validation
# --------------------------------------------------------------------------- #
if [[ -z "$VALUE" ]]; then
    echo "Error: a value is required (-v/--value)." >&2
    exit 2
fi
if [[ -z "$TARGET_PATH" ]]; then
    echo "Error: a path is required." >&2
    exit 2
fi
if [[ ! -e "$TARGET_PATH" ]]; then
    echo "Error: path '$TARGET_PATH' does not exist." >&2
    exit 2
fi
if [[ $ADD_REAPITCH -eq 1 && ${#ADD_TRACKS[@]} -eq 0 ]]; then
    echo "Error: --add-reapitch requires at least one track name." >&2
    exit 2
fi

# Numeric + range check for VALUE.
if ! awk -v v="$VALUE" 'BEGIN{ if (v+0 != v && v !~ /^[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)$/) exit 1; exit (v ~ /^[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)$/ ? 0 : 1) }'; then
    echo "Error: value '$VALUE' is not a number." >&2
    exit 2
fi
if ! awk -v v="$VALUE" 'BEGIN{ exit !(v+0 >= -24 && v+0 <= 24) }'; then
    echo "Error: value '$VALUE' is out of range [-24, 24]." >&2
    exit 2
fi

if ! command -v python3 >/dev/null 2>&1; then
    echo "Error: python3 is required but was not found on PATH." >&2
    exit 2
fi

# --------------------------------------------------------------------------- #
# Collect .RPP files (recursively, case-insensitive extension)
# NUL-delimited to survive spaces/newlines in paths; bash 3.2 compatible.
# --------------------------------------------------------------------------- #
RPP_FILES=()
while IFS= read -r -d '' f; do
    RPP_FILES+=("$f")
done < <(find "$TARGET_PATH" -type f -iname '*.rpp' -print0 | sort -z)

echo "==================================================================="
echo "ReaPitch batch processor"
echo "  Value (semitones) : $VALUE"
echo "  Search path       : $TARGET_PATH"
echo "  Dry run           : $([[ $DRY_RUN -eq 1 ]] && echo yes || echo no)"
echo "  Verbose           : $([[ $VERBOSE -eq 1 ]] && echo yes || echo no)"
echo "  Pitch MIDI        : $([[ $PITCH_MIDI -eq 1 ]] && echo yes || echo no)"
if [[ $ADD_REAPITCH -eq 1 ]]; then
    echo "  Add ReaPitch      : yes -> ${ADD_TRACKS[*]}"
else
    echo "  Add ReaPitch      : no"
fi
echo "  Output prefix     : $PREFIX"
echo "  .RPP files found  : ${#RPP_FILES[@]}"
echo "==================================================================="

if [[ ${#RPP_FILES[@]} -eq 0 ]]; then
    echo "Nothing to do."
    exit 0
fi

# --------------------------------------------------------------------------- #
# Per-file processing (heavy lifting done in Python for base64/float math)
# --------------------------------------------------------------------------- #
for file in "${RPP_FILES[@]}"; do
    echo
    echo ">>> File: $file"

    # Skip files we generated ourselves so re-runs don't compound the prefix.
    base="$(basename "$file")"
    if [[ "$base" == "$PREFIX"* ]]; then
        echo "    Skipped: already a '$PREFIX' output file."
        continue
    fi

    python3 - "$VALUE" "$file" "$DRY_RUN" "$PREFIX" "$VERBOSE" "$PITCH_MIDI" "$ADD_REAPITCH" ${ADD_TRACKS[@]+"${ADD_TRACKS[@]}"} <<'PY'
import base64
import os
import re
import struct
import sys
import uuid

value   = float(sys.argv[1])
path    = sys.argv[2]
dry_run = sys.argv[3] == "1"
prefix  = sys.argv[4]
verbose = sys.argv[5] == "1"
pitch_midi = sys.argv[6] == "1"
add_reapitch = sys.argv[7] == "1"
add_tracks = sys.argv[8:]
add_set = set(add_tracks)

# ReaPitch state-chunk layout (middle base64 line of the VST block).
STATE_LEN   = 76      # decoded length of the standard ReaPitch state chunk
PITCH_OFF   = 48      # byte offset of the pitch-shift float
FORMANT_OFF = 64      # byte offset of the formant-shift float
SEMI_RANGE  = 36.0    # full semitone span of the parameter ([-18, +18])
STEP        = 1.0 / SEMI_RANGE   # normalized units per semitone
EPS         = 1e-4

REAPITCH_TAG = '<VST "VST: ReaPitch (Cockos)"'


def norm_to_semi(n):
    return (n - 0.5) * SEMI_RANGE


def clamp01(n):
    return 0.0 if n < 0.0 else (1.0 if n > 1.0 else n)


def read_float(buf, off):
    return struct.unpack_from("<f", buf, off)[0]


def write_float(buf, off, val):
    struct.pack_into("<f", buf, off, val)


def is_state_line(b64):
    """A base64 line is the ReaPitch parameter state chunk when it decodes to the
    expected length and carries the ReaPitch state signature (ff ff ff ff @ 4)."""
    try:
        d = base64.b64decode(b64, validate=True)
    except Exception:
        return None
    if len(d) < FORMANT_OFF + 4:
        return None
    if d[4:8] != b"\xff\xff\xff\xff":
        return None
    if len(d) != STATE_LEN:
        # Not the layout we know how to edit safely.
        return None
    return d


def parse_track_name(stripped):
    rest = stripped[len("NAME"):].strip()
    if not rest:
        return "(unnamed)"
    q = rest[0]
    if q in "\"'`":
        end = rest.find(q, 1)
        if end != -1:
            return rest[1:end]
    return rest.split()[0]


# MIDI events inside <SOURCE MIDI> look like:  E <ticks> <status> <note> <vel>
# (ticks decimal, the three bytes hex). Transpose note-on/off + poly-aftertouch.
MIDI_NOTE_RE = re.compile(
    r"^([Ee]) (\d+) ([0-9A-Fa-f]{1,2}) ([0-9A-Fa-f]{1,2}) ([0-9A-Fa-f]{1,2})$")


def is_note_event(stripped):
    m = MIDI_NOTE_RE.match(stripped)
    if m and (int(m.group(3), 16) & 0xF0) in (0x80, 0x90, 0xA0):
        return m
    return None


def midi_shift_line(line, semis):
    """Transpose the note byte of a MIDI note event. Returns (line, changed, clamped)."""
    m = is_note_event(line.strip())
    if m is None:
        return line, False, False
    tag, delta, b1, b2, b3 = m.groups()
    note = int(b2, 16)
    target = note + semis
    new = 0 if target < 0 else (127 if target > 127 else target)
    clamped = new != target
    if new == note:
        return line, False, clamped
    indent = line[: len(line) - len(line.lstrip())]
    nl = "\r\n" if line.endswith("\r\n") else "\n"
    return f"{indent}{tag} {delta} {b1} {new:02x} {b3}{nl}", True, clamped


# Template pieces of a default ReaPitch VST block (platform-independent except
# for the plugin filename). LINE1/LINE3 and the state layout are identical on
# macOS/Windows; only the .dylib/.dll/.so name differs.
VST_LINE1 = "Y3Blcu5e7f4CAAAAAQAAAAAAAAACAAAAAAAAAAIAAAABAAAAAAAAAAIAAAAAAAAATAAAAAEAAAAAABAA"
VST_LINE3 = "AFByb2dyYW0gMQAQAAAA"
STATE_TEMPLATE = "AAAAAP////8BAAAALAAAAAIAAAAAAAAAAACAPwAAAAAAAAAAAACAPwAAAD8AAAA/AAAAPwAAAD8AAAA/AAAAPwAAAD8AAIA/AAAAPw=="
REAPITCH_ID = "1919250531<56535472657063726561706974636800>"


def reapitch_dll_for(header_line):
    """Pick the ReaPitch plugin filename for the project's platform."""
    h = header_line.lower()
    if "win" in h:
        return "reapitch.dll"
    if "osx" in h or "macos" in h or "darwin" in h:
        return "reapitch.vst.dylib"
    return "reapitch.so"


def reapitch_state_b64(pitch_semis):
    """Encode a ReaPitch state chunk with the given pitch (semitones), formant 0."""
    buf = bytearray(base64.b64decode(STATE_TEMPLATE))
    write_float(buf, PITCH_OFF, clamp01(pitch_semis / SEMI_RANGE + 0.5))
    # formant stays at the template's 0.5 (== 0 semitones)
    return base64.b64encode(bytes(buf)).decode("ascii")


def build_vst_block(pitch_semis, dll, ci, nl):
    """Lines for a single ReaPitch FX (indented at child-indent `ci`)."""
    bi = ci + "  "
    fxid = str(uuid.uuid4()).upper()
    state = reapitch_state_b64(pitch_semis)
    parts = [
        f"{ci}BYPASS 0 0 0",
        f'{ci}<VST "VST: ReaPitch (Cockos)" {dll} 0 "" {REAPITCH_ID} ""',
        f"{bi}{VST_LINE1}",
        f"{bi}{state}",
        f"{bi}{VST_LINE3}",
        f"{ci}>",
        f"{ci}FLOATPOS 0 0 0 0",
        f"{ci}FXID {{{fxid}}}",
        f"{ci}WAK 0 0",
    ]
    return [p + nl for p in parts]


def build_fxchain_block(pitch_semis, dll, fc, nl):
    """A full <FXCHAIN> block containing one ReaPitch, at FXCHAIN-indent `fc`."""
    ci = fc + "  "
    inner = build_vst_block(pitch_semis, dll, ci, nl)
    return ([f"{fc}<FXCHAIN{nl}",
             f"{ci}SHOW 0{nl}",
             f"{ci}LASTSEL 0{nl}",
             f"{ci}DOCKED 0{nl}"]
            + inner
            + [f"{fc}>{nl}"])


with open(path, "r", newline="") as fh:
    lines = fh.readlines()

# ---- Pass 1: locate tracks and their ReaPitch state lines. ------------------
tracks = []            # list of dicts: {name, state_line_idx, indent}
depth = 0
current = None          # current track being scanned
in_reapitch = False
reapitch_b64 = []       # (line_idx, indent, b64text)
in_midi = False
midi_depth = 0
project_dll = None      # ReaPitch filename found in this project (if any)

for idx, raw in enumerate(lines):
    stripped = raw.strip()
    indent = raw[: len(raw) - len(raw.lstrip())]

    if stripped.startswith("<TRACK"):
        if current is not None:
            tracks.append(current)
        current = {"name": "(unnamed)", "state_idx": None, "indent": "",
                   "midi_lines": [], "has_midi": False,
                   "track_indent": indent, "track_close_idx": None,
                   "has_fxchain": False, "fxchain_indent": None,
                   "fxchain_depth": None, "fxchain_close_idx": None}
        depth += 1
        current["tdepth"] = depth
        continue

    if stripped.startswith("<"):
        depth += 1
        if stripped.startswith(REAPITCH_TAG):
            in_reapitch = True
            reapitch_b64 = []
            if project_dll is None:
                tail = stripped.split('(Cockos)"', 1)
                if len(tail) == 2 and tail[1].split():
                    project_dll = tail[1].split()[0]
        elif stripped.startswith("<SOURCE MIDI"):
            in_midi = True
            midi_depth = depth
            if current is not None:
                current["has_midi"] = True
        elif stripped == "<FXCHAIN" and current is not None:
            current["has_fxchain"] = True
            current["fxchain_indent"] = indent
            current["fxchain_depth"] = depth
        continue

    if stripped == ">":
        depth -= 1
        if in_reapitch:
            in_reapitch = False
            # Identify the state line among collected base64 lines.
            for lidx, ind, b64 in reapitch_b64:
                if is_state_line(b64) is not None and current is not None:
                    current["state_idx"] = lidx
                    current["indent"] = ind
                    break
            reapitch_b64 = []
        if in_midi and depth < midi_depth:
            in_midi = False
        if current is not None:
            if (current["fxchain_depth"] is not None
                    and depth == current["fxchain_depth"] - 1
                    and current["fxchain_close_idx"] is None):
                current["fxchain_close_idx"] = idx
            if depth == current["tdepth"] - 1 and current["track_close_idx"] is None:
                current["track_close_idx"] = idx
        continue

    if current is not None and depth == current["tdepth"] and stripped.startswith("NAME"):
        current["name"] = parse_track_name(stripped)
        continue

    if in_midi and depth == midi_depth and current is not None:
        if is_note_event(stripped) is not None:
            current["midi_lines"].append(idx)
        continue

    if in_reapitch and stripped:
        reapitch_b64.append((idx, indent, stripped))

if current is not None:
    tracks.append(current)

if project_dll is None:
    project_dll = reapitch_dll_for(lines[0] if lines else "")

file_nl = "\r\n" if (lines and lines[0].endswith("\r\n")) else "\n"

# ---- Pass 2: apply changes and collect per-track results. -------------------
def fmt(v):
    r = round(v, 3)
    if r == 0:
        r = 0.0            # avoid a "-0" display
    return str(int(r)) if r == int(r) else f"{r:g}"


results = []
changed = False
midi_semis = int(round(value))
insertions = []   # (line_index_to_insert_before, [lines]) for added ReaPitch
for t in tracks:
    name = t["name"]
    res = {"name": name, "fx": False,
           "has_midi": t.get("has_midi", False),
           "midi_count": 0, "midi_clamped": False}

    # --- ReaPitch pitch / formant ---
    if t["state_idx"] is not None:
        lidx = t["state_idx"]
        indent = t["indent"]
        b64 = lines[lidx].strip()
        buf = bytearray(base64.b64decode(b64, validate=True))

        pitch_n = read_float(buf, PITCH_OFF)
        form_n = read_float(buf, FORMANT_OFF)
        pitch_s = norm_to_semi(pitch_n)
        form_s = norm_to_semi(form_n)

        new_pitch_n = pitch_n + value * STEP
        new_pitch_c = clamp01(new_pitch_n)
        pitch_clamped = new_pitch_c != new_pitch_n
        write_float(buf, PITCH_OFF, new_pitch_c)
        new_pitch_s = norm_to_semi(new_pitch_c)

        formant_nonzero = abs(form_n - 0.5) > EPS
        formant_mirrors = abs(form_n - (1.0 - pitch_n)) < EPS
        formant_changed = formant_nonzero and formant_mirrors
        new_form_s = form_s
        if formant_changed:
            new_form_c = 1.0 - new_pitch_c   # normalized form of -(new pitch)
            write_float(buf, FORMANT_OFF, new_form_c)
            new_form_s = norm_to_semi(new_form_c)

        newline = "\r\n" if lines[lidx].endswith("\r\n") else "\n"
        lines[lidx] = indent + base64.b64encode(bytes(buf)).decode("ascii") + newline
        changed = True

        res.update({
            "fx": True,
            "pitch_old": pitch_s, "pitch_new": new_pitch_s, "pitch_clamped": pitch_clamped,
            "formant_changed": formant_changed,
            "formant_old": form_s, "formant_new": new_form_s,
        })

    elif add_reapitch and name in add_set:
        # Named track has no ReaPitch -> add one (pitch = value, formant = 0).
        pn = value / SEMI_RANGE + 0.5
        applied = norm_to_semi(clamp01(pn))
        if t.get("has_fxchain") and t.get("fxchain_close_idx") is not None:
            block = build_vst_block(value, project_dll,
                                    (t["fxchain_indent"] or "") + "  ", file_nl)
            insertions.append((t["fxchain_close_idx"], block))
        elif t.get("track_close_idx") is not None:
            block = build_fxchain_block(value, project_dll,
                                        (t["track_indent"] or "") + "  ", file_nl)
            insertions.append((t["track_close_idx"], block))
        else:
            block = None
        if block is not None:
            changed = True
            res.update({
                "fx": True, "added": True,
                "pitch_old": 0.0, "pitch_new": applied,
                "pitch_clamped": pn < 0.0 or pn > 1.0,
                "formant_changed": False, "formant_old": 0.0, "formant_new": 0.0,
            })

    # --- MIDI transpose (only with --pitch-midi and a non-zero whole-step) ---
    if pitch_midi and midi_semis != 0:
        cnt = 0
        clamped = False
        for mi in t.get("midi_lines", []):
            new_line, ch, cl = midi_shift_line(lines[mi], midi_semis)
            if ch:
                lines[mi] = new_line
                cnt += 1
                changed = True
            if cl:
                clamped = True
        res["midi_count"] = cnt
        res["midi_clamped"] = clamped

    results.append(res)

# Apply queued FX insertions bottom-up so earlier line indices stay valid.
for at, block in sorted(insertions, key=lambda x: x[0], reverse=True):
    lines[at:at] = block

# ---- Reporting. -------------------------------------------------------------
if verbose:
    for r in results:
        if r.get("added"):
            extra = " (clamped to +/-18 st)" if r["pitch_clamped"] else ""
            print(f"    Track '{r['name']}': ReaPitch FX ADDED "
                  f"(pitch {r['pitch_new']:+.3f} st, formant 0){extra}.")
        elif r["fx"]:
            print(f"    Track '{r['name']}': ReaPitch FX present.")
            print(f"        Current pitch  : {r['pitch_old']:+.3f} st")
            print(f"        Current formant: {r['formant_old']:+.3f} st")
            if r["pitch_clamped"]:
                print("        ! Pitch clamped to [-18, 18] st.")
            print(f"        -> Pitch   {r['pitch_old']:+.3f} -> {r['pitch_new']:+.3f} st "
                  f"(applied {value:+g})")
            if r["formant_changed"]:
                print(f"        -> Formant {r['formant_old']:+.3f} -> {r['formant_new']:+.3f} st "
                      f"(set to negative of new pitch)")
            else:
                print("        -> Formant unchanged.")
        else:
            print(f"    Track '{r['name']}': ReaPitch FX not present.")
        if pitch_midi:
            if r["has_midi"]:
                extra = " (some clamped to 0..127)" if r["midi_clamped"] else ""
                print(f"        -> MIDI transposed {r['midi_count']} note(s) "
                      f"by {midi_semis:+d} st{extra}.")
            else:
                print("        -> No MIDI items.")
else:
    ARROW = "\u2192"   # →
    headers = ["Track", f"Pitch (old {ARROW} new)", f"Formant (old {ARROW} new)"]
    if pitch_midi:
        headers.append("MIDI (notes)")

    rows = []
    for r in results:
        if r.get("added"):
            pcol = f"add {fmt(r['pitch_new'])}"
        elif r["fx"]:
            pcol = f"{fmt(r['pitch_old'])} {ARROW} {fmt(r['pitch_new'])}"
        else:
            pcol = "/"
        fcol = (f"{fmt(r['formant_old'])} {ARROW} {fmt(r['formant_new'])}"
                if r.get("formant_changed") else "/")
        cells = [r["name"], pcol, fcol]
        if pitch_midi:
            cells.append(str(r["midi_count"]) if r["has_midi"] else "/")
        rows.append(cells)

    # TrackName column fits its content; the value columns share one width.
    name_w = max([len(headers[0])] + [len(c[0]) for c in rows])
    value_cells = headers[1:] + [c for cs in rows for c in cs[1:]]
    val_w = max(len(x) for x in value_cells)

    def render(cells):
        out = f"| {cells[0]:<{name_w}} |"
        for c in cells[1:]:
            out += f" {c:<{val_w}} |"
        return out

    print(render(headers))
    print("| " + " | ".join(["-" * name_w] + ["-" * val_w for _ in headers[1:]]) + " |")
    for cells in rows:
        print(render(cells))

out_path = os.path.join(os.path.dirname(path), prefix + os.path.basename(path))

if add_reapitch:
    existing = {t["name"] for t in tracks}
    missing = [n for n in add_tracks if n not in existing]
    if missing:
        print(f"    ! --add-reapitch: no track named: {', '.join(missing)}")

if not changed:
    print("No changes were made - no output file written.")
elif dry_run:
    print(f"(dry-run) would write: {out_path}")
else:
    with open(out_path, "w", newline="") as fh:
        fh.writelines(lines)
    print(f"Output written: {out_path}")
PY
done

echo
echo "Done."
