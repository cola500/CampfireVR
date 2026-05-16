#!/usr/bin/env bash
#
# scripts/build-quest.sh — local Quest APK build + optional deploy.
#
# Wraps `Tools → Quest Setup → Build Remote Fika APK` (= QuestBuildAPK.Build)
# in a Unity batchmode invocation, so iteration doesn't require opening the
# Editor and clicking Cmd+B. With --install / --launch, chains adb on a
# USB-connected Quest.
#
# Usage:
#   ./scripts/build-quest.sh                # build only
#   ./scripts/build-quest.sh --install      # build + adb install -r
#   ./scripts/build-quest.sh --launch       # build + install + monkey-launch
#   ./scripts/build-quest.sh --help
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
#   4 — adb not found (only when --install / --launch was requested)

set -euo pipefail

UNITY_VERSION="${UNITY_VERSION:-6000.4.7f1}"
UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
ADB="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="${REPO_ROOT}/UnityProject"
APK_PATH="${PROJECT_PATH}/Builds/CampfireVR-remote-fika-test-v0.1.apk"
PACKAGE_ID="com.unitymcplab.campfireroom"
LAUNCH_ACTIVITY="${PACKAGE_ID}/com.unity3d.player.UnityPlayerGameActivity"

usage() {
    cat <<EOF
Usage: $(basename "$0") [--install] [--launch] [--help]

Builds CampfireVR APK via Unity batchmode. Wraps
Tools/Quest Setup/Build Remote Fika APK from QuestBuildAPK.cs.

Options:
  --install   adb install -r the built APK onto the first connected Quest
  --launch    adb monkey-launch the app after install (implies --install)
  -h, --help  show this message

Env:
  UNITY_VERSION   Unity Editor version (default: 6000.4.7f1)
EOF
}

INSTALL=0
LAUNCH=0
for arg in "$@"; do
    case "$arg" in
        --install) INSTALL=1 ;;
        --launch)  INSTALL=1; LAUNCH=1 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown arg: $arg" >&2; usage >&2; exit 1 ;;
    esac
done

if [[ ! -x "$UNITY" ]]; then
    echo "Unity not found at $UNITY" >&2
    echo "Set UNITY_VERSION env to the installed Editor version." >&2
    exit 2
fi
if [[ ! -d "$PROJECT_PATH" ]]; then
    echo "Project not found at $PROJECT_PATH" >&2
    exit 2
fi

echo "[build] Unity ${UNITY_VERSION} → ${APK_PATH#"${REPO_ROOT}/"}"
echo "[build] (Close the Editor first if CampfireVR is open in the GUI.)"

# Capture previous APK mtime so we can verify the build actually wrote a new
# file (Unity sometimes exits 0 even when BuildResult is Failed — the only
# reliable signal is the APK timestamp).
PREV_MTIME=0
[[ -f "$APK_PATH" ]] && PREV_MTIME=$(stat -f %m "$APK_PATH")

"$UNITY" \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT_PATH" \
    -buildTarget Android \
    -executeMethod QuestBuildAPK.Build \
    -quit \
    -logFile -

if [[ ! -f "$APK_PATH" ]]; then
    echo "[build] APK not produced at $APK_PATH" >&2
    exit 3
fi
NEW_MTIME=$(stat -f %m "$APK_PATH")
if [[ "$NEW_MTIME" == "$PREV_MTIME" ]]; then
    echo "[build] APK timestamp unchanged — Unity reported success but didn't write a new build" >&2
    exit 3
fi
echo "[build] OK · $(du -h "$APK_PATH" | cut -f1) · ${APK_PATH#"${REPO_ROOT}/"}"

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
