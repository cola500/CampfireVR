---
name: friend-package
description: Build a fresh CampfireVR Quest APK and produce a traceable, immutable friend test zip from the latest commit. Handles Unity batchmode failure modes (stale lockfile, dirty tree, ProjectSettings Cloud Build drift) defensively so the resulting zip cleanly maps to a single committed source state.
---

# /friend-package

Cuts a new CampfireVR friend test zip from the current `main`. The whole flow is defensive about Unity batchmode's failure modes — most of which we've hit at least once and shouldn't hit twice.

Invoke when the user says any of:

- "bygg en ny friend package", "ny friend test build", "cut a friend build"
- "package for testing", "release for friend", "send to tester"
- "build + package for handoff"

## What this skill produces

- `UnityProject/Builds/CampfireVR-v<version>-<YYYYMMDD-HHMM>.apk` — immutable versioned APK (kept forever)
- `UnityProject/Builds/CampfireVR-latest.apk` — pointer copy (overwritten each build, never shared directly)
- `dist/CampfireVR-friend-test-v<version>-<YYYYMMDD-HHMM>.zip` — the release artefact
- `dist/CampfireVR-friend-test-v<version>-<YYYYMMDD-HHMM>.zip.sha256` — sidecar checksum

`dist/` is gitignored. None of this gets committed.

## Phase 1 — Assess working tree

Always start with `git status --short`. Classify what's there:

| Pattern | What it is | Action |
|---|---|---|
| Only `M UnityProject/ProjectSettings/ProjectSettings.asset` showing `projectName: CampfireVR → CampfireRoom` | Recurring Unity Cloud Build sync drift | Run `git checkout -- UnityProject/ProjectSettings/ProjectSettings.asset` — note in the final report. The fix is in commit `162d950` (cloud relink); residual drift may still appear until Editor is re-opened fresh. |
| `M UnityProject/ProjectSettings/ProjectSettings.asset` showing `AndroidBundleVersionCode` non-1 | `VersionCodeGuard` failed to restore (build was interrupted between pre- and post-hooks) | Revert with `git checkout`. Don't include in any commit — baseline stays at 1 by design (see `docs/release-process.md` "Version-identity chain"). |
| `M UnityProject/ProjectSettings/ProjectSettings.asset` showing `preloadedAssets: []` | `PreloadedAssetsGuard` regressed | Same — revert. If it persists across multiple builds, that guard is broken and needs investigation. |
| `?? UnityProject/ProjectSettings/Packages/*` | Unity Services or AI Assistant initialised a per-package settings dir | If `com.unity.services.core` → probably legitimate (a relink event). If `com.unity.ai.assistant` or similar → user toggled AI tools by mistake — see the AI Assistant incident handling. |
| `M CHANGELOG.md` with an entry for in-flight slice work | Documentation catching up to a committed slice | Commit + push separately *before* the build so build-info.json's changelogSummary surfaces the new bullet. Use a focused commit message describing the catch-up. |
| `M` other source files | Genuine in-flight work | **Ask the user** whether to commit first or build a `gitDirty: true` package. Default: ask. |
| `??` untracked files (any path) | New work not yet added to git | **No action needed for the build.** `gitDirty` is computed from `git diff` + `git diff --cached` only — untracked files don't pollute build-info. Mention them in the final report if they're surprising, but never auto-stage or auto-delete. |

The goal: enter Phase 3 with `git status --short` clean (or only drift we deliberately accept, or only untracked entries we accept).

## Phase 2 — Pre-build hygiene

Check Unity Editor state. Editor must be CLOSED for batchmode to acquire the project lock:

```sh
pgrep -lf "Unity.app/Contents/MacOS/Unity\b" 2>&1 | grep -v Licensing | head -3
```

If the result is non-empty: ask the user to close Unity Editor and wait.
If the result is empty: check for a stale lockfile from a previous crashed Editor:

```sh
ls UnityProject/Temp/UnityLockfile 2>/dev/null
```

If present and no Editor process is running, the lockfile is stale — remove it:

```sh
rm -f UnityProject/Temp/UnityLockfile
```

`Temp/` is gitignored; the lockfile is safe to delete when no Unity instance is actually running.

## Phase 3 — Build

Run the build:

```sh
./scripts/build-quest.sh
```

**Critical gotcha — never pipe build output through `head -N`.** Unity batchmode running under a shell pipe gets SIGPIPE'd when `head` closes the pipe, killing the build halfway through. This has happened in real sessions. Acceptable patterns:

