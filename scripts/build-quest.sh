#!/usr/bin/env bash
#
# scripts/build-quest.sh — local Quest APK build + optional deploy.
#
# Wraps `Tools → Quest Setup → Build Remote Fika APK` (= QuestBuildAPK.Build)
# in a Unity batchmode invocation, so iteration doesn't require opening the
# Editor and clicking Cmd+B. Each build is renamed to a versioned filename
# (CampfireVR-<version>-<YYYYMMDD-HHMM>.apk) and a copy is mirrored to
# CampfireVR-latest.apk as a convenience pointer. Old versioned APKs are
# never auto-deleted.
#
# Usage:
#   ./scripts/build-quest.sh                            # build only (Editor must be CLOSED)
#   ./scripts/build-quest.sh --install                  # build + adb install -r (Editor CLOSED)
#   ./scripts/build-quest.sh --launch                   # build + install + monkey-launch (Editor CLOSED)
#   ./scripts/build-quest.sh --install-only             # install newest Builds/*.apk — Editor can stay OPEN
#   ./scripts/build-quest.sh --install-only --launch    # install + launch — Editor can stay OPEN
#   ./scripts/build-quest.sh --apk PATH                 # install a specific APK (overrides newest)
#   ./scripts/build-quest.sh --install-only --apk PATH  # install a specific older APK
#   ./scripts/build-quest.sh --list                     # list APKs in Builds/ by mtime, newest first
#   ./scripts/build-quest.sh --help
#
# Fast iteration loop (no Editor close needed):
#   1. In Unity Editor: Tools → Quest Setup → Build Remote Fika APK  (or Cmd+B for Build And Run)
#   2. In terminal:     ./scripts/build-quest.sh --install-only --launch
#   See docs/release-process.md "Fast iteration workflow" for the full pattern,
#   including the one-click Editor menu Build + Deploy to Quest.
#
# Version tag for the filename is resolved in this order:
#   1) First [v…] heading in CHANGELOG.md (e.g. "v0.1.2-session-fix")
#   2) bundleVersion from ProjectSettings.asset (e.g. "v1.0")
#   3) Fallback "v0.1.0"
#
# Requirements:
#   * Unity ${UNITY_VERSION} (set via env if you upgrade) installed under
#     /Applications/Unity/Hub/Editor/<version>/Unity.app
#   * Unity Editor must NOT have CampfireVR open in another window FOR FULL
#     BUILDS ONLY. Batchmode can't acquire the project lock if the GUI is
#     editing the same project; close the Editor first, or use a separate
#     Unity instance. `--install-only` and `--list` do not invoke Unity and
#     are safe to run with the Editor open at any time.
#   * For --install / --launch: Quest connected via USB-C with Developer
#     Mode enabled. Run `adb devices` once first time to authorise.
#
# Exit codes:
#   0 — success
#   1 — bad CLI args
#   2 — Unity / project not found
#   3 — Unity ran but no APK produced (check the log Unity wrote to stdout)
#   4 — adb not found / no Quest connected (only when --install / --launch was requested)
#   5 — install target APK not found (for --install-only / --apk)

set -euo pipefail

UNITY_VERSION="${UNITY_VERSION:-6000.4.7f1}"
UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
ADB="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="${REPO_ROOT}/UnityProject"
OUTPUT_DIR="${PROJECT_PATH}/Builds"
PACKAGE_ID="com.unitymcplab.campfireroom"
LAUNCH_ACTIVITY="${PACKAGE_ID}/com.unity3d.player.UnityPlayerGameActivity"

# QuestBuildAPK.DefaultName — what Unity batchmode writes to. We rename it
# to a versioned filename + mirror to latest immediately after the build,
# so this file never persists in the Builds/ directory after a successful run.
BUILD_TEMP_APK="${OUTPUT_DIR}/CampfireVR-remote-fika-test-v0.1.apk"

# CampfireVR-latest.apk is always a copy of the most recently built APK.
# Friendly default for --install-only.
LATEST_APK="${OUTPUT_DIR}/CampfireVR-latest.apk"

