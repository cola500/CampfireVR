#!/usr/bin/env bash
#
# scripts/package-friend-test.sh — bundle a tested APK + docs into an
# immutable, traceable friend test zip.
#
# Replaces the manual "## 4. Create the friend package" recipe in
# docs/release-process.md. Reads UnityProject/Assets/Resources/build-info.json
# (written by scripts/build-quest.sh at build time) to pick the APK and to
# stamp the zip filename with the exact build version + timestamp, so two
# different builds can never collide on the same zip name.
#
# Artifact policy:
#   - Versioned APKs (Builds/CampfireVR-v<ver>-<YYYYMMDD-HHMM>.apk) are
#     IMMUTABLE — already named by build-quest.sh, never overwritten.
#   - Builds/CampfireVR-latest.apk is an overwriteable convenience pointer.
#     Do not ship it directly; use the versioned filename it currently
#     points at.
#   - dist/friend-test/ is a transient STAGING folder — we recreate it
#     every run. Don't archive it; it's not the release artifact.
#   - dist/CampfireVR-friend-test-<version>-<YYYYMMDD-HHMM>.zip is the
#     release artifact. Immutable: the script refuses to overwrite an
#     existing zip with the same name (use --force only when you really
#     mean it).
#
# Usage:
#   ./scripts/package-friend-test.sh                # use newest APK matching BUILD-INFO
#   ./scripts/package-friend-test.sh --apk PATH     # package a specific APK instead
#   ./scripts/package-friend-test.sh --force        # overwrite an existing zip
#   ./scripts/package-friend-test.sh --no-sha256    # skip SHA256 generation
#   ./scripts/package-friend-test.sh --help
#
# The default flow:
#   1. Read UnityProject/Assets/Resources/build-info.json.
#   2. Resolve the APK named in build-info.apkName under UnityProject/Builds/.
#   3. Refuse if BUILD-INFO says the tree was dirty unless --allow-dirty.
#      (A dirty build can't be reliably reproduced from the recorded commit;
#      shipping it makes future bug reports ambiguous.)
#   4. Stage dist/friend-test/ with APK + INSTALL.md + DEBUG-LOGS.md +
#      CHANGELOG.md + BUILD-INFO.json + RELEASE-NOTES.md + README.md +
#      SHA256SUMS.
#   5. Zip → dist/CampfireVR-friend-test-<version>-<YYYYMMDD-HHMM>.zip
#   6. Sidecar .sha256 of the zip itself for download-integrity check.
#
# Exit codes:
#   0 — success
#   1 — bad CLI args
#   2 — BUILD-INFO.json or APK not found
#   3 — output zip already exists (use --force to override)
#   4 — build is dirty (use --allow-dirty if you really mean it)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILDS_DIR="${REPO_ROOT}/UnityProject/Builds"
DIST_DIR="${REPO_ROOT}/dist"
STAGING_DIR="${DIST_DIR}/friend-test"
BUILD_INFO="${REPO_ROOT}/UnityProject/Assets/Resources/build-info.json"

usage() {
    cat <<EOF
Usage: $(basename "$0") [--apk PATH] [--force] [--no-sha256] [--allow-dirty] [--help]

Packages the APK named in BUILD-INFO.json into an immutable friend test zip
under dist/, alongside install / debug / changelog docs and a generated
RELEASE-NOTES.md.

Options:
  --apk PATH       Package a specific APK instead of the one named in
                   BUILD-INFO.json. Useful for re-packaging an older
                   versioned build.
  --force          Overwrite an existing zip with the same name. Default
                   is to refuse (immutability is the point).
  --no-sha256      Skip SHA256 generation. Default is to include
                   SHA256SUMS inside the zip and a sidecar .sha256 next
                   to the zip.
  --allow-dirty    Allow packaging a build whose BUILD-INFO records
                   gitDirty=true. Default is to refuse — uncommitted
                   changes can't be reliably reconstructed from the
                   recorded commit.
  -h, --help       Show this message.

Output:
  dist/friend-test/                                 (staging — recreated)
  dist/CampfireVR-friend-test-<version>-<stamp>.zip (release artifact)
  dist/CampfireVR-friend-test-<version>-<stamp>.zip.sha256 (sidecar)
EOF
}

EXPLICIT_APK=""
FORCE=0
SKIP_SHA=0
ALLOW_DIRTY=0

while [[ $# -gt 0 ]]; do
    case "$1" in
        --apk)
            if [[ -z "${2:-}" ]]; then
                echo "--apk requires a path argument." >&2
                exit 1
            fi
            EXPLICIT_APK="$2"
            shift 2
            ;;
        --force)        FORCE=1; shift ;;
        --no-sha256)    SKIP_SHA=1; shift ;;
        --allow-dirty)  ALLOW_DIRTY=1; shift ;;
        -h|--help)      usage; exit 0 ;;
        *) echo "Unknown arg: $1" >&2; usage >&2; exit 1 ;;
    esac
