# Release process

How to cut a new test build and ship it to a friend. Five steps, ~10 minutes if nothing breaks.

Tone: this is a one-person indie project. No CI, no release governance, no release notes review board. Just disciplined notes so future-me can reproduce what past-me did.

## 1. Decide the version tag

Open [CHANGELOG.md](../CHANGELOG.md) and pick the next `v0.x.y-suffix` for this build.

Convention:

- Bump the patch number (`v0.1.2` → `v0.1.3`).
- The suffix is a short human-friendly theme (`-session-fix`, `-dog-companion`, `-voice-spatial`). Free-form. Readable beats semantic.
- Don't bump `bundleVersion` in `ProjectSettings.asset` unless something significant changes (rebrand, auth model, major refactor). Most builds reuse the existing `bundleVersion`.

## 2. Move `[Unreleased]` into a new version section

Edit `CHANGELOG.md`:

```
## [Unreleased]

### Added
### Changed
### Fixed

## [v0.1.3-suffix] — YYYY-MM-DD
   ...moved from Unreleased...
```

Don't bother backfilling every commit. Focus on what a tester would care about — "hands no longer point sideways", "voice now spatial", "we added a dog".

If `[Unreleased]` is empty because you've been disciplined and updated it as you went: lucky you. Otherwise, scan `git log --oneline` since the last version tag and write 3–6 bullets per category.

## 3. Build the APK