usage() {
    cat <<EOF
Usage: $(basename "$0") [--install] [--launch] [--install-only] [--apk PATH] [--list] [--help]

Builds CampfireVR APK via Unity batchmode. Wraps
Tools/Quest Setup/Build Remote Fika APK from QuestBuildAPK.cs.

Each successful build produces:
  Builds/CampfireVR-<version>-<YYYYMMDD-HHMM>.apk   (versioned, retained)
  Builds/CampfireVR-latest.apk                      (copy of the above)

Options:
  --install        adb install -r the freshly built APK onto the connected Quest
  --launch         adb monkey-launch the app after install (implies --install)
  --install-only   skip the Unity build; install the newest CampfireVR-*.apk
                   in Builds/ (mtime). Editor can stay OPEN.
  --apk PATH       install the specific APK at PATH instead of newest/freshly-built
                   (works with or without --install-only)
  --list           list APKs in Builds/ by mtime, newest first; does not install
  -h, --help       show this message

Env:
  UNITY_VERSION   Unity Editor version (default: 6000.4.7f1)
EOF
}

# Resolve the version tag for the APK filename.
# Order: CHANGELOG.md latest version heading → bundleVersion → fallback.
resolve_version() {
    local v=""

    # 1) CHANGELOG.md — first heading matching `## [v...]` after Unreleased.
    if [[ -f "${REPO_ROOT}/CHANGELOG.md" ]]; then
        v=$(awk '/^## \[v[0-9]/ {gsub(/[][]/, "", $2); print $2; exit}' "${REPO_ROOT}/CHANGELOG.md")
    fi

    # 2) ProjectSettings bundleVersion, prefixed with v.
    if [[ -z "$v" && -f "${PROJECT_PATH}/ProjectSettings/ProjectSettings.asset" ]]; then
        local bv
        bv=$(grep -E '^[[:space:]]*bundleVersion:' \
            "${PROJECT_PATH}/ProjectSettings/ProjectSettings.asset" \
            | head -1 | awk '{print $2}')
        if [[ -n "$bv" ]]; then v="v${bv}"; fi
    fi

    # 3) Fallback.
    if [[ -z "$v" ]]; then v="v0.1.0"; fi

    echo "$v"
}

# Resolve the Android bundleVersionCode for this build. Must strictly
# increase on every Meta Horizon Store / App Lab upload — uploading the
# same value twice is rejected.
#
# `git rev-list --count HEAD` is a free monotonic counter: it increments
# with every commit on main and never decreases. Falls back to a Unix-epoch
# minute (modulo 32-bit signed int range) for unusual cases like a shallow
# clone where commit-counting isn't available.
resolve_version_code() {
    local n
    n=$( (cd "$REPO_ROOT" && git rev-list --count HEAD 2>/dev/null) || true)
    if [[ -z "$n" || ! "$n" =~ ^[0-9]+$ || "$n" -lt 1 ]]; then
        # Fallback: minutes since 2026-01-01 UTC. Strictly increasing,
        # fits in 31 bits for ~4000 years.
        n=$(( ($(date -u +%s) - 1767225600) / 60 ))
        if [[ "$n" -lt 1 ]]; then n=1; fi
    fi
    echo "$n"
}

VERSION=""    # resolved lazily, after arg parse, only if needed
VERSION_CODE=""
STAMP=""

# Build-info JSON metadata written into Resources/ so DebugLogger can read it
# at app startup and stamp every session log with the exact build identity.
# The .json file is gitignored — regenerated per build. Unity auto-creates
# its .meta on first import.
BUILD_INFO_JSON="${PROJECT_PATH}/Assets/Resources/build-info.json"

