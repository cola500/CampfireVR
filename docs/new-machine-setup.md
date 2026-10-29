---
title: New Machine Setup
description: Checklist for recreating a working CampfireVR dev environment on a new computer from a fresh GitHub clone, including everything git does not carry.
category: workflow
status: active
last_updated: 2026-10-29
sections: [Why this exists, 1. Before leaving the old machine, 2. Clone, 3. Install tooling, 4. First Unity open, 5. Re-import Asset Store packs, 6. Local-only config, 7. Build and verify, Known gotchas]
tags: [migration, onboarding, unity, quest]
related: [install-on-quest.md, release-process.md, blender-asset-pipeline.md, ci-cd-quest-build-plan.md]
---

# New Machine Setup

## Why this exists

A fresh clone of CampfireVR does **not** open into a working scene on its own. Asset Store packs are gitignored per EULA, `CLAUDE.md` and `.mcp.json` are local-only, and the scene references a `.blend` file directly. This checklist was written during the 2026-10 migration audit; it lists every manual step found by static analysis of the repo. Nothing here has been verified on the new machine yet — tick items off and annotate surprises.

## 1. Before leaving the old machine

- [ ] `git status` is clean (or remaining changes are intentionally local), and `git log @{upstream}..` is empty on every branch you care about.
- [ ] Copy `CLAUDE.md` (repo root — untracked on purpose since `81bc304`).
- [ ] Copy `~/Development/CLAUDE.md` (the shared collaboration guide) if you want it on the new machine.
- [ ] Copy the release keystore if one exists (suggested location `~/.keystores/CampfireVR-release.keystore`, see `docs/release-keystore.md`). Never via git.
- [ ] Optional: copy Claude Code memory from `~/.claude/projects/-Users-<user>-Development-CampfireVR/memory/`. The folder name encodes the absolute repo path — rename it if the path differs on the new machine.
- [ ] Optional: copy `.claude/settings.local.json` (local permission allowlist).

## 2. Clone

```sh
mkdir -p ~/Development && cd ~/Development
git clone https://github.com/cola500/CampfireVR.git
cd CampfireVR
git switch <active-feature-branch>   # e.g. feature/campfire-reacts-to-voice
```

Git LFS is **not** used — no extra install needed. The repo is ~25 MB.

## 3. Install tooling

- [ ] **Unity Hub**, signed in with the account that owns org `johan_unity930` (Unity Cloud project `CampfireVR`, `cloudProjectId 44034960-...`). Relay + Authentication won't work under another account.
- [ ] **Unity `6000.4.7f1`** (exact — see `UnityProject/ProjectSettings/ProjectVersion.txt`), with modules:
  - Android Build Support
  - Android SDK & NDK Tools
  - OpenJDK
- [ ] Install under the default Hub path `/Applications/Unity/Hub/Editor/6000.4.7f1/` — `scripts/build-quest.sh` and its bundled `adb` path assume it. Otherwise set `UNITY_VERSION` / adjust the script.
- [ ] **Blender** (any recent version, in `/Applications/`). The scene references `Assets/Art/Dinosaur/Dino_v002.blend` directly; Unity imports `.blend` by calling Blender. Without it the BabyDino mesh silently goes missing.
- [ ] **Node.js** (for the mcp-unity server and `npx @meta-quest/hzdb`).
- [ ] **Claude Code**.

## 4. First Unity open

- [ ] Add `UnityProject/` in Unity Hub and open it. First import takes a while (Library is rebuilt from scratch).
- [ ] Packages resolve from `Packages/packages-lock.json`. `com.gamelovers.mcp-unity` is fetched from GitHub, pinned to hash `d176a9d7…`.
- [ ] Expect missing-reference warnings until step 5 is done — **don't save the scene yet**, or broken refs get serialized.

## 5. Re-import Asset Store packs

Package Manager → My Assets → download + import each. Import into the default location; never move or rename files inside them.

| Pack | Folder | Needed by scene? |
|---|---|---|
| Mountain Terrain Rocks and Tree (Jermesa) | `Mountain Terrain rocks and tree/` | Yes — rocks, tree, mountain backdrop |
| Big Oak Tree FREE + GrassFlowersFREE (ALP) | `ALP_Assets/` | Yes — oak centerpiece, meadow flowers |
| Terra (Walker C) | `Terra/` | Yes — dirt textures in our ground materials |
| Real Stars Skybox Lite | `Real Stars Skybox/` | Yes — skybox |
| Piloto Studio Campfires & Torches | `Piloto Studio/` | Yes |
| VFX PACK FIRE (WALLCOEUR) | `VFXPACK_FIRE_WALLCOEUR/` | Yes |
| Animals_FREE (ithappy) | `ithappy/` | Yes — DogCompanion |
| Nature Starter Kit 2 | `NatureStarterKit2/` | No direct refs found — skip unless something turns pink |

XRI Samples (`Assets/Samples/`) re-import automatically the first time `Tools → Quest Setup → Apply Hand Visuals` runs.

After importing:

- [ ] Wrap the `using UnityEditor;` line in `ithappy/.../CreatureMover.cs` with `#if UNITY_EDITOR ... #endif` (gitignored folder, so the fix is per-clone). Without it the IL2CPP Player build fails with `CS0246`.
- [ ] Open `Assets/Scenes/CampfireRoom.unity` and check the Console for missing scripts / missing prefabs.

## 6. Local-only config

- [ ] Put `CLAUDE.md` back in the repo root.
- [ ] Create `.mcp.json` from `.mcp.example.json`: replace `ABSOLUTE_PATH_TO_REPO` and `PACKAGE_HASH` (look under `UnityProject/Library/PackageCache/com.gamelovers.mcp-unity@<HASH>/`). Restart Claude Code.
- [ ] Start the MCP server from Unity (mcp-unity's server window under the `Tools` menu — exact path unverified) and confirm Claude Code connects.
- [ ] Quest: enable Developer Mode in the Meta Horizon app, connect over USB-C, accept "Allow USB debugging" in the headset.

## 7. Build and verify

```sh
# Close the Unity Editor first — batchmode needs the project lock.
./scripts/build-quest.sh --launch
```

- [ ] APK lands in `UnityProject/Builds/CampfireVR-remote-fika-test-v0.1.apk`.
- [ ] In headset: scene loads, fire flickers, dog and dino are visible, hands track.
- [ ] Two-player test over Relay (room A) + voice works.
- [ ] `./scripts/pull-quest-logs.sh` pulls logs.

## Known gotchas

- **`activeInputHandler` must stay `0` (legacy).** `QuestBuildAPK.ForceLegacyInputHandling()` re-asserts it on build, but if the Editor prompts to enable the new Input System on first open, say **no**.
- **Photon Voice NullReferenceException** in the Editor when the scene is dirty — pre-existing, Editor-only, ignore.
- **`ProjectSettings.asset` will drift** after the first open on a new machine. Don't bundle that drift into unrelated commits.
- **Photon AppId is committed** in `PhotonServerSettings.asset` (public repo). Same id ships in the APK, so it's low risk, but don't copy that pattern for real secrets — those go in `.env` / local files.
