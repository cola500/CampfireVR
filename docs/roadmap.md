# Roadmap

Each item below is a single slice — one commit, one observable change.

## Done

- **MCP scene manipulation** — Claude reads/edits scenes through `mcp-unity`.
- **CampfireRoom prototype** — ground, logs, flame, two seats, two player slots, camera.
- **Fire light flicker** — Perlin noise on `FireLight.intensity`.
- **Night atmosphere** — flat dark-blue ambient, skybox cleared, dim cool directional.
- **Social presence placeholders** — `FaceTarget` rotates capsules to look at the fire.
- **Quest 3 standalone build** — Oculus XR Plugin, IL2CPP/ARM64/Linear, re-runnable setup menu.
- **Seated spawn + camera offset** — explicit `CameraOffset y=1.2` under `VRRig`.
- **HMD pose tracking** — `XRHeadTracker` on `VRCamera` via `UnityEngine.XR.InputDevices`.
- **World static, only camera moves** — explicit `World` root, no environment under `VRRig`.
- **Hand/controller presence** — primitive placeholders tracking `XRNode.LeftHand` / `RightHand`.
- **Trigger feedback** — subtle scale pulse via `XRControllerInputFeedback`.
- **Minimal LAN multiplayer spike** — NGO + UnityTransport, owner-authoritative head pose sync.
- **Remote seat presence** — first remote player anchored at `RemoteRig`; `PlayerSlot_B` placeholder hides while occupied.
- **Remote hand sync** — `NetworkVariable<Vector3/Quaternion>` for left/right hand poses, seat-relative.
- **Presence breathing** — subtle sine on remote head scale; non-owner only.
- **Auth bootstrap** — `UnityServices.InitializeAsync` + anonymous sign-in via `Unity.Services.Authentication`.
- **Relay join-code connectivity** — `MultiplayerService.CreateSessionAsync().WithRelayNetwork()`; 6-char code over internet.
- **Cozy Quest join UX** — left X toggles Mode, left Y stops, right B opens `TouchScreenKeyboard` for the code; big spaced campfire code with warm pulse; explicit state strings.

## Next — Voice slices

Chosen direction: **Photon Voice 2 (free tier)**. Full rationale and alternatives considered in [voice-research.md](voice-research.md). Tiny slices in order:

- ✓ **Voice A — Quest microphone capture probe.** `MicrophoneTest.cs` reads RMS, renders OnGUI level bar. `RECORD_AUDIO` permission, mic gain, sample window all verified.
- ✓ **Voice B — Photon Voice bootstrap.** Photon Voice 2 imported from Asset Store. `VoiceBootstrap.cs` calls `VoiceConnection.ConnectUsingSettings()` and surfaces the `Client.StateChanged` cycle as `Voice: …` in the overlay. AppId lives in `PhotonServerSettings.asset` only.
- ✓ **Voice C — Minimal remote voice.** `Recorder` on `NetworkBootstrap`, `VoiceSpeaker.prefab` (non-spatial `AudioSource` + `Speaker`) auto-instantiated by `VoiceConnection.SpeakerPrefab`. `VoiceBootstrap.JoinRoom/LeaveRoom` ties Photon room name to NGO Relay code (or `lan-campfire`), so voice membership tracks campfire membership without extra UI.
- **Voice C — Mono voice between two Quests.** `Recorder` local + `Speaker` on remote `PlayerHead`. "Hello from Stockholm" moment.
- **Voice D — Spatial voice.** `Speaker.AudioSource.spatialBlend = 1` so the voice comes from the right side of the fire.
- **Voice E — Cozy polish.** Distance falloff, noise suppression, optional speech-bobbing affordance.

Risks to revisit each slice: parallel sessions on Quest, mic-permission timing, echo when testing in the same room, AppId hygiene. Full list in [voice-research.md](voice-research.md#risks-and-open-questions).

## Next — Other slices (deferable)

- **Cozy polish** — bloom on the flame, ambient crackle `AudioSource`, dimmer global ambient.
- **Avatar experiments** — replace capsules + cubes with low-poly shapes (a body silhouette, simple hand mitts).
- **Interaction objects** — a stick to poke the fire, a stone you can pick up. Nothing more.
- **Seat-facing tweak** — angle player slots slightly toward each other so eye-line crosses near the fire instead of running parallel.

## Later

- **Persistence** — name your seat, remember the last person who sat there.
- **Shared activities** — sketching in the air, listening to a track together.
- **Reconnect** — auto-rejoin if WiFi blips during a session.
- **Region/quality controls** — Photon region picker, voice bitrate sliders.

## Explicit non-goals

- Locomotion.
- Combat / game systems.
- Realistic graphics.
- Cross-platform beyond Quest standalone in the medium term.
- Matchmaking, friend lists, accounts.