# Writes Assets/Resources/build-info.json. Called once before the Unity
# batchmode invocation; the asset is then included in the APK as a
# TextAsset under Resources/build-info.
generate_build_info() {
    local apk_name="$1"

    local commit short_commit branch dirty build_time
    commit=$( (cd "$REPO_ROOT" && git rev-parse HEAD 2>/dev/null) || echo "unknown")
    short_commit=$( (cd "$REPO_ROOT" && git rev-parse --short HEAD 2>/dev/null) || echo "unknown")
    branch=$( (cd "$REPO_ROOT" && git rev-parse --abbrev-ref HEAD 2>/dev/null) || echo "unknown")
    if (cd "$REPO_ROOT" && git diff --quiet 2>/dev/null && git diff --cached --quiet 2>/dev/null); then
        dirty="false"
    else
        dirty="true"
    fi
    build_time=$(date "+%Y-%m-%dT%H:%M:%S%z")

    # Changelog summary: the bullets under the first non-empty `## ` block
    # in CHANGELOG.md. Prefers `[Unreleased]` entries if present (= "what
    # changed since the last shipped build"); otherwise falls through to
    # the most recently shipped version's bullets. Max 5 entries, each
    # truncated to ~80 chars and stripped down to the bolded title.
    local summary=""
    if [[ -f "$REPO_ROOT/CHANGELOG.md" ]]; then
        summary=$(awk '
            BEGIN { collecting = 0; n = 0 }
            /^## / {
                if (n > 0) exit
                collecting = 1
                next
            }
            collecting && /^- / && n < 5 {
                line = $0
                sub(/^- /, "", line)
                sub(/^\*\*/, "", line)
                sub(/\*\*.*/, "", line)
                if (length(line) > 80) line = substr(line, 1, 77) "..."
                gsub(/\\/, "\\\\", line)
                gsub(/"/,  "\\\"", line)
                n++
                out[n] = line
            }
            END {
                for (i = 1; i <= n; i++) {
                    printf "%s\"%s\"", (i == 1 ? "" : ","), out[i]
                }
            }
        ' "$REPO_ROOT/CHANGELOG.md")
    fi

    mkdir -p "$(dirname "$BUILD_INFO_JSON")"
    cat > "$BUILD_INFO_JSON" <<EOF
{
  "version":           "${VERSION}",
  "versionCode":       ${VERSION_CODE},
  "buildTime":         "${build_time}",
  "gitCommit":         "${short_commit}",
  "gitCommitLong":     "${commit}",
  "gitBranch":         "${branch}",
  "gitDirty":          ${dirty},
  "apkName":           "${apk_name}",
  "changelogVersion":  "${VERSION}",
  "changelogSummary":  [${summary}]
}
EOF

    echo "[build-info] ${VERSION} (code ${VERSION_CODE}) · ${short_commit}$([ "$dirty" = "true" ] && echo "-dirty") · ${branch} · ${build_time}"
    if [[ "$dirty" == "true" ]]; then
        echo "[build-info] ⚠ Repo is dirty — uncommitted changes are baked into this APK." >&2
    fi
}

# Lists all CampfireVR-*.apk files in Builds/ by mtime, newest first. Used
# by --list (user-facing) and by find_newest_apk (programmatic).
list_apks() {
    if [[ ! -d "$OUTPUT_DIR" ]]; then return; fi
    # Portable mtime-sort: ls -t handles macOS bash 3.2 fine. Filter to
    # CampfireVR-*.apk so unrelated files in Builds/ don't slip in.
    ( cd "$OUTPUT_DIR" && ls -1t CampfireVR-*.apk 2>/dev/null ) | while IFS= read -r f; do
        echo "${OUTPUT_DIR}/${f}"
    done
}

# Returns the path of the newest APK in Builds/, or empty if none exist.
# Always prefers the actual newest file on disk over any specific filename,
# so Editor-side builds (which land at the QuestBuildAPK temp path) get
# picked up automatically without the user knowing the path.
find_newest_apk() {
    list_apks | head -1
}

SKIP_BUILD=0
INSTALL=0
LAUNCH=0
EXPLICIT_APK=""
LIST_ONLY=0

