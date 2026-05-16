# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project shape

CampfireVR is a Unity 6.4 (Built-in RP) social-VR scene for Meta Quest 3. Two players sit around a campfire and talk. The repo is authored almost entirely through Claude Code driving the Unity Editor via the `mcp-unity` MCP server. Work proceeds as thin **vertical slices** (one verifiable change per session); each slice gets its own `docs/<name>-slice.md`. There is one playable scene, `Assets/Scenes/CampfireRoom.unity`.

See README.md for the user-facing description and tech stack table, and `docs/vision.md` for the longer "why".

## Common commands

All commands run from the repo root. There is no test suite, no lint config.

```sh
# Build a Quest APK (Unity batchmode — Editor must be closed; UNITY_VERSION overridable):
./scripts/build-quest.sh
./scripts/build-quest.sh --install         # + adb install -r
./scripts/build-quest.sh --launch          # + install + monkey-launch
./scripts/build-quest.sh --install-only    # skip build, install existing APK (Editor can stay open)
./scripts/build-quest.sh --install-only --launch

# Pull debug logs from a connected Quest after a test session:
./scripts/pull-quest-logs.sh               # → quest-logs/YYYYMMDD-HHMMSS/
./scripts/pull-quest-logs.sh --zip         # also produces a zip

# Verify scripts compile without opening the Editor:
# (mcp-unity tool — only works when Unity Editor is running with the MCP server)
mcp__mcp-unity__recompile_scripts
# After creating a new .cs via MCP, follow with:
mcp__mcp-unity__execute_menu_item  menuPath="Assets/Refresh"
# Then recompile again.
```

Build output: `UnityProject/Builds/CampfireVR-remote-fika-test-v0.1.apk` (filename intentionally static — version tag lives in `CHANGELOG.md`, not the filename). Android package id: `com.unitymcplab.campfireroom`.

## Repo layout

```
UnityProject/                 # the Unity project root
  Assets/
    Scenes/CampfireRoom.unity # the only scene in build settings
    Scenes/_Archive/          # dead scenes kept for reference, not in build
    Scripts/<domain>/         # runtime C# grouped: Networking/, Voice/, XR/,
                              #   Debug/, Environment/, UI/, _Deprecated/,
                              #   Services/ (has its own asmdef)
    Editor/<domain>/          # Editor helpers grouped: Build/, Environment/,
                              #   Networking/, Verification/
    Materials/                # our own materials (warm tones to match firelight)
    Prefabs/PlayerHead.prefab # the spawned remote-avatar prefab
    Models/HandsControllerMesh.asset  # combined XRI UniversalController mesh
    Photon/                   # vendor (committed libs, Demos/ gitignored)
    XR/, XRI/, TextMesh Pro/  # auto-managed by Unity packages — do not edit
    <7 Asset Store packs>/    # ALL gitignored per EULA (see .gitignore)
    Samples/                  # XRI Samples — gitignored, re-importable via
                              #   HandVisualsSetup auto-import
  Builds/                     # gitignored APK output
  Packages/                   # UPM manifest + lock file
  ProjectSettings/

scripts/                      # shell helpers, bash 3.2 compatible (macOS default)
docs/                         # one .md per slice + index docs:
                              #   roadmap, retro-log, app-alignment-qa,
                              #   vision, release-process, debug-logging,
                              #   ci-cd-quest-build-plan, install-on-quest,
                              #   remote-fika-test (+ debug-checklist)
CHANGELOG.md                  # versioned milestones (v0.1.0, v0.1.1-remote-fika, ...)
README.md                     # user-facing intro + setup
dist/                         # gitignored friend-test packaging (cp + zip)
quest-logs/                   # gitignored adb pull output
```

## Architecture (the parts that span files)

**Scene hierarchy** (post-cleanup; see `docs/project-structure-cleanup.md` for the move):