done

# --- read BUILD-INFO.json -------------------------------------------------

if [[ ! -f "$BUILD_INFO" ]]; then
    echo "BUILD-INFO.json not found at $BUILD_INFO" >&2
    echo "Run ./scripts/build-quest.sh first to generate it." >&2
    exit 2
fi

# Tiny JSON reader — pulls a top-level string field. Avoids a python/jq
# dependency for a script that already runs alongside build-quest.sh.
json_str() {
    local key="$1"
    sed -nE "s/^[[:space:]]*\"$key\"[[:space:]]*:[[:space:]]*\"([^\"]*)\".*/\1/p" "$BUILD_INFO" | head -1
}
json_bool() {
    local key="$1"
    sed -nE "s/^[[:space:]]*\"$key\"[[:space:]]*:[[:space:]]*(true|false).*/\1/p" "$BUILD_INFO" | head -1
}
json_int() {
    local key="$1"
    sed -nE "s/^[[:space:]]*\"$key\"[[:space:]]*:[[:space:]]*([0-9]+).*/\1/p" "$BUILD_INFO" | head -1
}

VERSION="$(json_str version)"
VERSION_CODE="$(json_int versionCode)"
APK_NAME_FROM_INFO="$(json_str apkName)"
GIT_COMMIT="$(json_str gitCommit)"
GIT_BRANCH="$(json_str gitBranch)"
BUILD_TIME="$(json_str buildTime)"
GIT_DIRTY="$(json_bool gitDirty)"

if [[ -z "$VERSION" || -z "$APK_NAME_FROM_INFO" ]]; then
    echo "BUILD-INFO.json missing required fields (version / apkName)." >&2
    exit 2
fi

# --- resolve APK ----------------------------------------------------------

if [[ -n "$EXPLICIT_APK" ]]; then
    APK_PATH="$EXPLICIT_APK"
    APK_NAME="$(basename "$APK_PATH")"
else
    APK_NAME="$APK_NAME_FROM_INFO"
    APK_PATH="${BUILDS_DIR}/${APK_NAME}"
fi

if [[ ! -f "$APK_PATH" ]]; then
    echo "APK not found: $APK_PATH" >&2
    if [[ -z "$EXPLICIT_APK" ]]; then
        echo "BUILD-INFO says $APK_NAME_FROM_INFO but it isn't under $BUILDS_DIR." >&2
        echo "Either re-run ./scripts/build-quest.sh, or pass --apk PATH to override." >&2
    fi
    exit 2
fi

APK_SIZE_BYTES="$(stat -f %z "$APK_PATH")"
if [[ "$APK_SIZE_BYTES" -eq 0 ]]; then
    echo "APK is zero bytes — refusing to package: $APK_PATH" >&2
    exit 2
fi

# --- dirty-tree guard -----------------------------------------------------

if [[ "$GIT_DIRTY" == "true" && $ALLOW_DIRTY -eq 0 ]]; then
    echo "BUILD-INFO says this build was produced from a dirty tree." >&2
    echo "Packaging it makes bug reports ambiguous — the recorded commit" >&2
    echo "$GIT_COMMIT can't be checked out to reproduce what the tester ran." >&2
    echo "Either rebuild from a clean tree, or pass --allow-dirty." >&2
    exit 4
fi

# --- derive zip filename --------------------------------------------------
#
# APK filename pattern: CampfireVR-<version>-<YYYYMMDD-HHMM>.apk
# Extract the <YYYYMMDD-HHMM> tail (last 13 chars before .apk) as the
# build stamp — guarantees the zip filename matches the APK's exact build.
STAMP="$(echo "$APK_NAME" | sed -nE 's/.*-([0-9]{8}-[0-9]{4})\.apk$/\1/p')"
if [[ -z "$STAMP" ]]; then
    # Fall back to BUILD-INFO.buildTime — collapse to YYYYMMDD-HHMM.
    # 2026-05-17T19:37:28+0200 → 20260517-1937
    STAMP="$(echo "$BUILD_TIME" | sed -E 's/-//g; s/T/-/; s/[:].*//' | sed -E 's/^([0-9]{8}-[0-9]{2})([0-9]{2}).*/\1\2/')"
fi
if [[ -z "$STAMP" ]]; then
    echo "Could not derive build stamp from APK name '$APK_NAME' or buildTime '$BUILD_TIME'." >&2
    exit 2
fi

ZIP_BASENAME="CampfireVR-friend-test-${VERSION}-${STAMP}.zip"
ZIP_PATH="${DIST_DIR}/${ZIP_BASENAME}"
ZIP_SHA_PATH="${ZIP_PATH}.sha256"