- Pipe through `tail -N` (reads the full stream then prints last N lines)
- Pipe through a file: `... 2>&1 | tee /tmp/build.log`, then inspect the file
- No pipe at all — just let stdout flow

If a build runs and exits with `Aborting batchmode due to fatal error: It looks like another Unity instance is running`, the lockfile is stale — go back to Phase 2 cleanup and retry.

A successful build prints `[QuestBuildAPK] OK · <size> MB · <time> · <abs path>` and a `[build] OK · <size> · <rel path>` line at the end.

## Phase 4 — Package

```sh
./scripts/package-friend-test.sh
```

The package script's behaviour to know:

- Reads `UnityProject/Assets/Resources/build-info.json` to find the APK
- Refuses to overwrite an existing zip with the same `<version>-<stamp>` filename (exit 3) — use `--force` only when intentionally re-cutting the same build
- Refuses dirty builds by default (exit 4) — Phase 1 should have left us clean. If we deliberately want a dirty package, pass `--allow-dirty`
- Generates SHA256 for both the APK (inside the zip as `SHA256SUMS`) and the zip itself (sidecar `.zip.sha256`)

### Re-packaging the same commit

The `<stamp>` portion of the zip filename comes from the *build timestamp* (minute precision), not the commit. Re-packaging the same commit on a later day gets a fresh `<stamp>` slot and no collision — `--force` is not needed unless you re-run within the same minute. This is the normal path for "the build is the same, but I want a new package to send out today."

## Phase 5 — Verify

After the package script reports `[package] OK`, sanity-check:

- The zip exists at `dist/CampfireVR-friend-test-v<version>-<stamp>.zip` and is non-zero
- `unzip -l <zip>` shows 9 files: APK, README.md, INSTALL.md, DEBUG-LOGS.md, CHANGELOG.md, BUILD-INFO.json, RELEASE-NOTES.md, SHA256SUMS, plus the `friend-test/` directory entry
- The APK inside is non-zero (typically ~117 MB at the time of writing)
- `BUILD-INFO.json` shows `gitDirty: false` and a recognisable `gitCommit` (top of `git log`)
- `git status --short` is clean

## Phase 6 — Report

Produce a tight summary table for the user:

| Field | Value |
|---|---|
| Zip filename | `dist/CampfireVR-friend-test-v<version>-<stamp>.zip` |
| Zip size | `<MB>` |
| APK filename | `CampfireVR-v<version>-<stamp>.apk` |
| APK size | `<MB>` (`<bytes>` bytes — non-zero) |
| Build version | `<version>` (versionCode `<N>`) |
| Git commit | `<short SHA>` on `main`, `gitDirty: <true/false>` |
| APK SHA256 | first 16 chars + `…` (full value is in the zip's SHA256SUMS) |
| Zip SHA256 | first 16 chars + `…` (sidecar file: `<zip>.sha256`) |
| `git status --short` | `clean` or list any remaining drift |

If anything was reverted in Phase 1 (typically ProjectSettings drift), include a one-liner noting it: "Reverted ProjectSettings Cloud Build sync drift before build — drift will return next Editor open; expected per `162d950`."

If a CHANGELOG entry was committed in Phase 1, mention the commit SHA so the user can trace it.

## What this skill deliberately doesn't do

- **No commits to `dist/`** — gitignored on purpose
- **No automated `git push`** after Phase 1 commits — ask the user before pushing
- **No headset install** — the resulting zip is for handoff; `./scripts/build-quest.sh --install-only --launch` is a separate workflow
- **No assumption about which slice is being shipped** — the build picks up whatever's on `main`. If the user wants a specific older build packaged, they should pass `--apk PATH` to `package-friend-test.sh`
- **No release-keystore signing checks** — `ReleaseSigningGuard` will warn if env vars aren't set, but the friend-test build path is debug-signed by design (Meta submission is a separate flow)

## Known regressions worth catching early

- `ProjectSettings/Packages/com.unity.ai.assistant/` appearing untracked → user enabled Unity AI Assistant; review the `Packages/manifest.json` diff before building (revert AI packages if unintended, see commit `162d950` neighbours for the playbook)
- `activeInputHandler: 2` (Both) in `ProjectSettings.asset:797` while `CLAUDE.md` says it must be 0 → known open question, doesn't block the build but flag in the report

## Reference

- `docs/release-process.md` — full version-identity chain, friend-package philosophy
- `docs/post-sprint-backlog.md` — list of headset-validation tasks the friend build supports
- `scripts/build-quest.sh` — APK production script + version computation
- `scripts/package-friend-test.sh` — zip + SHA256 + dirty-tree guard