# Arg loop supports both flag-only and flag-with-value forms.
while [[ $# -gt 0 ]]; do
    case "$1" in
        --install)      INSTALL=1; shift ;;
        --launch)       INSTALL=1; LAUNCH=1; shift ;;
        --install-only) SKIP_BUILD=1; INSTALL=1; shift ;;
        --apk)
            if [[ -z "${2:-}" ]]; then
                echo "--apk requires a path argument." >&2
                exit 1
            fi
            EXPLICIT_APK="$2"
            shift 2
            ;;
        --list)         LIST_ONLY=1; shift ;;
        -h|--help)      usage; exit 0 ;;
        *) echo "Unknown arg: $1" >&2; usage >&2; exit 1 ;;
    esac
done

# --list is a read-only inspection mode — runs without touching Unity or adb.
if [[ $LIST_ONLY -eq 1 ]]; then
    if [[ ! -d "$OUTPUT_DIR" ]]; then
        echo "No Builds/ directory yet."
        exit 0
    fi
    found=0
    while IFS= read -r apk; do
        [[ -z "$apk" ]] && continue
        size=$(du -h "$apk" | cut -f1)
        mtime=$(stat -f "%Sm" "$apk")
        echo "  ${size}  ${mtime}  ${apk#"${REPO_ROOT}/"}"
        found=1
    done < <(list_apks)
    if [[ $found -eq 0 ]]; then
        echo "No APKs in $OUTPUT_DIR yet — run without --list to build one."
    fi
    exit 0
fi

# Compute the versioned-build filename only when we'll actually build.
if [[ $SKIP_BUILD -eq 0 ]]; then
    VERSION="$(resolve_version)"
    VERSION_CODE="$(resolve_version_code)"
    STAMP="$(date +%Y%m%d-%H%M)"
fi
VERSIONED_APK="${OUTPUT_DIR}/CampfireVR-${VERSION}-${STAMP}.apk"

# --- build block --------------------------------------------------------

if [[ $SKIP_BUILD -eq 0 ]]; then
    if [[ ! -x "$UNITY" ]]; then
        echo "Unity not found at $UNITY" >&2
        echo "Set UNITY_VERSION env to the installed Editor version." >&2
        exit 2
    fi
    if [[ ! -d "$PROJECT_PATH" ]]; then
        echo "Project not found at $PROJECT_PATH" >&2
        exit 2
    fi

    mkdir -p "$OUTPUT_DIR"
    echo "[build] Unity ${UNITY_VERSION} · version tag = ${VERSION} (code ${VERSION_CODE}) · stamp = ${STAMP}"
    echo "[build] (Close the Editor first if CampfireVR is open in the GUI.)"

    # Generate build-info.json BEFORE Unity batchmode so it gets imported as
    # a TextAsset under Resources/ and included in the APK. DebugLogger loads
    # it at app startup to stamp the session log with the exact build identity.
    generate_build_info "$(basename "$VERSIONED_APK")"

    # Verify Unity actually wrote a fresh file by tracking the temp path's mtime.
    # Unity sometimes exits 0 even when BuildResult is Failed — the timestamp
    # is the only reliable signal.
    PREV_MTIME=0
    [[ -f "$BUILD_TEMP_APK" ]] && PREV_MTIME=$(stat -f %m "$BUILD_TEMP_APK")

    # Pass the resolved versionCode + the keystore env vars (already in our
    # shell env if Johan has them in ~/.zshrc) through to Unity batchmode.
    # VersionCodeGuard + ReleaseSigningGuard read these at pre-build time.
    export CAMPFIREVR_VERSION_CODE="$VERSION_CODE"

    "$UNITY" \
        -batchmode \
        -nographics \
        -projectPath "$PROJECT_PATH" \
        -buildTarget Android \
        -executeMethod QuestBuildAPK.Build \
        -quit \
        -logFile -

    if [[ ! -f "$BUILD_TEMP_APK" ]]; then
        echo "[build] APK not produced at $BUILD_TEMP_APK" >&2
        exit 3
    fi
    NEW_MTIME=$(stat -f %m "$BUILD_TEMP_APK")
    if [[ "$NEW_MTIME" == "$PREV_MTIME" ]]; then
        echo "[build] APK timestamp unchanged — Unity reported success but didn't write a new build" >&2
        exit 3
    fi

    # Rename the fresh temp APK to its versioned filename, and update the
    # convenience CampfireVR-latest.apk pointer (a copy, since symlinks in
    # macOS Builds/ can confuse adb on different shells).
    mv "$BUILD_TEMP_APK" "$VERSIONED_APK"
    cp "$VERSIONED_APK" "$LATEST_APK"

    echo "[build] OK · $(du -h "$VERSIONED_APK" | cut -f1) · ${VERSIONED_APK#"${REPO_ROOT}/"}"
    echo "[build] Latest · ${LATEST_APK#"${REPO_ROOT}/"}"
