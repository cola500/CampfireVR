---
title: Project rename notes
description: What was renamed from unity-mcp-lab / CampfireRoom to CampfireVR, what was intentionally kept on the old names, and why.
category: meta
status: stable
last_updated: 2026-05-16
sections:
  - The naming situation today
  - What was renamed
  - What was intentionally NOT renamed
  - Things that did NOT migrate automatically
---

# Project rename notes

This project started life as `unity-mcp-lab` — a sandbox for driving the Unity Editor through Claude Code over MCP. The first real artefact that came out of it was a cozy social-VR scene called `CampfireRoom`, which evolved into the product `CampfireVR`. The rename happened piecewise rather than as a clean event. Most layers are now `CampfireVR`; two identifiers are intentionally retained on the old names because changing them has high blast radius and low value.

This doc explains where the layers sit today and why the legacy names that remain are deliberate.

## The naming situation today

| Layer | Current name | Why |
|---|---|---|
| Product / app | `CampfireVR` | Canonical — see `PlayerSettings.productName`, headset app label, debug-log path, friend zip naming. |
| Unity company name | `CampfireVR` | Renamed from `unity-mcp-lab`. Affects `Application.persistentDataPath` on Mac Editor (Quest path is package-id-based, unaffected). |
| Unity project name | `CampfireVR` | `ProjectSettings.asset projectName` field — legacy Unity Cloud Build metadata, cosmetic. |
| GitHub repo | `cola500/CampfireVR` | Renamed from `cola500/unity-mcp-lab`. GitHub keeps a permanent HTTP redirect from the old URL, so old clones / bookmarks still work. |
| Local clone folder | `~/Development/CampfireVR/` | Renamed from `~/Development/unity-mcp-lab/`. |
| Main scene | `CampfireRoom` | **Intentionally kept.** Renaming the scene file changes a GUID-referenced asset path, which breaks `EditorBuildSettings.scenes`, the build script's `Tools/Quest Setup/Build Remote Fika APK` menu, and every doc that names the scene. Low value, high blast radius. |
| Android package id | `com.unitymcplab.campfireroom` | **Intentionally kept.** Changing this means every friend has to *uninstall* the previous CampfireVR build before installing the new one (`adb install -r` refuses to upgrade across package-id changes). Permanent: once an APK ships with a package id, that's that for the install chain. |
| Script class names (`CampfireRoom`-derived) | n/a — none exist | Always `Quest…`, `Network…`, `XR…`, etc. — never embedded the product name. |

## What was renamed

The concrete changes that landed across the rename slices:

- `PlayerSettings.companyName`: `unity-mcp-lab` → `CampfireVR` (`UnityProject/ProjectSettings/ProjectSettings.asset` line 15)
- `PlayerSettings.productName`: also `CampfireVR` (was already, just confirmed)
- `projectName` (Unity Cloud Build legacy field): `CampfireRoom` → `CampfireVR` (line 801)
- `Tools/Quest Setup/Configure Project for Quest 3` (`QuestBuildSetup.cs`): hard-coded values updated to match — re-running the menu no longer reverts to the stale names.
- `README.md` title: `# unity-mcp-lab` → `# CampfireVR`.
- `docs/debug-logging.md`: Mac Editor log path updated to `~/Library/Application Support/CampfireVR/CampfireVR/debug-logs/`.
- GitHub repo: renamed via the repo settings page (the redirect from the old URL is permanent).
- Local clone folder: renamed in `~/Development/`.
- `docs/install-on-quest.md` + `docs/ci-cd-quest-build-plan.md`: GitHub URL + `cd <folder>` instructions updated to the new name.

## What was intentionally NOT renamed

These are technical-debt-style names that are cheap to fix in isolation but expensive in blast radius:

- **`com.unitymcplab.campfireroom`** — Android package id. Renaming forces every tester to uninstall before next install; the package id is the upgrade identity on Android. Cost-benefit lands clearly on "leave it".
- **`Assets/Scenes/CampfireRoom.unity`** — main scene file. Unity references it by file path in `EditorBuildSettings.scenes`; the build script's menu hardcodes the scene name in `QuestBuildAPK.cs` and `QuestBuildSetup.cs`. Renaming requires touching all three plus every doc that references the scene by name. Marginal value (the scene is one of many things in the project, and "CampfireRoom" is still a fine name for the *location*).
- **Slice docs / CHANGELOG references to `CampfireRoom` or `remote-fika`** — these are historical: `v0.1.1-remote-fika`, `v0.1.0`'s app-icon-still-says-CampfireRoom note, etc. The slice docs are append-only — rewriting them to use the current product name would erase the timeline of how we got here. Leave them as-is.

## Things that did NOT migrate automatically

A few quality-of-life details to know about post-rename:

- **Old `~/Library/Application Support/unity-mcp-lab/CampfireVR/debug-logs/` files** stay where they are — Unity only writes new logs to the new `CampfireVR/CampfireVR/...` path. Hand-merge if you care about session continuity.
- **Quest device-side logs** (`/sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/`) are package-id-based and don't change.
- **`mcp-unity` connection** — the MCP server config in `.mcp.json` is path-based; the folder rename requires updating that path. (It's gitignored per-machine, so this is a per-clone task.)
- **Shell aliases / VS Code workspaces / Claude Code session paths** that referenced `~/Development/unity-mcp-lab/` need updating per-machine.
- **The friend zip recipe in `docs/release-process.md`** already uses `CampfireVR-friend-test-vX.Y.Z-suffix.zip` naming — no change needed there.

If a future tester reports "I can't install on top of my existing CampfireVR", that's almost certainly because we renamed the package id — which we are *not* doing. The upgrade path is preserved.
