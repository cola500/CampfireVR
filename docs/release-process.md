# Release process

How to cut a new test build and ship it to a friend. Five steps, ~10 minutes if nothing breaks.

Tone: this is a one-person indie project. No CI, no release governance, no release notes review board. Just disciplined notes so future-me can reproduce what past-me did.

The five-step flow further down is for **release builds** — versioned, packaged, sent to a friend. For day-to-day "tweak scene → headset to validate" iteration use the workflow in the next section, which keeps the Editor open and skips changelog ceremony.

## Fast iteration workflow

Optimised for "tiny VR tweak → deploy to headset fast." No Editor close, no changelog bump, no friend package.

### One-click (recommended)

With the Unity Editor open and a Quest connected via USB-C:

```
Tools → Quest Setup → Build + Deploy to Quest
```

That menu (added in `Assets/Editor/Build/QuestBuildAPK.cs`) runs `BuildPipeline.BuildPlayer` inside the Editor process — no batchmode lock conflict because the build happens in the same Unity instance that holds the project lock — then shells out to `scripts/build-quest.sh --install-only --launch` to push the APK to the headset and start it.

Total time on a warm build: ~30–60 s from menu click to seeing the scene in the Quest. Install output lands in the Editor's Console.

Caveats this path accepts in exchange for speed:
- APK is written at the static `Builds/CampfireVR-remote-fika-test-v0.1.apk` filename, not a versioned `CampfireVR-<version>-<YYYYMMDD-HHMM>.apk`. Fine for iteration; not what you want to share with a friend.
- `Assets/Resources/build-info.json` is NOT regenerated (only the terminal script does that). The APK will report whichever commit / dirty flag was baked in by the last script-side build. For "I want the headset log to show the exact commit I'm testing", run the script instead.

### Two-step (when you want the build inside Editor but install separately)

1. **Build inside Unity** — either:
   - `Tools → Quest Setup → Build Remote Fika APK` (menu we already had), OR
   - Standard `Cmd+B` "Build And Run" with Run Device set to the connected Quest (Unity does the install itself in this case — even faster, but you don't go through the script's adb path).
2. **In a terminal (Editor stays open):**
   ```sh
   ./scripts/build-quest.sh --install-only --launch
   ```
   `--install-only` does NOT invoke Unity; the project lock is irrelevant. The script auto-detects the newest `CampfireVR-*.apk` in `Builds/` by mtime, so it picks up whatever the Editor just wrote without you specifying a filename.

If you want to see what's currently in `Builds/` before installing:

```sh
./scripts/build-quest.sh --list
```

Sorted newest-first with size + mtime so you can tell at a glance which APK `--install-only` would pick.

### Why batchmode requires the Editor closed (full builds only)

`./scripts/build-quest.sh` without `--install-only` launches Unity in batchmode, which tries to open `UnityProject/` as a fresh project instance. Unity enforces single-writer access to a project's `Library/` folder via a lock file — if the Editor GUI is already holding that lock, batchmode bails. There's no way around this without a second copy of the Editor opening a copy of the project. That's why the iteration workflow above puts the build inside the open Editor instead of shelling out — same process, same lock, no conflict.

`--install-only` and `--list` don't invoke Unity at all (just adb + filesystem reads) so they're completely safe with the Editor open.

### Build hook: PreloadedAssetsGuard

Unity 6 + XR Plugin Management empties `PlayerSettings.preloadedAssets` during the Quest build, which would leave `ProjectSettings.asset` dirty after every build and risk shipping a future APK without the XR subsystem initialised at startup (= black 2D-rendered scene on the headset).

`Assets/Editor/Build/PreloadedAssetsGuard.cs` implements `IPostprocessBuildWithReport` to re-add the two required entries (Android `XRGeneralSettings` sub-asset + `OculusSettings.asset`) and `AssetDatabase.SaveAssets()` after every build. No configuration — runs automatically on both batchmode and Editor-driven builds. Look for `[PreloadedAssetsGuard] post-build: restored …` in the build log; if you ever see `git status` flag `ProjectSettings.asset` as dirty after a build, that hook is missing or didn't execute.

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

The APK manifest declares `targetSdkVersion=34` (Android 14) and `minSdkVersion=29` — pinned explicitly in `QuestBuildSetup.cs` rather than left on `AndroidApiLevelAuto`, so the value can't silently drift across machines that have different Android SDK platforms installed. Required for Meta Horizon Store / App Lab submissions since 2026-03-01. Verify post-build with `aapt2 dump badging Builds/CampfireVR-latest.apk | grep targetSdk`.

### Build metadata baked into every APK

Before Unity batchmode runs, the script writes `UnityProject/Assets/Resources/build-info.json` capturing the exact identity of this build:

```json
{
  "version":          "v0.1.3-suffix",
  "versionCode":      103,
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

`DebugLogger` reads this at app startup via `Resources.Load<TextAsset>("build-info")` and stamps the session log's `app_started` event with `build_version`, `build_version_code`, `build_time`, `git_commit`, `git_branch`, `git_dirty`, `apk_name`. See [debug-logging.md](debug-logging.md#build-identity-in-app_started) for the exact fields.

#### Version-identity chain

A single build's identity propagates through five places that must all agree. When a tester reports a bug, any one of these is enough to recover the exact source tree they ran:

| Layer | Value | Where it lives |
|---|---|---|
| CHANGELOG heading | `v0.1.3-suffix` | `CHANGELOG.md`'s first `## [v…]` heading |
| APK filename | `CampfireVR-v0.1.3-suffix-20260516-2027.apk` | `UnityProject/Builds/` (immutable, never overwritten) |
| `bundleVersion` string | `1.0` | `ProjectSettings.asset` — only bumped for major rebrands |
| `bundleVersionCode` integer | `103` | Computed per-build by `scripts/build-quest.sh` as `git rev-list --count HEAD`. Applied to `PlayerSettings.Android.bundleVersionCode` in memory by `VersionCodeGuard.cs` (pre-build) and restored to the committed baseline (1) post-build so `ProjectSettings.asset` on disk never drifts. Strictly-increasing — Meta requires this for Store uploads. |
| `build-info.json` | all of the above + git SHA + dirty flag | `UnityProject/Assets/Resources/build-info.json` (gitignored, regenerated per build, included in the APK as a TextAsset) |
| Debug log `app_started` event | same fields under `build_version`, `build_version_code`, `git_commit`, etc. | `/sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/*.jsonl` on the headset |