if [[ -f "$ZIP_PATH" && $FORCE -eq 0 ]]; then
    echo "Zip already exists and is immutable: $ZIP_PATH" >&2
    echo "If you intentionally want to overwrite it, re-run with --force." >&2
    exit 3
fi

# --- stage ----------------------------------------------------------------

mkdir -p "$DIST_DIR"
rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR"

cp "$APK_PATH"                                       "$STAGING_DIR/"
cp "$REPO_ROOT/docs/install-on-quest.md"             "$STAGING_DIR/INSTALL.md"
cp "$REPO_ROOT/docs/debug-logging.md"                "$STAGING_DIR/DEBUG-LOGS.md"
cp "$REPO_ROOT/CHANGELOG.md"                         "$STAGING_DIR/CHANGELOG.md"
cp "$BUILD_INFO"                                     "$STAGING_DIR/BUILD-INFO.json"

# --- RELEASE-NOTES.md -----------------------------------------------------
#
# Plain-bash version of the python recipe previously in docs/release-process.md
# — keeps the script dependency-free. Pulls the changelogSummary array
# bullets out of BUILD-INFO.json with a tiny awk pass.

DIRTY_NOTE=""
[[ "$GIT_DIRTY" == "true" ]] && DIRTY_NOTE=" (uncommitted changes)"