From the repo root, with Unity Editor **closed** (batchmode can't acquire the project lock if the GUI is open):

```sh
./scripts/build-quest.sh
```

Takes ~1 min warm, ~5–10 min if `Library/` is cold. Each successful build produces two files:

- `UnityProject/Builds/CampfireVR-<version>-<YYYYMMDD-HHMM>.apk` — the versioned artefact. Kept on disk forever (no auto-cleanup); this is what gets shared with friends.
- `UnityProject/Builds/CampfireVR-latest.apk` — copy of the same bits under a stable name. `--install-only` defaults to this.

The version segment comes from step 2 above — the first `## [v…]` heading in `CHANGELOG.md`. So bumping the changelog heading before the build automatically tags the resulting APK. (If CHANGELOG can't be parsed, the script falls back to `bundleVersion` from `ProjectSettings.asset`, then to `v0.1.0`.)

### Build metadata baked into every APK

Before Unity batchmode runs, the script writes `UnityProject/Assets/Resources/build-info.json` capturing the exact identity of this build:

```json
{
  "version":          "v0.1.3-suffix",
  "buildTime":        "2026-05-16T20:27:02+0200",
  "gitCommit":        "7a56b9d",
  "gitCommitLong":    "7a56b9dbd0647ef2f95b02cb93dc4d7178b033f2",
  "gitBranch":        "main",
  "gitDirty":         false,
  "apkName":          "CampfireVR-v0.1.3-suffix-20260516-2027.apk",
  "changelogVersion": "v0.1.3-suffix",
  "changelogSummary": ["First bullet from CHANGELOG", "Second bullet", ...]
}
```

`DebugLogger` reads this at app startup via `Resources.Load<TextAsset>("build-info")` and stamps the session log's `app_started` event with `build_version`, `build_time`, `git_commit`, `git_branch`, `git_dirty`, `apk_name`. See [debug-logging.md](debug-logging.md#build-identity-in-app_started) for the exact fields.

The file is **gitignored** (`UnityProject/Assets/Resources/build-info.json` + `.meta`) so per-build timestamps and git SHAs don't pollute commit history. Regenerated fresh on every `./scripts/build-quest.sh` invocation; safe to delete locally — the next build recreates it.

If the script runs against a dirty working tree, it prints a `⚠ Repo is dirty — uncommitted changes are baked into this APK.` warning to stderr and sets `"gitDirty": true`. Useful trust signal when a tester reports a problem on a build that didn't come from a clean commit.

To deploy directly to a USB-connected Quest:

```sh
./scripts/build-quest.sh --install-only --launch                 # install CampfireVR-latest.apk
./scripts/build-quest.sh --apk Builds/CampfireVR-v0.1.2-...apk --install   # roll back to an older versioned build
```

## 4. Create the friend package

Use the *exact versioned APK filename* that step 3 produced — the friend's report later will reference this name, so we want it traceable:

```sh
# Pick the latest versioned build (most recent mtime among versioned files).
APK=$(ls -t UnityProject/Builds/CampfireVR-v*-*.apk | head -1)
echo "Packaging $APK"

rm -rf dist/friend-test
mkdir -p dist/friend-test
cp "$APK"                                              dist/friend-test/
cp docs/install-on-quest.md                            dist/friend-test/INSTALL.md
cp docs/debug-logging.md                               dist/friend-test/DEBUG-LOGS.md
cp CHANGELOG.md                                        dist/friend-test/CHANGELOG.md
cp UnityProject/Assets/Resources/build-info.json       dist/friend-test/BUILD-INFO.json
```

`BUILD-INFO.json` is the same metadata baked into the APK — handy for the friend (or future-you) to see the exact build identity without launching the headset. A short `RELEASE-NOTES.md` is one shell snippet away:

```sh
# Synthesise dist/friend-test/RELEASE-NOTES.md from build-info.json + CHANGELOG.
python3 -c "
import json, pathlib
info = json.loads(pathlib.Path('UnityProject/Assets/Resources/build-info.json').read_text())
dirty = ' (uncommitted changes)' if info['gitDirty'] else ''
bullets = '\n'.join('- ' + b for b in info['changelogSummary']) or '- See CHANGELOG.md.'
print(f'''# CampfireVR — {info[\"version\"]}

**APK:** {info[\"apkName\"]}
**Built:** {info[\"buildTime\"]}
**Commit:** {info[\"gitCommit\"]} on {info[\"gitBranch\"]}{dirty}

## What's new

{bullets}

See CHANGELOG.md for the full per-version history, INSTALL.md for the install
walkthrough, and DEBUG-LOGS.md for how to send logs back if something breaks.
''')
" > dist/friend-test/RELEASE-NOTES.md
```

Then refresh `dist/friend-test/README.md` so the version is explicit: include the actual APK filename + the CHANGELOG version tag. When the friend reports a bug, the filename they downloaded tells you exactly which build it was.

`docs/debug-logging.md` (= `DEBUG-LOGS.md` in the zip) covers `scripts/pull-quest-logs.sh` for the developer side and a manual `adb pull` recipe for testers who only have Platform Tools. The pull-script is *not* something the tester runs inside their headset — it runs on the computer their Quest is plugged into, after a session, to extract the JSONL logs for sending back.

Then write a short `dist/friend-test/README.md` (or copy + tweak the previous one) that:

- Names the version: "This is `v0.1.3-suffix` — see CHANGELOG.md for what's new."
- Quick install path (drag APK onto MQDH, or `adb install -r`).
- Reminder: room A by default, Internet mode, headphones.
- Reminder: send logs back if something breaks (`adb pull` recipe).

Zip it:

```sh
cd dist
zip -r CampfireVR-friend-test-v0.1.3-suffix.zip friend-test/
```

(Zip filename uses the version tag, since this artefact is what gets shared and labelled.)

`dist/` is gitignored — none of this is committed.

## 5. Commit the changelog update

If you bumped the version section before the build, commit it standalone:

```sh
git add CHANGELOG.md
git commit -m "Cut v0.1.3-suffix"
```

If you also bumped `bundleVersion` or `productName`:

```sh
git add CHANGELOG.md UnityProject/ProjectSettings/ProjectSettings.asset
git commit -m "Cut v0.1.3-suffix"
```

That's it. Share the zip out of band (Discord, AirDrop, Drive).

## Finding the version inside the headset

If you forget which version a tester is running:

1. Pull their debug log with `./scripts/pull-quest-logs.sh` (or the long-form adb command).
2. First line of any `campfirevr-log-*.jsonl` is the `app_started` event:
   ```json
   {"ts":"2026-05-16T18:52:49.805","mono":4.78,"event":"app_started",
    "product_name":"CampfireVR","version":"1.0","platform":"Android",
    "device_model":"Oculus Quest 3", ...}
   ```
3. The `version` field is `Application.version` (= `bundleVersion`). It tells you the binary version, not the patch suffix — so cross-reference with the APK filename the tester downloaded + the `CHANGELOG.md` entry whose date matches.

There's currently no in-VR version display. If you find yourself wanting one, a single-line `TutorialOverlay` footer with `$"v{Application.version} · {DateTime.Now:HH:mm}"` would be ~10 lines of code in a future slice.

## Things this process explicitly doesn't do

- **No automated release** — no GitHub release, no auto-tag, no CI build matrix. See [docs/ci-cd-quest-build-plan.md](ci-cd-quest-build-plan.md) for why (Asset Store EULA blocks cloud build today).
- **No git tags** — the CHANGELOG headings are the version record. If we ever publish to a store, we'll add `git tag v0.1.x-suffix` to step 5.
- **No release-notes-from-commits generator** — the changelog is hand-written so it stays human-readable. Conventional Commits would be overkill for a 2-person hobby project.
- **No version bump validation** — nothing checks that you actually moved entries out of `[Unreleased]` before building. If you forget, the next build will look "empty" in the changelog. Just remember to do it.
- **No store submission, no Meta App Lab pipeline** — sideload only. See [docs/install-on-quest.md](install-on-quest.md) for the install side.

## Quick reference

```sh
# 1. Update changelog (move Unreleased → new version)
# 2. Build (writes build-info.json + versioned APK + latest APK)
./scripts/build-quest.sh

# 3. Package
rm -rf dist/friend-test && mkdir -p dist/friend-test
cp UnityProject/Builds/CampfireVR-v*-*.apk         dist/friend-test/
cp docs/install-on-quest.md                        dist/friend-test/INSTALL.md
cp docs/debug-logging.md                           dist/friend-test/DEBUG-LOGS.md
cp CHANGELOG.md                                    dist/friend-test/CHANGELOG.md
cp UnityProject/Assets/Resources/build-info.json   dist/friend-test/BUILD-INFO.json
# (write/refresh dist/friend-test/README.md and optional RELEASE-NOTES.md per recipe above)
( cd dist && zip -r "CampfireVR-friend-test-vX.Y.Z-suffix.zip" friend-test/ )

# 4. Commit changelog
git add CHANGELOG.md && git commit -m "Cut vX.Y.Z-suffix"

# 5. Share the zip out of band.
```