```
CampfireRoom
├── World/
│   ├── Ground, Atmosphere, Directional Light
│   ├── PlayerSlot_A/B, EyeHeightMarker_A, RemoteRig
│   ├── TutorialPanel (TextMesh + TutorialOverlay)
│   ├── Main Camera (disabled — flat-screen fallback)
│   ├── Campfire/{SM_campfire_001, Log_1/2, Flame, FireLight,
│   │             FireCrackleAudio, Embers, FireStones, FirePitKerb/}
│   ├── Environment/Forest/{Trees, Rocks, Mountains}
│   ├── Environment/Grass/GrassBreakup/{6 GrassTuft_* with cross-quad cards}
│   ├── Seats/{Seat_A/B (functional, mesh disabled), StoneSeat_A/B + variants}
│   └── Companions/DogCompanion
├── VRRig (1.6, 0, 0, rot Y=270°) — local XR rig
│   └── CameraOffset (0, 1.2, 0) ← seated eye height (Quest Floor origin alone wasn't enough)
│       ├── VRCamera         (XRHeadTracker node=CenterEye)
│       ├── LeftHandAnchor   (XRHeadTracker LeftHand + XRControllerInputFeedback)
│       │   └── LeftHandMesh (combined controller mesh, +45° pitch)
│       └── RightHandAnchor  (same)
├── NetworkManager   (Unity.Netcode + UnityTransport)
└── NetworkBootstrap (host/client state machine, controller input, debug overlay)
```

`Forest` at scene root is an orphan empty GameObject from a pre-cleanup slice — safe to delete in a follow-up.

**Networking model.** NGO + UnityTransport for transport. Owner-authoritative `ClientNetworkTransform` syncs each player's `PlayerHead` prefab transform (head). Hand poses ride on four `NetworkVariable<Vector3>`/`<Quaternion>` per player. Owner hides its own visual (the user sees the world through `VRCamera`, not their own avatar). Remote head/hands are positioned by `NetworkHead.cs` using a `_rotDiff` mirror computed once at spawn from `VRRig` → `RemoteRig` — so head pose is in seat-relative coordinates, friends always appear opposite each other regardless of which physical room they're in.

Two transport modes via `NetworkBootstrap.Mode`:
- `Lan` ("Same Wi-Fi") — direct UDP, `serverAddress` baked into the scene. Dev-only.
- `Relay` ("Internet") — Unity Relay (free tier) + Photon room property `rc` to broker the allocation join code.

Room code is a single letter A–Z (default A), cycled via right thumbstick. Both host and join read `_codeChars[0]`. Voice room name is the same letter.

**Voice** is Photon Voice 2 on a separate cloud connection. `VoiceBootstrap` connects to Photon master at startup, then `JoinRoom(letter)` after host/join succeeds. `VoiceSpeakerPlacer` reparents each incoming Speaker under `RemoteRig` at eye height for spatial-audio illusion.

**Custom XR (no XRI/Input System runtime).** `XRHeadTracker` reads `InputDevices.GetDeviceAtXRNode(...)` and writes pose to its Transform each `LateUpdate`. XRI 3.5.0 is installed only for its `UniversalController.fbx` mesh (combined into `Assets/Models/HandsControllerMesh.asset` for both local and remote hands). `activeInputHandler` **must** stay at `0` (legacy) — XRI's transitive Input System dep flips it to "Both" which breaks Android builds; `QuestBuildAPK.ForceLegacyInputHandling()` re-asserts this on every build via SerializedObject.

**Debug logging.** `DebugLogger` (`Scripts/Debug/`) is a `RuntimeInitializeOnLoadMethod`-bootstrapped singleton writing JSONL events to `Application.persistentDataPath/debug-logs/`. Auto-flush on every event, 5 MB file rotation, 10-file retention. `NetworkBootstrap` and `VoiceBootstrap` are instrumented at every state transition (mode change, room change, host/join attempts, Relay alloc, voice state). Pull with `scripts/pull-quest-logs.sh`; schema and triage recipes in `docs/debug-logging.md`.

## MCP / Unity workflow gotchas

(These come from `README.md` "MCP workflow" plus the slice docs — listed here because they affect day-to-day tool selection.)

