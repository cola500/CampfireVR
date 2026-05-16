# CampfireVR

A cozy social VR experiment for Meta Quest 3, built entirely as thin vertical slices through Claude Code driving the Unity Editor over MCP.

> The git repo + folder are still called `unity-mcp-lab` for historical reasons (that's where the MCP integration work started). The product, app, and most paths have rebranded to `CampfireVR`. See [docs/project-rename-notes.md](docs/project-rename-notes.md) for the optional repo-rename steps.

## Vision

Two people meet in VR, sit by a low-poly campfire, and talk. Nothing more.

The repo starts as the AI ↔ Editor link and grows one verifiable slice at a time toward that goal — Quest 3 standalone, seated, low-poly, cozy. See [docs/vision.md](docs/vision.md) for the longer version.

## Current MVP

`CampfireRoom` scene running standalone on Quest 3:

- Night-time campfire room (ground, logs, flickering flame, point-light glow, dark navy ambient).
- Seated player rig at `PlayerSlot_A` facing the fire, with explicit camera offset for sitting eye height.
- Head tracking via HMD, hand placeholders via tracked Quest controllers, trigger feedback.
- A second placeholder slot (`PlayerSlot_B`) that subtly looks at the fire.
- Minimal LAN multiplayer spike that synchronises a remote player's head pose between two Quests (or Quest + Editor).

No voice, no hands-on-network, no locomotion, no interactions. Every piece is a separate slice.

## Verified capabilities

- [x] Unity Editor controlled by Claude Code via [`CoderGamester/mcp-unity`](https://github.com/CoderGamester/mcp-unity)
- [x] Scene authoring through MCP (primitives, materials, lighting, scripts, components, prefabs, build settings)
- [x] Quest 3 standalone build via Oculus XR Plugin
- [x] HMD pose tracking with a hand-written `XRHeadTracker` (no XRI/Input System)
- [x] Hand/controller presence as primitive placeholders tracking `XRNode.LeftHand` / `RightHand`
- [x] Trigger input feedback (subtle scale pulse on the hand placeholder)
- [x] Netcode for GameObjects on UnityTransport, owner-authoritative head pose sync over LAN

## Architecture overview

```
Root
├── World                       (static; no script moves it)
│   ├── Ground, Log_1, Log_2, Flame, FireLight (+ FireLightFlicker)
│   ├── Atmosphere (NightAtmosphere — RenderSettings ambient + skybox)
│   ├── Seat_A, Seat_B, PlayerSlot_A (disabled), PlayerSlot_B (+ FaceTarget)
│   ├── EyeHeightMarker_A, Directional Light, Main Camera (disabled)
│
├── VRRig (1.6, 0, 0, rot Y=270°)
│   ├── XRTrackingOriginSetter (Device + recenter on start)
│   ├── XRDebugLogger
│   └── CameraOffset (local 0, 1.2, 0)          ← seated eye height
│       ├── VRCamera        (XRHeadTracker node=CenterEye, MainCamera tag)
│       ├── LeftHandAnchor  (XRHeadTracker node=LeftHand   + XRControllerInputFeedback)
│       │   └── LeftHandMesh
│       └── RightHandAnchor (XRHeadTracker node=RightHand  + XRControllerInputFeedback)
│           └── RightHandMesh
│
├── NetworkManager  (Unity.Netcode.NetworkManager + UnityTransport)
└── NetworkBootstrap (host/client startup, OnGUI overlay)
```

Networking model: each peer spawns a `PlayerHead` prefab on connect. Owner-authoritative `ClientNetworkTransform` syncs head pose. Owner hides its own visual (we render through `VRCamera`).

## Tech stack

| Component | Version |
|---|---|
| Unity Editor | `6000.4.7f1` (Unity 6.4) |
| Render pipeline | Built-in |
| XR | `com.unity.xr.management 4.5.0` + `com.unity.xr.oculus 4.5.0` |
| Networking | `com.unity.netcode.gameobjects 2.1.1` + `com.unity.transport 2.4.0` |
| MCP bridge | [`CoderGamester/mcp-unity`](https://github.com/CoderGamester/mcp-unity) |
| Node.js / npm | ≥ 18 / ≥ 9 (verified on 26 / 11.12) |
| Host machine | macOS Apple Silicon (Rosetta 2 required) |
| Target device | Meta Quest 3 standalone |

## Quest 3 setup

**One-time:**

1. Unity Hub → Installs → Add Modules → **Android Build Support** with **OpenJDK** and **Android SDK & NDK Tools**.
2. `sudo softwareupdate --install-rosetta --agree-to-license` (Unity's toolchain needs it even on arm64).
3. Quest: Developer Mode on via the Meta Quest mobile app; allow USB debugging on first connect.
4. Open the project in Unity, run **Tools → Quest Setup → Configure Project for Quest 3** once.

**Each iteration:**

```sh
# Plug in the Quest, then from the repo root:
./scripts/build-quest.sh --launch                # build + adb install -r + monkey-launch
./scripts/build-quest.sh --install               # build + adb install -r (don't auto-launch)
./scripts/build-quest.sh                         # build only (writes to UnityProject/Builds/)

# Re-deploy the most-recent APK without rebuilding (useful after a Quest reboot
# or to share the same build to a second headset):
./scripts/build-quest.sh --install-only --launch # skip build, install latest + launch
./scripts/build-quest.sh --install-only          # skip build, install latest

# Install a specific older versioned APK (e.g. roll back to a previous build):
./scripts/build-quest.sh --apk UnityProject/Builds/CampfireVR-v0.1.2-session-fix-20260516-2025.apk --install
```

Each successful build produces two files in `UnityProject/Builds/`:

- `CampfireVR-<version>-<YYYYMMDD-HHMM>.apk` — versioned, kept forever (no auto-cleanup yet).
- `CampfireVR-latest.apk` — copy of the most recent build, what `--install-only` defaults to.

Version tag comes from `CHANGELOG.md`'s most recent `[v…]` heading (e.g. `v0.1.2-session-fix`), falling back to `bundleVersion` in `ProjectSettings.asset`, then `v0.1.0`. So if you want to bump the tag without rebuilding, just edit the CHANGELOG heading.

The script wraps `Tools/Quest Setup/Build Remote Fika APK` (`QuestBuildAPK.Build`) in Unity batchmode, so you don't need to open the Editor. **Close the Editor first** if CampfireVR is open — Unity can't acquire the project lock from batchmode if the GUI is editing the same project. (`--install-only` skips the build entirely so the Editor can stay open.)

Manual fallback (if the script isn't an option):

```
adb=/Applications/Unity/Hub/Editor/6000.4.7f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$adb devices                          # expect your Quest serial
# In Unity:
# File → Build Settings → Build And Run   (or Cmd+B)
```

The APK installs and launches automatically. Falling back to flat-screen Editor view: enable `Main Camera` and disable `VRRig`. See [docs/ci-cd-quest-build-plan.md](docs/ci-cd-quest-build-plan.md) for the longer plan (GitHub Actions compile-check Phase 2, Unity Cloud Build Phase 3).

## Multiplayer testing

Scene has `NetworkManager` (NGO + UnityTransport), `NetworkBootstrap`, and `ServicesBootstrap`. The bootstrap supports two modes (the visible labels in the world-space tutorial are user-friendly; the C# enum values in brackets are the internal names):

| Visible label | Internal | Use | How to start |
|---|---|---|---|
| **Internet** | `Relay` | two devices on different internet connections | Unity Relay free tier; single-letter room code (A–Z, default A) shared out of band |
| **Same Wi-Fi** | `Lan` | same Wi-Fi / same machine | direct IP — set `serverAddress` in the scene before building (effectively dev-only today; no runtime IP entry, no LAN discovery) |

Default is **Same Wi-Fi** (`Mode.Lan` in the scene's serialized field). Toggle with **left Y** on Quest or **M** in the Editor.

| Action | Quest | Editor (Mac) |
|---|---|---|
| Host session | left controller **X** | **H** |
| Join session | right controller **B** | **C** |
| Switch mode | left controller **Y** | **M** |
| Recenter seat/view | right controller **A** | — |
| Stop / disconnect | take headset off or quit via Meta button | **X** |

There is currently **no Quest button bound to `Stop()`** — to leave a session in headset, press the Meta button and quit the app. The editor `X` key is the only `Stop()` trigger. Tracked in `docs/app-alignment-qa.md` as a recommended follow-up slice.

**Same Wi-Fi (LAN) flow:** the host's IP is shown in the editor-only overlay (`Local IPs: …`). Read it from the editor or via `adb shell ip addr`, set it as `serverAddress` on the client build's `NetworkBootstrap` component, rebuild, deploy. Not viable for a vanilla user — gated behind a scene edit.

**Internet (Relay) flow:**

The room is a single letter A–Z. The panel always shows the current letter (default `A`) under the headline so host and join paths use the same room without any pickers or editor modes.

1. On host (Quest): if the panel's bottom line reads `mode · Same Wi-Fi`, press **left Y** to flip to `mode · Internet`. Then press **left X** to host room `A` (or whatever letter is currently shown). The world-space panel switches to `🔥 YOUR FIRE` and walks `Creating fire ... → Sharing room → waiting for friend ...`. The room letter is displayed mid-panel.
2. Share the single letter out of band (SMS, Discord, "we're both on A").
3. On client (Quest): make sure the bottom line reads `mode · Internet`, then press **right B** to join the displayed room. State walks `Looking for room A ... → Joining fire ... → Connected`. No edit mode, no slot navigation — joining is one button.
4. To change room before host/join: nudge the **right thumbstick** sideways. The letter cycles A → B → ... → Z → A (and `Room: X` updates in the panel + the legend's `host room X` / `join room X` lines update with it). Both host and joiner have to land on the same letter — defaulting both to `A` means a fresh launch on both devices Just Works without anyone touching the stick.
5. Host sees a brief `🔥 Friend joined` notification before the panel fades to blank. Head, hands, and presence breathing sync over Relay.
6. **Stop:** no in-VR button. Quit the app from the Meta system menu.

A–Z = 26 possible rooms. Defaulting both sides to `A` is the no-touch path; if you need to deconflict (two pairs testing at once on the public Photon AppId), each pair picks a private letter.

In the Editor, the same actions are bound to **M** / **H** / **C** / **X**; an extra "Editor room override:" text field is shown so you can type a different letter without using the stick.

Unity Dashboard prerequisites: Authentication and Relay services must be Active for the project's `cloudProjectId`. Anonymous sign-in is automatic; no UI.

**Ambient fire crackle:** `Assets/audio/campfire_crackle.wav` is played by an `AudioSource` on the `FireCrackleAudio` GameObject parented to `Flame`. Looping, `spatialBlend = 1`, linear rolloff 0.5–8 m, volume 0.4 — under conversation, present in silence. The clip is yours to drop in (see `Tools → Ambience Setup → Create FireCrackleAudio` for the re-runnable wiring).

**Voice chat (spatial, from across the fire):** Photon Voice 2 is imported under `Assets/Photon/`. `VoiceBootstrap` connects to Photon Cloud at startup; after Host/Client succeeds via the regular campfire flow, it auto-joins a Photon Voice room whose name equals the Relay join code (or `lan-campfire` on LAN). A `Recorder` on `NetworkBootstrap` captures the local mic; remote voices are played by Speakers auto-instantiated from `Assets/Prefabs/VoiceSpeaker.prefab`. A tiny `VoiceSpeakerPlacer` reparents each spawned Speaker under `RemoteRig` at eye height, with `AudioSource.spatialBlend = 1` and linear rolloff 0.5–10 m, so the friend's voice comes from their seat across the fire. The overlay walks `Voice: connecting… → Voice connected (CODE) → Voice: left room`. No mute button — see [docs/voice-research.md](docs/voice-research.md) for what's next.

When it works: the static `PlayerSlot_B` placeholder disappears and the remote player's head appears anchored at the `RemoteRig` (mirror of `VRRig` across the fire), facing the campfire. The owner's head pose is broadcast in seat-relative coordinates so the remote always sits at their seat regardless of where the owner physically is. **Two small cubes** also appear at the remote's hand positions, driven by NGO `NetworkVariable<Vector3>` / `NetworkVariable<Quaternion>` pairs — same seat-relative transform applied to `LeftHandAnchor` / `RightHandAnchor`. On disconnect, `PlayerSlot_B` returns. No finger tracking, no IK, no voice.

## MCP workflow

The whole project was authored through Claude Code calling `mcp__mcp-unity__*` tools against a running Unity Editor. Notable patterns we settled on:

- **Re-runnable Editor menus** (`Tools → Quest Setup → ...`, `Tools → Network Setup → ...`) configure non-trivial setup (Player Settings, XR loader, NetworkManager bindings) declaratively. Easier than poking individual fields through MCP and reproducible from scratch.
- **`Assets/Refresh` is required** after writing a new `.cs` via MCP — `recompile_scripts` alone does not surface the new file to the asset database.
- **MCP cannot bind `UnityEngine.Object` references** through JSON `componentData`. Workarounds: auto-find by name in `OnEnable` (used in `FaceTarget`, `NetworkHead`), or wire references inside an Editor menu (`NetworkSetup`).
- **Verify with `get_console_logs`** after each compile-affecting change.
- **Restart Claude Code** after editing `.mcp.json` or starting the Unity-side MCP server.

## Known issues / limitations

- `.mcp.json` is gitignored. It embeds an absolute path and a `Library/PackageCache/com.gamelovers.mcp-unity@<HASH>/` segment; the hash changes on package upgrade. `.mcp.example.json` ships as a template.
- Hand placeholders sit on the controller grip tracking point, not the palm — they feel slightly offset.
- `serverAddress` is baked into the scene at build time. No runtime IP entry, no LAN discovery.
- No graceful Stop on Quest builds — re-launch the app to disconnect.
- ~~`PlayerSlot_B` and a remote `PlayerHead` can co-exist visually; not yet de-duplicated.~~ Resolved: remote head is anchored at `RemoteRig` and `PlayerSlot_B`'s mesh hides while occupied.
- Floor tracking origin alone was not enough on Quest 3; we use Device origin + an explicit `CameraOffset y=1.2`. See [docs/retro-log.md](docs/retro-log.md).

## Next slices

See [docs/roadmap.md](docs/roadmap.md) for the live list of done / next / deferred slices. Most items listed in earlier README revisions of this section (remote head/hand sync, voice chat, ambient crackle, cozy polish) have since shipped — see the `## Done` section of the roadmap. Current focus per `docs/app-alignment-qa.md`: in-VR disconnect binding and a copy pass on the per-phase legend.

## License

MIT — see [LICENSE](LICENSE).
