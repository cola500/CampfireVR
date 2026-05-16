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
#   ./scripts/build-quest.sh                            # build only
#   ./scripts/build-quest.sh --install                  # build + adb install -r
#   ./scripts/build-quest.sh --launch                   # build + install + monkey-launch
#   ./scripts/build-quest.sh --install-only             # skip build, install CampfireVR-latest.apk
#   ./scripts/build-quest.sh --install-only --launch    # skip build, install + launch
#   ./scripts/build-quest.sh --apk PATH                 # install a specific APK (overrides latest)
#   ./scripts/build-quest.sh --install-only --apk PATH  # install a specific older APK
#   ./scripts/build-quest.sh --help
#
# Version tag for the filename is resolved in this order:
#   1) First [v…] heading in CHANGELOG.md (e.g. "v0.1.2-session-fix")
#   2) bundleVersion from ProjectSettings.asset (e.g. "v1.0")
#   3) Fallback "v0.1.0"
#
# Requirements:
#   * Unity ${UNITY_VERSION} (set via env if you upgrade) installed under
#     /Applications/Unity/Hub/Editor/<version>/Unity.app
#   * Unity Editor must NOT have CampfireVR open in another window — Unity
#     can't acquire the project lock from batchmode if the GUI is editing
#     the same project. Close the Editor first, or use a separate Unity
#     instance for batch builds.
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
Usage: $(basename "$0") [--install] [--launch] [--install-only] [--apk PATH] [--help]

Builds CampfireVR APK via Unity batchmode. Wraps
Tools/Quest Setup/Build Remote Fika APK from QuestBuildAPK.cs.

Each successful build produces:
  Builds/CampfireVR-<version>-<YYYYMMDD-HHMM>.apk   (versioned, retained)
  Builds/CampfireVR-latest.apk                      (copy of the above)

Options:
  --install        adb install -r the freshly built APK onto the connected Quest
  --launch         adb monkey-launch the app after install (implies --install)
  --install-only   skip the Unity build; install CampfireVR-latest.apk
  --apk PATH       install the specific APK at PATH instead of latest/freshly-built
                   (works with or without --install-only)
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

VERSION=""    # resolved lazily, after arg parse, only if needed
STAMP=""

SKIP_BUILD=0
INSTALL=0
LAUNCH=0
EXPLICIT_APK=""

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
        -h|--help)      usage; exit 0 ;;
        *) echo "Unknown arg: $1" >&2; usage >&2; exit 1 ;;
    esac
done

# Compute the versioned-build filename only when we'll actually build.
if [[ $SKIP_BUILD -eq 0 ]]; then
    VERSION="$(resolve_version)"
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
    echo "[build] Unity ${UNITY_VERSION} · version tag = ${VERSION} · stamp = ${STAMP}"
    echo "[build] (Close the Editor first if CampfireVR is open in the GUI.)"

    # Verify Unity actually wrote a fresh file by tracking the temp path's mtime.
    # Unity sometimes exits 0 even when BuildResult is Failed — the timestamp
    # is the only reliable signal.
    PREV_MTIME=0
    [[ -f "$BUILD_TEMP_APK" ]] && PREV_MTIME=$(stat -f %m "$BUILD_TEMP_APK")

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

if [[ -n "$EXPLICIT_APK" ]]; then
    APK_PATH="$EXPLICIT_APK"
elif [[ $SKIP_BUILD -eq 1 ]]; then
    APK_PATH="$LATEST_APK"
else
    APK_PATH="$VERSIONED_APK"
fi

if [[ $INSTALL -eq 1 && ! -f "$APK_PATH" ]]; then
    echo "[install] APK not found: $APK_PATH" >&2
    if [[ $SKIP_BUILD -eq 1 && -z "$EXPLICIT_APK" ]]; then
        echo "[install] Run without --install-only first to produce a build." >&2
    fi
    exit 5
fi

if [[ $INSTALL -eq 1 && $SKIP_BUILD -eq 1 ]]; then
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