The friend zip's `BUILD-INFO.json` is byte-identical to the in-APK copy, and `scripts/package-friend-test.sh` surfaces both `version` and `versionCode` in the README + RELEASE-NOTES, so the tester can quote either in a bug report.

The file is **gitignored** (`UnityProject/Assets/Resources/build-info.json` + `.meta`) so per-build timestamps and git SHAs don't pollute commit history. Regenerated fresh on every `./scripts/build-quest.sh` invocation; safe to delete locally — the next build recreates it.

If the script runs against a dirty working tree, it prints a `⚠ Repo is dirty — uncommitted changes are baked into this APK.` warning to stderr and sets `"gitDirty": true`. Useful trust signal when a tester reports a problem on a build that didn't come from a clean commit.

To deploy directly to a USB-connected Quest:

```sh
./scripts/build-quest.sh --install-only --launch                 # install CampfireVR-latest.apk
./scripts/build-quest.sh --apk Builds/CampfireVR-v0.1.2-...apk --install   # roll back to an older versioned build
```

## 4. Create the friend package

One command:

```sh
./scripts/package-friend-test.sh
```

The script reads `UnityProject/Assets/Resources/build-info.json` (step 3 wrote it), picks the APK named in that file, and produces:

- `dist/friend-test/` — temporary staging folder with the unpacked contents (APK + INSTALL.md + DEBUG-LOGS.md + CHANGELOG.md + BUILD-INFO.json + generated RELEASE-NOTES.md + README.md + SHA256SUMS). The script recreates it from scratch every run. **This folder is not the release artefact — never share it directly.**
- `dist/CampfireVR-friend-test-<version>-<YYYYMMDD-HHMM>.zip` — the release artefact. Filename pulls the version + build stamp from `BUILD-INFO.json`, so the zip name maps 1:1 to the exact APK it contains.
- `dist/CampfireVR-friend-test-<version>-<YYYYMMDD-HHMM>.zip.sha256` — sidecar checksum of the zip, for download-integrity verification.

The script enforces a few invariants without prompting:

- **No silent overwrites.** If the target zip already exists (same version + stamp), the script exits 3 with `Zip already exists and is immutable — re-run with --force`. The matching APK and zip share an identity; replacing one without the other would invalidate every BUILD-INFO + SHA256 trail you've recorded so far. Use `--force` only when you're consciously re-cutting the same build (e.g. corrected README + same APK).
- **No dirty builds by default.** If `BUILD-INFO.json` says `"gitDirty": true`, the script exits 4 with `Either rebuild from a clean tree, or pass --allow-dirty`. A dirty build can't be reproduced from the recorded commit, so bug reports against it are ambiguous. Pass `--allow-dirty` only for ad-hoc internal tests, never for shared builds.
- **SHA256 by default.** `SHA256SUMS` inside the zip lets the tester run `shasum -a 256 -c SHA256SUMS` to confirm the APK survived the trip; the `.zip.sha256` sidecar lets them confirm the zip itself before unpacking. Skip with `--no-sha256` if you have a reason (you usually don't).

For repackaging an older versioned build (e.g. the friend needs an older APK because a new one regressed), pass `--apk` explicitly:

```sh
./scripts/package-friend-test.sh --apk UnityProject/Builds/CampfireVR-v0.1.1-remote-fika-20260514-2138.apk
```

This still uses the current `BUILD-INFO.json` for metadata — so make sure to also keep around (or regenerate from a commit checkout) the matching build-info if you want the RELEASE-NOTES + git stamp to match what the tester is actually running.

`dist/` is gitignored — none of this is committed.

### Artefact policy at a glance

| Path | Immutable? | What to share |
|---|---|---|
| `UnityProject/Builds/CampfireVR-v<ver>-<stamp>.apk` | **Yes** — written once, never overwritten. | Standalone if a friend just wants the APK (rare; the zip is the usual unit). |
| `UnityProject/Builds/CampfireVR-latest.apk` | **No** — overwritten on every build. | Never. It's a convenience pointer for `--install-only`. |
| `dist/friend-test/` | **No** — recreated every package run. | Never. Staging only. |
| `dist/CampfireVR-friend-test-<ver>-<stamp>.zip` | **Yes** — script refuses to overwrite without `--force`. | Yes. This is the release artefact. |
| `dist/...zip.sha256` | **Yes** — derived from the zip. | Optional — useful for testers who care about download integrity. |

### How to identify what a tester installed

When a bug report comes in, three places agree on the build identity:

1. The **zip filename** they downloaded — `CampfireVR-friend-test-v<ver>-<stamp>.zip` — encodes version + build minute.
2. The **APK filename** inside the zip — `CampfireVR-v<ver>-<stamp>.apk` — same stamp.
3. The **`build-info` event** in their `debug-logs/*.jsonl` — same fields again, plus the git commit SHA. `DebugLogger` reads `Assets/Resources/build-info.json` at app startup.

Any one of those three is enough to `git checkout` the exact source tree the tester is running. The SHA256SUMS file lets you confirm bit-for-bit that the APK they have matches the APK you packaged.

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