- **After creating a new `.cs` file via MCP**, call `Assets/Refresh` menu before `recompile_scripts` — Unity caches the source file list and won't see new files otherwise. The same goes for newly-added menus: the menu cache doesn't always rebuild on first compile. If `execute_menu_item` fails with "no menu named ...", run `Assets/Refresh` + recompile + retry.
- **`mcp__mcp-unity__update_component` silently drops `UnityEngine.Object` references** assigned through JSON `componentData` (ends up with default-material, missing-prefab, etc). Workaround: assign Object refs from a re-runnable Editor helper (`Tools → Quest Setup → ...` menus under `Assets/Editor/Environment/`) using `AssetDatabase.LoadAssetAtPath<T>` + `EditorUtility.SetDirty`. Enum / int / float / string fields go through `update_component` fine.
- **Re-runnable Editor menus over one-shot MCP calls.** Anything that touches multiple components / assets / scene hierarchy should be a `[MenuItem("Tools/Quest Setup/...")]` helper, idempotent, with its own log line. Pattern examples: `QuestBuildAPK`, `ForestSetup`, `GroundStones`, `HandVisualsSetup`, `RemoteAvatarPolish`, `SceneHierarchyOrganize`. They survive Editor restarts and Library wipes.
- **`get_gameobject` output blows past token limits** for objects with many children (`World` post-cleanup, `DogCompanion` with its skinned rig). Use `Bash` `grep` on the scene YAML or query specific child paths instead.
- **The Editor's project lock conflicts with batchmode builds.** `./scripts/build-quest.sh` will fail if the GUI is open with `CampfireRoom`. Close it first, or use `--install-only` which skips the build.
- **Photon Voice 2 fires a `NullReferenceException`** in `VoiceConnection.Update()` / `OnDestroy()` whenever the scene is marked dirty in Editor. Pre-existing third-party bug, Editor-only, does not affect builds. Ignore.

## Documentation conventions

Slice work always lands a doc. Two patterns:

- **Per-slice docs** `docs/<theme>-slice.md` — what we did, why, what we measured, what we deliberately *didn't* do. Reads like a small lab notebook entry, ~150–250 lines. Examples: `dog-companion-slice.md`, `vr-alignment-polish.md`, `session-recovery-slice.md`. New slice = new file.
- **Index/audit docs** that get updated across slices:
  - `docs/roadmap.md` — done / next / deferred slice list (live)
  - `docs/app-alignment-qa.md` — code-vs-docs drift audit (annotate with `[updated: <slice name>]` and strikethrough when fixed, don't rewrite history)
  - `docs/retro-log.md` — lessons learned, project retrospectives
  - `CHANGELOG.md` — moves entries from `[Unreleased]` into a new `[v0.x.y-suffix]` section when a build is shared. Versioning approach in `docs/release-process.md`.

When a slice introduces a new helper/script/material, list it in the slice doc with `path`, why it exists, and how to invoke. The user reads these — they're not internal notes.

## Asset Store / vendor folders

Seven Asset Store packs are gitignored per EULA: `Real Stars Skybox/`, `Piloto Studio/`, `VFXPACK_FIRE_WALLCOEUR/`, `NatureStarterKit2/`, `Mountain Terrain rocks and tree/`, `Terra/`, `ithappy/` (Animals_FREE). Plus `Photon/*/Demos/` and `Samples/` (XRI). The committed scene references these by GUID; a fresh clone must re-import each pack from Package Manager → My Assets before the scene resolves cleanly.

**Never** move/rename files inside vendor folders. Never commit a vendor pack's source. When a slice needs a new asset, create our own minimal Standard-shader material in `Assets/Materials/` and assign via Editor helper (e.g. `DogCoat.mat`, `RemotePlayer.mat`) — never try to bend a pack's shipped shader through `Render_Pipeline_Convert.unitypackage`-style mass conversions.

If a vendor MonoBehaviour has `using UnityEditor;` at the top (e.g. `ithappy/CreatureMover.cs`) it will fail IL2CPP Player builds. Either wrap in `#if UNITY_EDITOR ... #endif` locally (gitignored, so the fix is per-clone), or strip the script from the scene via Editor helper before build (the `RemoteAvatarPolish` pattern for the Dog prefab is the reference).

## Working style

The user is hands-on and reads diffs. Default to: **propose → implement → validate → show diff/status → wait for explicit "commit"**. Don't auto-commit. When the user does ask for a commit, they often specify the exact file set + commit message; honour it literally and don't bundle in carryover from earlier slices (`UnityProject/ProjectSettings/ProjectSettings.asset` drifts often and rarely belongs in the current slice's commit).

Prefer **multiple small commits over one big commit** when a slice naturally splits (e.g. "input handling carryover" + "VR polish" rather than one). The user has asked for this split twice unprompted.

Class names, namespaces, and `[SerializeField]` field names are part of the scene's serialized binding. Renaming breaks scene refs even when `git mv` preserves the `.meta` GUID. Don't rename unless asked.

`scripts/` are bash 3.2 compatible (macOS default). Don't use `mapfile`, `[[ -v var ]]`, or other bash 4+ idioms — use the `while IFS= read -r` pattern instead.