fi

# --- resolve which APK to install ---------------------------------------
#
# Resolution order:
#   1. --apk PATH         (always wins — explicit override)
#   2. --install-only     (auto-detect newest CampfireVR-*.apk in Builds/ by
#                         mtime; lets Editor-side builds get picked up
#                         without the user supplying a filename)
#   3. (after a fresh build) the file we just produced this run

if [[ -n "$EXPLICIT_APK" ]]; then
    APK_PATH="$EXPLICIT_APK"
elif [[ $SKIP_BUILD -eq 1 ]]; then
    APK_PATH="$(find_newest_apk)"
    if [[ -z "$APK_PATH" ]]; then
        # Older recipes may still rely on the literal CampfireVR-latest.apk
        # pointer; fall back to it so backwards-compat shell snippets work.
        [[ -f "$LATEST_APK" ]] && APK_PATH="$LATEST_APK"
    fi
else
    APK_PATH="$VERSIONED_APK"
fi

if [[ $INSTALL -eq 1 && ( -z "$APK_PATH" || ! -f "$APK_PATH" ) ]]; then
    echo "[install] APK not found: ${APK_PATH:-<none>}" >&2
    if [[ $SKIP_BUILD -eq 1 && -z "$EXPLICIT_APK" ]]; then
        echo "[install] No CampfireVR-*.apk in $OUTPUT_DIR." >&2
        echo "[install] Either:" >&2
        echo "[install]   - run without --install-only to build via batchmode (Editor must be closed)" >&2
        echo "[install]   - build inside the open Editor via Tools → Quest Setup → Build Remote Fika APK" >&2
        echo "[install]   - or use Tools → Quest Setup → Build + Deploy to Quest for a one-click pipeline" >&2
    fi
    exit 5
fi

if [[ $INSTALL -eq 1 && $SKIP_BUILD -eq 1 ]]; then
    echo "[install-only] Editor can stay open — this path doesn't invoke Unity."
    echo "[install-only] Using APK · $(du -h "$APK_PATH" | cut -f1) · built $(stat -f %Sm "$APK_PATH") · ${APK_PATH#"${REPO_ROOT}/"}"
fi

# --- install / launch ---------------------------------------------------

if [[ $INSTALL -eq 1 ]]; then
    if [[ ! -x "$ADB" ]]; then
        echo "adb not found at $ADB" >&2
        exit 4
    fi
    DEVICES=$("$ADB" devices | awk 'NR>1 && $2=="device" {print $1}')
    if [[ -z "$DEVICES" ]]; then
        echo "[install] No authorised Quest connected. Run '$ADB devices' to check." >&2
        exit 4
    fi
    echo "[install] adb install -r → $(echo "$DEVICES" | head -1)"
    "$ADB" install -r "$APK_PATH"
fi

if [[ $LAUNCH -eq 1 ]]; then
    echo "[launch] monkey → $LAUNCH_ACTIVITY"
    "$ADB" shell monkey -p "$PACKAGE_ID" -c android.intent.category.LAUNCHER 1
fi
