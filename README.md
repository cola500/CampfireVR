# unity-mcp-lab

A cozy social VR experiment for Meta Quest 3, built entirely as thin vertical slices through Claude Code driving the Unity Editor over MCP.

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

```
adb=/Applications/Unity/Hub/Editor/6000.4.7f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$adb devices                          # expect your Quest serial
# In Unity:
# File → Build Settings → Build And Run   (or Cmd+B)
```

The APK installs and launches automatically. Falling back to flat-screen Editor view: enable `Main Camera` and disable `VRRig`.

## LAN multiplayer testing

Scene has `NetworkManager` (NGO + UnityTransport) and `NetworkBootstrap`. `serverAddress` defaults to `127.0.0.1` — edit it in the scene before building if the host runs on a different machine.

| Action | Editor (Mac) | Quest |
|---|---|---|
| Start as host | press **H** | right controller **A** |
| Start as client | press **C** | right controller **B** |
| Stop | press **X** | re-launch app |

The OnGUI overlay shows the local IPv4 addresses. To pair two Quests: read the host's IP off its overlay, set that as `serverAddress` on the second build, rebuild, deploy.

When it works: the static `PlayerSlot_B` placeholder disappears and the remote player's head appears anchored at the `RemoteRig` (mirror of `VRRig` across the fire), facing the campfire. The owner's head pose is broadcast in seat-relative coordinates so the remote always sits at their seat regardless of where the owner physically is. On disconnect, `PlayerSlot_B` returns. Hands are not synced yet. No voice.

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

In rough order, each tiny enough to ship in one commit. Full list in [docs/roadmap.md](docs/roadmap.md):

1. Place the first remote `PlayerHead` at `PlayerSlot_B`'s position and hide the static placeholder when a remote peer connects.
2. Sync the hand anchors over the network too (same pattern as the head).
3. Voice chat (Vivox or peer-to-peer alternative).
4. Cozy polish — bloom on the flame, ambient crackle audio, dimmer global ambient.

## License

MIT — see [LICENSE](LICENSE).