# changelogSummary: ["a","b","c"] → \n- a\n- b\n- c
BULLETS="$(awk '
    /"changelogSummary"/ {
        line = $0
        sub(/.*\[/, "", line)
        sub(/\].*/, "", line)
        gsub(/","/, "\n", line)
        gsub(/"/,   "",   line)
        gsub(/^ +| +$/, "", line)
        n = split(line, arr, "\n")
        for (i = 1; i <= n; i++) {
            if (arr[i] != "") printf "- %s\n", arr[i]
        }
    }
' "$BUILD_INFO")"
[[ -z "$BULLETS" ]] && BULLETS="- See CHANGELOG.md."

VERSION_CODE_LINE=""
[[ -n "$VERSION_CODE" ]] && VERSION_CODE_LINE="**Version code:** ${VERSION_CODE}
"

cat > "$STAGING_DIR/RELEASE-NOTES.md" <<EOF
# CampfireVR — ${VERSION}

**APK:** ${APK_NAME}
**Built:** ${BUILD_TIME}
**Commit:** ${GIT_COMMIT} on ${GIT_BRANCH}${DIRTY_NOTE}
${VERSION_CODE_LINE}

## What's new

${BULLETS}

See CHANGELOG.md for the full per-version history, INSTALL.md for the install walkthrough, and DEBUG-LOGS.md for how to send logs back if something breaks.
EOF

# --- README.md ------------------------------------------------------------
#
# Friend-facing intro. APK filename + commit are embedded so the tester
# can quote them in a bug report without launching the headset.

APK_SIZE_MB="$(du -h "$APK_PATH" | awk '{print $1}')"

cat > "$STAGING_DIR/README.md" <<EOF
# CampfireVR — friend test build

Thanks for testing! This zip has everything you need to install CampfireVR on your Meta Quest 3 and join a remote campfire session.

**This build:** \`${VERSION}\` (code ${VERSION_CODE:-unset}) · built ${BUILD_TIME} · commit ${GIT_COMMIT}${DIRTY_NOTE}
See \`RELEASE-NOTES.md\` for a one-page summary and \`CHANGELOG.md\` for the full per-version history.

## What's in here

| File | What it is |
|---|---|
| \`${APK_NAME}\` | The app — sideloads onto your Quest. ~${APK_SIZE_MB}. |
| \`INSTALL.md\` | Full step-by-step install guide. Read this first if you've never sideloaded a Quest app. |
| \`DEBUG-LOGS.md\` | How to pull session logs off the headset if something goes wrong. |
| \`RELEASE-NOTES.md\` | One-page summary of this specific build. |
| \`CHANGELOG.md\` | Full version history. |
| \`BUILD-INFO.json\` | Build identity (version, timestamp, git commit) — same info baked into the APK. |
| \`SHA256SUMS\` | Checksums for the APK so you can verify the download is intact. |
| \`README.md\` | This file. |

## Install — quickest path

**If you have Meta Quest Developer Hub (recommended, no terminal):**

1. Download MQDH: <https://developer.oculus.com/downloads/package/oculus-developer-hub-mac/> (or Windows / Linux equivalent).
2. Sign in with the Meta account paired to your Quest.
3. Make sure Developer Mode is enabled on your Quest (one-time setup — see \`INSTALL.md\` Part A if not).
4. Plug the Quest in via USB-C.
5. Drag \`${APK_NAME}\` onto your headset in MQDH's sidebar. Done.

**If you prefer adb (terminal):**

\`\`\`sh
adb install -r ${APK_NAME}
\`\`\`

If \`adb devices\` shows nothing, see \`INSTALL.md\` troubleshooting section.

## Find and launch the app

Inside the headset:

1. From the home menu → **Apps** (bottom row).
2. Top-right dropdown → **Unknown Sources** — sideloaded apps live here, hidden by default. (Not a bug; this is how Meta hides non-store apps.)
3. Click **CampfireVR**. First launch takes ~10 seconds.

## First minute in headset

You'll see a campfire at night, two stone seats, a forest, a dog by one of the seats, and a world-space text panel that reads \`Room: A\`.

- **To host:** press **left X** on your controller. Tell me "we're on A" — that's the default room.
- **To join:** press **right B** when I say I'm hosting.
- **Use Internet mode** unless we're on the same Wi-Fi. The bottom of the panel should read \`mode · Internet\` — if it says \`Same Wi-Fi\`, press **left Y** to toggle. (Same Wi-Fi mode is dev-only in this build; it won't work for two people in different houses.)
- **Headphones strongly recommended.** Quest speakers leak voice into the other Quest's microphone — voice gets weird without them.
- **Other controls:** A = recenter, right thumbstick = cycle the room letter (only needed if multiple pairs are testing at once).

## If the session gets weird (stop / recovery)

This build has an in-VR recovery path so you don't have to quit the app and start over:

- **Long-press left Y for ~1.5 seconds** → cleanly stops the current session (voice + Relay + networking shutdown). Room letter + mode are preserved. After the stop you can press X to re-host or B to re-join.
- **Short tap left Y** still toggles between Internet and Same Wi-Fi mode (unchanged from before).

If you ever feel like the connection is half-stuck, that's the move — long-press Y, wait a second for the panel to say \`Ready\`, then host or join again.

## If something fails

Please send me the debug logs — they tell me exactly what happened during your session (when you tried to join, what state networking was in, whether voice connected, etc).

Quick recipe (run on your computer with the Quest connected via USB-C):

\`\`\`sh
adb pull /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs ~/Desktop/campfirevr-logs
\`\`\`

Then zip that folder and send it to me. Full instructions in \`DEBUG-LOGS.md\`, including a manual pull recipe if \`adb\` isn't on your machine.

When you report a bug, mentioning the APK filename (\`${APK_NAME}\`) or just the commit (\`${GIT_COMMIT}\`) tells me exactly which build to look at — but the logs already include this so don't worry if you forget.

## Verifying the download (optional)

If you want to be sure the APK isn't corrupted, the included \`SHA256SUMS\` file has the expected hash. Verify with:

\`\`\`sh
shasum -a 256 -c SHA256SUMS
# Expected output:
#   ${APK_NAME}: OK
\`\`\`
EOF

# --- SHA256 ---------------------------------------------------------------

if [[ $SKIP_SHA -eq 0 ]]; then
    ( cd "$STAGING_DIR" && shasum -a 256 "$APK_NAME" > SHA256SUMS )
fi

# --- zip ------------------------------------------------------------------

# Quiet zip (-q) — the staging contents are listed by the validator below.
( cd "$DIST_DIR" && rm -f "$ZIP_BASENAME" && zip -rq "$ZIP_BASENAME" "friend-test/" )

if [[ $SKIP_SHA -eq 0 ]]; then
    ( cd "$DIST_DIR" && shasum -a 256 "$ZIP_BASENAME" > "${ZIP_BASENAME}.sha256" )
fi

# --- summary --------------------------------------------------------------

ZIP_SIZE="$(du -h "$ZIP_PATH" | awk '{print $1}')"

echo "[package] OK · ${ZIP_SIZE} · ${ZIP_PATH#"${REPO_ROOT}/"}"
echo "[package]   build  · ${VERSION} (code ${VERSION_CODE:-unset}) · commit ${GIT_COMMIT} · ${BUILD_TIME}${DIRTY_NOTE}"
echo "[package]   apk    · ${APK_SIZE_MB} · ${APK_NAME}"
if [[ $SKIP_SHA -eq 0 ]]; then
    APK_SHA="$(awk '{print $1}' "$STAGING_DIR/SHA256SUMS")"
    ZIP_SHA="$(awk '{print $1}' "${ZIP_PATH}.sha256")"
    echo "[package]   sha256 · apk ${APK_SHA}"
    echo "[package]   sha256 · zip ${ZIP_SHA}"
fi
echo "[package]   share  · ${ZIP_PATH#"${REPO_ROOT}/"}"
echo "[package]   note   · dist/friend-test/ is staging only — do not share it directly."
