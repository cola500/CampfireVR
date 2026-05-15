# Voice chat — research and recommendation

> Investigation slice. No code or packages added yet. Decisions captured here, slice plan below.

## What we are choosing for

- **2 people** at a campfire (free tiers apply, no scaling pressure)
- **Quest 3 standalone** (Android build, no PC/Quest Link)
- **Already wired**: NGO 2.x, UnityTransport, Unity Relay session via join code, Unity Authentication anonymous
- **What we want to keep**: head/hand/breathing sync over NGO+Relay should not have to change
- **What we want to add**: spatialised real-time voice between the two seats around the fire

## Options surveyed

### 1. Unity Vivox  ⚠️ deprecated direction

- Was Unity's first-party voice chat service, hosted backend, spatial audio supported.
- **Unity has announced Vivox is being sunset**: new project adoption is discouraged and the service is being wound down. Existing integrations still work today, but committing a new project to it is committing to a forced rewrite later.
- Would integrate cleanly with our existing Unity Authentication.
- **Verdict: rule out**. Building a future on a deprecated service is exactly the kind of trap thin slices are supposed to help us avoid.

### 2. Dissonance Voice Chat  💵 paid asset, NGO-native

- Third-party Unity Asset Store package, one-time ~80 USD.
- Has a first-class `Dissonance.Integrations.UnityNetcode` add-on that piggybacks on the existing NGO connection: voice rides our Relay session, no second network.
- Mature, stable, low-friction. Spatial audio via Unity AudioSource.
- No dashboard, no extra account, no auth — just import the asset.
- **Best ergonomics**, downside is the paywall and being a closed third-party dependency.

### 3. Photon Voice 2  🆓 hosted, separate network

- Free tier: 20 concurrent users per project — irrelevant cap for us (we need 2).
- Hosted Photon Cloud handles relay/STUN/TURN; no infra to run.
- Voice runs on its own network connection independent of NGO. Adds a parallel session, but does not interfere with our Relay-routed NGO traffic.
- Spatial audio built in via `Speaker` components driving Unity AudioSource.
- Active development, large community, well-supported on Quest.
- Requires a Photon AppId from photonengine.com dashboard (separate from Unity Dashboard).
- **Solid free option, but you sign up for two service ecosystems** (Unity Services + Photon).

### 4. WebRTC (Unity WebRTC package or custom)

- Direct P2P audio with sub-50 ms latency.
- `com.unity.webrtc` is official but low-level — we would have to implement SDP exchange, ICE candidate signalling, microphone capture binding, remote audio playback.
- Could reuse our existing NGO+Relay channel as the signalling transport (one-time SDP exchange) — elegant in theory.
- Spatial: we wire received audio to a spatial AudioSource manually, doable but more code.
- **Lowest latency**, **highest implementation cost**. Wrong tool for a "first hello" spike. Worth revisiting if Photon's latency or terms ever bite us.

### 5. Meta / Oculus voice solutions

- Meta XR Audio SDK provides excellent spatial-audio *playback* but no voice-chat infrastructure.
- Meta Voice SDK is oriented toward AI/transcription (Wit.ai), not P2P voice chat between users.
- Meta does not ship a turnkey voice-chat service for Quest standalone apps the way Vivox did.
- **Verdict: not a fit** for our use case.

## Comparison

| Solution | 2-person MVP | Quest standalone | NGO compat | Relay compat | Setup | Latency | Spatial | Pricing | New auth | Maintenance | Future scale |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Vivox | OK today | Yes | Independent | Independent | Medium | ~150 ms | Yes | Free tier | None (we have it) | **Deprecated** | Migration looming |
| Dissonance | Excellent | Yes | **Native** | Rides on NGO | Low (import asset) | ~150 ms | Yes | ~$80 once | None | Stable | Good |
| Photon Voice 2 | Excellent | Yes | Coexists | Independent | Medium (Photon dash) | ~100–150 ms | Yes | Free (20 CCU) | Photon AppId | Active | Good |
| WebRTC | Possible | Yes | Custom | Hijack signalling | **Very high** | <50 ms | Manual | Free (P2P) | None (DIY) | DIY | Limited (P2P) |
| Meta XR Audio | N/A — playback only |||||||||||

## Recommendation: **Photon Voice 2 (free tier)**

It is the smallest viable path *for us, today*, that:

1. Doesn't cost money for a spike.
2. Doesn't depend on a sunset service (rules out Vivox).
3. Doesn't require writing our own signalling/transport (rules out WebRTC).
4. Doesn't force us to refactor the NGO/Relay setup that already works (rules out anything that wants to own networking).
5. Works on Quest standalone with documented setup steps and an active community to crib from.

Dissonance is a close second and is the cleaner architectural fit (voice over our Relay session, no parallel network). If the $80 stops being a friction point or we find we need the deeper NGO coupling, we revisit.

## Required for the chosen path

### Unity packages

- `com.exitgames.client.photon.voice` (Photon Voice 2 via Asset Store or registry)
- Photon Voice depends on Photon Realtime — installed as a dependency.

### Dashboards

- New: photonengine.com account → "Create a new app" of type **Photon Voice** → record the AppId.
- Unchanged: Unity Dashboard. We don't add Unity services for voice; Photon is independent.

### Android permissions

