# Changelog

Notable user-visible / tester-visible changes to CampfireVR.

Format loosely follows [Keep a Changelog](https://keepachangelog.com/).
Tone is "indie dev's notes for the friend testing the build" — not release ceremony.

## [Unreleased]

(Things merged to `main` after the most recent published test build go here. Move them under a new version section when the next test build is cut.)

### Added
### Changed
### Fixed

## [v0.1.2-session-fix] — 2026-05-16 evening

Recovery, ergonomics, and testing tooling on top of the v0.1.1 environment. Built and shared as `CampfireVR-remote-fika-test-v0.1.apk` on 2026-05-16 ~18:30.

### Added
- **Dog companion** beside `StoneSeat_A` — static, no AI, no audio. Uses ithappy `Animals_FREE` atlas texture (`docs/dog-companion-slice.md`).
- **Build / deploy scripts** — `scripts/build-quest.sh` wraps Unity batchmode; supports `--install`, `--launch`, `--install-only`. No need to open the Editor for iterations (`docs/ci-cd-quest-build-plan.md`).
- **Debug logging system** — `DebugLogger` writes timestamped JSONL events to `persistentDataPath/debug-logs/`. Hooks: app start, mode/room change, host/join attempts, Relay alloc, voice state, NGO connect/disconnect, Unity errors, manual marker (Editor `L` key). Pull via `adb pull` after a session (`docs/debug-logging.md`).
- **Session recovery** — long-press left **Y** for ~1.5 s = in-VR `Stop()`. Short tap Y still toggles mode. Stop teardown is try/catch-wrapped so a partial failure doesn't block the rest. Room letter + mode preserved across stop (`docs/session-recovery-slice.md`).
- **Join-while-hosting guard** — pressing **B** while already hosting now shows "Already hosting Room X" instead of throwing `SessionConflict` from Unity Services. One log event per press, not per frame.
- **Quest install guide for friends** — step-by-step `docs/install-on-quest.md`, plus a packaged `dist/friend-test/` with APK + INSTALL.md + DEBUG-LOGS.md + README.md zipped for sharing.
- **`Polish Remote Avatar`** Editor helper — replaces the white sphere head + cube hands with a warm tan head + the same combined Quest Touch controller mesh local players see (`docs/remote-avatar-sanity.md`).
- **`Ground Stones`** Editor helper with deterministic per-name varied embed depth (seats 5–8 cm, rocks 6–12 cm).
- **`Organize Scene Hierarchy`** Editor helper — groups loose env roots under `World/Campfire`, `World/Environment/Forest/{Trees,Rocks,Mountains}`, `World/Seats`, `World/Companions`. 74 scene roots → 5.

### Changed
- **Room code simplified** — 3-letter ABC (27 codes) → single letter A–Z (26 rooms). Default `A`. Right thumbstick cycles letter. No edit-mode, no slot picker. Both host and join read the same letter (`docs/verification/join-flow.md`).
- **Hand visuals** — pitch +45° on `LeftHandMesh`/`RightHandMesh` so the controller mesh angles down naturally from the tracked pointer pose.
- **Project structure** — `Scripts/` and `Editor/` flat folders organised into Networking / Voice / XR / Debug / Environment / UI / _Deprecated and Build / Environment / Networking / Verification respectively. GUIDs preserved via `git mv` (`docs/project-structure-cleanup.md`).
- **Quest player settings** — `productName` rebrand to `CampfireVR`, FireLight shadows Soft → Hard, Directional Light shadows Soft → None (`docs/quest-validation-pass.md`).
- **Stop event vocabulary** — `stop_pressed` → `stop_requested`, `stopped` → `stop_completed`; plus `stop_step_failed`, `stop_completed_with_errors`, `join_ignored_already_hosting`, `join_ignored_already_in_session`.

### Fixed
- `MicrophoneTest` GameObject removed from scene (script kept under `Scripts/_Deprecated/`).
- TutorialPanel `shadowCastingMode` switched to `Off` — UI panels shouldn't cast contact shadows.
- Active Input Handling forced to legacy at every build (`ForceLegacyInputHandling` in `QuestBuildAPK.cs`) so XRI's transitive Input System dep doesn't break Android builds.
- Magenta dog material — pack ships a URP shader that falls back to magenta in Built-In RP; replaced with our own Standard material that samples the pack's shared atlas texture.

## [v0.1.1-remote-fika] — 2026-05-16 morning

Environment polish for the first headset-validated remote fika test. APK shared as `CampfireVR-remote-fika-test-v0.1.apk` (same filename as v0.1.0; the `v0.1.1-remote-fika` tag exists only in this changelog and in the bundled docs).

### Added
- **Forest atmosphere** — 6 trees + 4 fire-pit kerb stones around the campfire. Manual placements + `ForestSetup` Editor helper for re-runnable shadow / scale config.
- **Dirt ground, mountain backdrop, stone seats** — Terra dirt material applied to `Ground` via `ForestFloorSetup`. Mountain Terrain rocks used for seats + perimeter mountains.
- **Expanded forest** — ~52 additional manual placements (trees, rocks, mountains). All routed through `ForestSetup` so Quest-perf safety (shadows off, etc) is preserved.
- **Subtle tree wind** — 5 trees closest to the fire get a barely-perceptible per-tree sway via `SubtleTreeWind`.
- **Bark texture on wood pile** — Mountain Terrain bark texture + normalmap on the Piloto campfire mesh, matching the surrounding trees.
- **Sparse grass breakup** — 6 cross-quad grass tufts as ground detail.
- **VFX_Fire flame** — WALLCOEUR VFX URP Fire pack integrated as the main campfire flame.
- **App alignment QA report** — full audit of code-vs-docs drift at the time of the build (`docs/app-alignment-qa.md`).

### Changed
- **Campfire wood pile** — Piloto Studio mesh replaces the original capsule logs. Standard shader since the pack ships HDRP.

### Fixed
- `VoiceBootstrap.OnGUI` gated behind `Application.isEditor` so the debug overlay no longer leaks into Quest builds.

## [v0.1.0] — 2026-05-15 evening

Initial shareable remote-fika test build. Two Quests can host + join + talk over a campfire. `CampfireVR-remote-fika-test-v0.1.apk`.

### Added
- Seated VR campfire scene — ground, logs, flickering flame, point-light glow, dark navy ambient, starfield skybox, Photon Voice ambient crackle.
- Quest 3 build pipeline — Built-in RP, OpenXR Oculus loader, IL2CPP ARM64, IL2CPP-friendly scene.
- HMD pose tracking via custom `XRHeadTracker` against `XRNode.CenterEye` (no XRI).
- Hand presence via tracked Quest controllers — primitive placeholders driven by `XRNode.LeftHand` / `RightHand` plus trigger-press scale feedback.
- LAN multiplayer over Netcode for GameObjects + UnityTransport — owner-authoritative head/hand sync via `ClientNetworkTransform` and `NetworkVariable`.
- Unity Relay + Photon Voice — `ServicesBootstrap` for auth + Relay allocation, `VoiceBootstrap` for spatial voice. Relay code shared via Photon room property.
- ABC 3-letter join code with world-space slot picker (thumbstick + A/X buttons). 27 possible codes.
- Quest-native tutorial overlay (`TutorialOverlay`) with per-phase legend.
- Remote head + hand sync — `NetworkHead` with `RemoteRig` mirror math so seated players appear opposite each other regardless of physical room layout.
- `PlayerSlot_B` static placeholder that hides when a real friend connects.

### Known issues at this version
- App icon still reads "CampfireRoom" (Unity productName not yet renamed).
- No in-VR Stop button — must quit via Meta system menu.
- Remote head is a flat-white sphere + two white cubes for hands.
- LAN mode requires a baked `serverAddress` and is effectively dev-only.

## Versioning approach

Small indie VR project, manual notes, no release pipeline.

- **`bundleVersion`** in `ProjectSettings.asset` is the binary version Android reports (currently `1.0`). Bump it only when something significant changes (auth model, app rebrand, deep refactor). Most test builds reuse `1.0`.
- **APK filename suffix** is the human-friendly tag for each shared build:
  - `v0.1.0` — initial remote-fika build (the first version with multiplayer + voice working end-to-end)
  - `v0.1.1-remote-fika` — environment polish; same APK filename, distinguished only in this changelog and in `dist/friend-test/README.md`
  - `v0.1.2-session-fix` — recovery + tooling pass (current)
  - `v0.1.3-...` — next test build's suffix should describe its dominant theme
- **Increment the patch number** for each shared build. The suffix is free-form — readable beats semantic.
- **`Application.version`** is logged at every app start (`app_started` event in the debug log includes `"version":"1.0"`). When a tester reports a problem, the log tells you which `bundleVersion` they were on; the APK file they downloaded tells you which `v0.x.y-suffix` it was. The CHANGELOG bridges those two.

## How tester reports should reference a version

When a friend reports something:

1. Open their pulled debug log (see `docs/debug-logging.md`).
2. The first line is the `app_started` event — it carries `product_name`, `version` (= `Application.version`), `device_model`, `platform`.
3. Cross-reference with the APK filename they say they installed.
4. Find the matching CHANGELOG section to know what changed since the previous build they may have run.

So the version triangle is: **APK filename suffix ↔ CHANGELOG entry ↔ `Application.version` in log header**.
