#!/usr/bin/env bash
#
# scripts/create-soak-test-report.sh — scaffold a per-session soak-test report.
#
# Run this once at the start of each two-headset soak test (see
# docs/soak-test-checklist.md). Produces a local-only directory under
# quest-logs/soak-tests/ with:
#
#   - BUILD-INFO.json     — snapshot of the build under test
#   - NOTES.md            — empty template with the pass-criteria checklist
#                           and per-block observation prompts pre-filled
#
# After the soak, drop the renamed `johan-*.zip` and `friend-*.zip` log
# archives (from scripts/pull-quest-logs.sh) into the same folder. The
# quest-logs/ tree is gitignored — these archives stay local.
#
# Usage:
#   ./scripts/create-soak-test-report.sh           # default name
#   ./scripts/create-soak-test-report.sh --label release-candidate
#   ./scripts/create-soak-test-report.sh --help
#
# Exit codes:
#   0 — success
#   1 — bad CLI args
#   2 — BUILD-INFO.json not found (no build has been done yet)
#   3 — output directory already exists (rerun the script — uses minute-precision
#       so wait a minute or use --label to disambiguate)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_INFO="${REPO_ROOT}/UnityProject/Assets/Resources/build-info.json"
REPORTS_DIR="${REPO_ROOT}/quest-logs/soak-tests"

LABEL=""

usage() {
    cat <<EOF
Usage: $(basename "$0") [--label LABEL] [--help]

Scaffolds a soak-test report directory under quest-logs/soak-tests/.

Options:
  --label TEXT    Append "-<TEXT>" to the directory name. Use it to
                  disambiguate same-minute runs (e.g. "before-fix" /
                  "after-fix"), or to tag the session purpose.
  -h, --help      Show this message.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --label)
            if [[ -z "${2:-}" ]]; then
                echo "--label requires a value." >&2
                exit 1
            fi
            # Sanitise: lowercase, replace non-alphanum with hyphens.
            LABEL="$(echo "$2" | tr '[:upper:]' '[:lower:]' | tr -c 'a-z0-9' '-' | sed 's/^-*//;s/-*$//')"
            shift 2
            ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown arg: $1" >&2; usage >&2; exit 1 ;;
    esac
done

if [[ ! -f "$BUILD_INFO" ]]; then
    echo "BUILD-INFO.json not found at $BUILD_INFO" >&2
    echo "Run ./scripts/build-quest.sh first to generate it (or test against an existing build)." >&2
    exit 2
fi

STAMP="$(date +%Y%m%d-%H%M)"
DIR_NAME="$STAMP"
[[ -n "$LABEL" ]] && DIR_NAME="${STAMP}-${LABEL}"

OUT_DIR="${REPORTS_DIR}/${DIR_NAME}"

if [[ -d "$OUT_DIR" ]]; then
    echo "Output directory already exists: $OUT_DIR" >&2
    echo "Wait a minute or pass --label TEXT to disambiguate." >&2
    exit 3
fi

mkdir -p "$OUT_DIR"
cp "$BUILD_INFO" "$OUT_DIR/BUILD-INFO.json"

# Pull a few fields out of BUILD-INFO for the NOTES.md header. Tiny sed-based
# reader matches the convention from scripts/package-friend-test.sh — no jq
# dependency.
json_str() { sed -nE "s/^[[:space:]]*\"$1\"[[:space:]]*:[[:space:]]*\"([^\"]*)\".*/\1/p" "$BUILD_INFO" | head -1; }
json_int() { sed -nE "s/^[[:space:]]*\"$1\"[[:space:]]*:[[:space:]]*([0-9]+).*/\1/p" "$BUILD_INFO" | head -1; }
json_bool() { sed -nE "s/^[[:space:]]*\"$1\"[[:space:]]*:[[:space:]]*(true|false).*/\1/p" "$BUILD_INFO" | head -1; }

VERSION="$(json_str version)"
VERSION_CODE="$(json_int versionCode)"
APK_NAME="$(json_str apkName)"
GIT_COMMIT="$(json_str gitCommit)"
GIT_BRANCH="$(json_str gitBranch)"
BUILD_TIME="$(json_str buildTime)"
DIRTY="$(json_bool gitDirty)"
DIRTY_NOTE=""
[[ "$DIRTY" == "true" ]] && DIRTY_NOTE=" (uncommitted changes)"

cat > "$OUT_DIR/NOTES.md" <<EOF
# Soak test — $(date +%Y-%m-%d) $(date +%H:%M)

**Build under test**
- Version: ${VERSION} (code ${VERSION_CODE})
- APK: ${APK_NAME}
- Commit: ${GIT_COMMIT} on ${GIT_BRANCH}${DIRTY_NOTE}
- Built: ${BUILD_TIME}

**Testers**
- Side A:
- Side B:

**Network**
- Same Wi-Fi / different houses:
- Wi-Fi quality both sides:

---

## Pass criteria

- [ ] Zero crashes either side
- [ ] Voice survives normal usage (>= 25 / 30 connected minutes clean)
- [ ] Reconnect works (stop/rejoin + Meta-menu recover cleanly)
- [ ] No progressive degradation (minute 30 feels like minute 5)
- [ ] Logs retrievable from both headsets
- [ ] This NOTES.md filled in

Overall verdict: **PASS / WARN / FAIL** (delete two)

---

## Per-block observations

### 5-min smoke test
- [ ] Solo launches clean
- [ ] Host
- [ ] Join
- [ ] Meta menu open/close
- [ ] Stop/rejoin

Notes:

### 0:00–5:00 — Idle conversation
Hand/head sync feel, voice quality:

### 5:00–10:00 — Focus loss stress
Meta menu open × 3 each side, headset removal:

### 10:00–15:00 — Stop / rejoin cycle
Reconnect behaviour, host swap if attempted:

### 15:00–20:00 — Extended idle
Any slow regression, frame stutter, voice glitch:

### 20:00–25:00 — Stress test
Repeated stop/join × 5, room change cycle, simultaneous actions:

### 25:00–30:00 — Calm idle + observations
Battery start → end:
Thermal warnings (yes/no):
Subjective comfort:

---

## Failures / warnings

Wall-clock time + one-line description per item:

-

---

## Performance numbers (if perf overlay was enabled)

- App GPU time:
- App CPU time:
- Missed frames:
- GPU level trend:
- Memory at minute 0:
- Memory at minute 30:
- Thermal level:

---

## Log archives

Drop the renamed pulled-log zips here:

- side-A-${STAMP}.zip (johan / etc.)
- side-B-${STAMP}.zip (friend / etc.)

See [docs/remote-fika-test-debug-checklist.md](../../../docs/remote-fika-test-debug-checklist.md#quick-triage-on-receiving-both-files) for triage commands.

---

## Follow-up filed

Items raised by this session that need a backlog entry or fix:

-
EOF

REL="${OUT_DIR#"${REPO_ROOT}/"}"

echo "[report] OK · ${REL}/"
echo "[report]   build  · ${VERSION} (code ${VERSION_CODE}) · commit ${GIT_COMMIT}${DIRTY_NOTE}"
echo "[report]   files  · BUILD-INFO.json, NOTES.md"
echo "[report]"
echo "[report] Next steps:"
echo "[report]   1. Fill in testers + network at the top of NOTES.md."
echo "[report]   2. Follow docs/soak-test-checklist.md through the session."
echo "[report]   3. After the soak, each tester runs ./scripts/pull-quest-logs.sh --zip"
echo "[report]      on their own machine and drops the renamed zip into ${REL}/."