- `android.permission.RECORD_AUDIO` in the merged AndroidManifest (Unity adds this when `Microphone` is referenced from C#, but explicit add is safer).
- Runtime prompt on first launch is automatic on API ≥ 23 (Quest 3 is API 32+). Unity's `Permission.RequestUserPermission(Permission.Microphone)` lets us defer voice init until granted.

### Auth

- None beyond the Photon AppId. No accounts, no sign-in.

## Slice plan (each plan ships independently)

### Slice A — Verify Quest microphone capture
- No networking, no packages. Just `Microphone.devices`, `Microphone.Start`, plug clip into an `AudioSource`, render an OnGUI level meter.
- Goal: confirm Android permission works, mic is reachable from Unity on Quest, gain is reasonable.

### Slice B — Photon Voice bootstrap + connect  ✓ done

- Photon Voice 2 imported via Unity Asset Store (UPM scoped registry was probed and the URL `packages.photonengine.com` did not respond — Photon does not publish Voice 2 over UPM yet). PUN 2 came along as a dependency; we ignore it.
- `App Id Voice` field of `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` is the canonical config.
- `VoiceBootstrap.cs` on the `NetworkBootstrap` GameObject: `[RequireComponent(typeof(VoiceConnection))]`, calls `ConnectUsingSettings()` in `Start`, subscribes to `Client.StateChanged`, prints `Voice: {state}` via OnGUI.
- Verified states observed: `PeerCreated → ConnectingToNameServer → ConnectedToNameServer → ConnectingToMasterServer → ConnectedToMasterServer → JoinedLobby`.
- No Recorder, no Speaker, no room join. Just connectivity.

### Slice C — Mono voice between two Quests  ✓ done

- `Recorder` added to the same GameObject as `VoiceConnection`; wired as `PrimaryRecorder`.
- `Assets/Prefabs/VoiceSpeaker.prefab` (AudioSource with `spatialBlend = 0` + `Speaker` component) registered as `VoiceConnection.SpeakerPrefab`. Photon auto-instantiates one Speaker per remote voice stream.
- `VoiceBootstrap.JoinRoom(name)` / `LeaveRoom()` exposed. Called from `NetworkBootstrap` after Host/Client succeeds.
- Room name derivation: Relay mode uses the 6-character join code as the Photon room name; LAN mode uses the constant `"lan-campfire"`. Both peers reach the same name independently, so Photon Voice room membership lines up with NGO/Relay session membership without any extra UI.
- Status strings: `Voice room joining… (CODE)` → `Voice connected (CODE)` → `Voice: left room` → `Voice: disconnected`.

### Slice D — Spatial voice anchored at the head  ✓ done

- `VoiceSpeaker.prefab` regenerated with `AudioSource.spatialBlend = 1`, `AudioRolloffMode.Linear`, `minDistance = 0.5`, `maxDistance = 10`, `dopplerLevel = 0`.
- Tiny `VoiceSpeakerPlacer` MonoBehaviour on the same prefab: in `OnEnable` finds the scene `RemoteRig` and reparents the freshly-spawned Speaker GameObject at local `(0, 1.2, 0)` — head height above the remote seat. Works because both peers' scenes have `RemoteRig` at the same world coords.
- Photon `VoiceConnection` still auto-spawns the prefab from `SpeakerPrefab`; the Placer hooks in on the instantiation side, no Photon API surface touched.
- 2-player assumption baked in: every incoming remote voice goes to the single `RemoteRig`. Fine for our vision; a per-player factory is a later slice if we ever sit three around the fire.

### Slice E — Cozy polish
- Light volume curve (`AudioRolloffMode.Linear` with min=0.5 m / max=10 m) so voices feel close when across the fire and fade to nothing if someone wandered away.
- Enable Photon Voice's built-in noise suppression and auto-gain.
- Optional: small breath-synced subtle bobbing on the head visual when `Speaker.IsPlaying` is true — adds "they're speaking" affordance without lip-sync.

## Risks and open questions

1. **Two parallel network sessions on Quest**: NGO over Unity Relay *and* Photon Voice over Photon Cloud. Negligible bandwidth for 2 people, but slightly more battery and slightly higher chance of one connection wedging while the other holds. Acceptable for spike.

2. **Microphone permission timing**: must request `Permission.Microphone` *before* the first `Microphone.Start` call, or capture fails silently on Quest. Plan for an explicit request at app start, before joining a session.

3. **Echo when both Quests are in the same physical room** (likely during local testing). Photon Voice ships with acoustic echo cancellation; Quest hardware AEC is also good. Should work, but worth testing with one set of headphones on if echo persists.

4. **AppId leakage in repo**: Photon AppIds are public-safe in the sense that they identify *which* app and not *who*, but anyone with the AppId can connect to the same Photon room namespace. For a personal project this is fine. Store it in a ScriptableObject (the canonical Photon pattern) so it doesn't sit hardcoded in scripts.

5. **Photon Voice 2 vs Photon Voice 1**: Voice 1 is deprecated. Asset Store sometimes still lists both — make sure to install **Photon Voice 2**.

6. **Region selection**: Photon Cloud has regional endpoints. Default auto-selection is fine for two friends in Sweden (eu region). Worth confirming once we have telemetry.

7. **Future migration path**: if Photon's terms shift, swapping to Dissonance is roughly Slice C/D rewritten against a different SDK — same architectural shape, no NGO changes. We're not locked in.

## What "hello from Stockholm" needs technically

The first time you and your friend on different internet connections actually *hear each other*:

1. Both apps have completed Slice C (Photon Voice up, Recorder local, Speaker remote).
2. Mic permission granted at app start on both Quests.
3. One Quest hosts (right A) and reads its `CAMPFIRE CODE`.
4. Other Quest joins (right B → type code), NGO via Relay is up.
5. Photon Voice room is auto-joined per NGO connection (same code → same Photon room name).
6. Each Quest's mic feeds the local `Recorder`; Photon Cloud relays the audio frames; each remote `Speaker` plays them through the spatial AudioSource on the corresponding `PlayerHead`.
7. You hear "tjena" from approximately the right side of the fire.

That moment is **four small slices away** (A → B → C → D), each independently verifiable, each cheap to roll back.
