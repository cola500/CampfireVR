---
title: Networking stabilization plan
description: Verification-first plan for CampfireVR's two-peer discovery (Photon Voice + Unity Relay). Separates connection-state failures from join-code-discovery failures, reviews the shipping C1 event-based protocol, and defines a local Editor-as-peer harness that lets us prove the flow with a single Quest before involving Henrik again.
category: networking
status: draft
last_updated: 2026-06-15
sections:
  - Purpose
  - Two failure axes
  - What we know
  - What is unverified
  - Current architecture
  - C1 design review
  - Recommended design
  - Local verification strategy
  - Minimal harness slice (proposed)
  - Acceptance gates for next remote test
  - Out of scope
  - Open questions
---

# Networking stabilization plan

## Purpose

The 2026-06-15 Henrik+Johan session burned ~8 minutes per attempt mashing buttons across three independent fix attempts (`henrik2/johan2`, `henrik3/johan3`, `henrik5/johan5`, `henrik6/johan6`). The C1 event-based discovery (`relay_code_request` / `_response` / `_broadcast` via `OpRaiseEvent`) was deployed in commit `7b333b9` but has **never been observed working end-to-end** because Henrik's Photon Voice connection died before the joiner state machine reached the request step.

We will stop stacking fixes blindly. This document is the verification-first replacement for the next iteration:

1. Separate the two failure modes that have been compounding.
2. Review the shipping C1 protocol on paper before testing it.
3. Build a single-headset local harness that proves C1 without Henrik.
4. Define explicit acceptance gates for the next remote test.

Until the local gates pass, **we do not ask Henrik to install another build.**

## Two failure axes

The 2026-06-15 logs show two distinct failure surfaces tangled together. Future debugging gets exponentially easier if we triage them independently:

### Axis A — Photon Voice connection state

The transport for everything downstream. If Photon Voice is not `ConnectedToMasterServer` (or `Joined`), no event can be raised and no custom property can be set. Symptoms:

- `JoinOrCreateRoom can't be sent because peer is not connected` (Henrik's joiner attempt — Photon's own exception)
- `voice_state` events showing `Disconnected` mid-session
- `voice_disconnected_while_in_room` events
- `relay_join_voice_timeout` events (joiner pressed B but `WaitForRoomJoinedAsync` timed out at 8 s)

**Root causes** observed or suspected:

- Headset focus loss → process backgrounded → Photon socket reaped
- Headset off/on (sensor toggle) → focus + pause events → Photon disconnect
- Idle in scene with no user interaction long enough for keepalive to lapse
- Race between `_voice.ConnectUsingSettings()` in `Start()` and the user pressing X / B before the state machine has reached `ConnectedToMasterServer`

### Axis B — Relay join-code discovery / exchange

The C1 protocol that hands the Relay allocation join-code from host to joiner over the Photon Voice room. Symptoms:

- `relay_join_code_event_timeout` (joiner waited 8 s, no broadcast or response arrived)
- `relay_host_player_property_set_failed` (host couldn't even mirror the code into its own LocalPlayer — diagnostic only since C1; was the primary path in Plan B)
- `relay_join_failed` after the joiner successfully received a code but Unity Relay rejected the JoinAllocation call

**Root causes** observed or suspected:

- Custom-property propagation broken in Photon Voice's LoadBalancingClient (proven across three independent test sets — see backlog row 4.103)
- `OnPlayerEnteredRoom` race: host's callback may fire before joiner has registered its own `IOnEventCallback`, so the unsolicited broadcast is lost; joiner then has to fall through to the request/response path
- Single-shot request: joiner sends one `RelayCodeRequest`, waits 8 s — if that single event is lost or the host's `OnEvent` hasn't fired yet, there's no retry

**Critical insight from the logs**: every 2026-06-15 failure mode was Axis A failing first. We never even reached the point where we could observe Axis B succeeding or failing independently. That's why "stacking fixes" hasn't converged — we keep changing Axis B while Axis A is the actual blocker.

## What we know

Evidence chain from the 2026-06-15 session (full logs: `quest-logs/henrik-20260615-144825.zip` + `quest-logs/johan-20260615-145014.zip`):

| Observation | Evidence | Implication |
|---|---|---|
| Photon Voice can be in `Disconnected` when user presses join | Henrik's logs show `voice_state state=Disconnected` immediately before the `peer is not connected` exception | We have no precondition check before `StartClient` — Axis A bug |
| Host's `rc` custom property never reaches joiner's view | All three failure modes (room-prop, room-prop+lobby, player-prop) failed identically: host verify succeeded locally, joiner read returned null | Photon Voice's LoadBalancingClient does not propagate custom properties to new joiners — settled empirical finding |
| `_voice.PrimaryRecorder.TransmitEnabled` mute works on focus loss | `app_focus_lost` events include `voice_transmit_muted: true` | Slice 4 mic-mute is fine — does NOT prove Photon survives focus loss, only that we stopped streaming during it |
| C1 events (`OpRaiseEvent`) compile, queue, and are documented to propagate | Photon Voice's voice signaling itself rides on events; the SDK exposes `OpRaiseEvent` on the same client we already use | Strong prior that C1 *should* work once Axis A is stable. Untested empirically. |
| LAN mode is the trap | Both testers' first 8 min were on LAN with `serverAddress=127.0.0.1`; cross-NAT impossible | Already captured in backlog 4.101; cross-cuts this plan but not the primary Axis A/B issue |

## What is unverified

We must not assume any of the following until we have a green local run:

- That C1's broadcast path (`OnPlayerEnteredRoom` → `OpRaiseEvent` code 1) actually delivers to the joiner under Photon Voice 2's LoadBalancingClient.
- That C1's request/response path (codes 2 + 3) actually delivers.
- That `_voice.Client.OpRaiseEvent` succeeds on Photon Voice 2 (we expect yes — voice signaling depends on it — but our specific call sites are untested).
- That `OnEvent` is registered early enough on the joiner side to catch the host's `OnPlayerEnteredRoom` broadcast. `AddCallbackTarget` is called in `Update()` (line 75–79) after `_voice.Client` exists; if `Update()` hasn't run yet when the host's event fires, the broadcast is lost. The request/response backup should catch this, but we have not observed either side end-to-end.
- That Photon Voice's master-client identity remains stable across the discovery window (host should never lose master status before the joiner sends its request — likely fine but unverified).
- That the 8-second timeouts (`WaitForRoomJoinedAsync`, `WaitForRelayCodeEventAsync`) are tuned correctly. Henrik's joiner timed out at 8 s on `voice_joined` once; we don't yet know typical latency under good network conditions.

## Current architecture

Brief, pointing at the live code:

- **Game transport**: Unity Netcode for GameObjects + UnityTransport. Owner-authoritative `ClientNetworkTransform` on `PlayerHead`. (`Assets/Scripts/Networking/NetworkBootstrap.cs`)
- **Discovery / voice room**: Photon Voice 2's `VoiceConnection` (separate cloud connection from NGO). Single-letter room (`A`–`Z`, default `A`). (`Assets/Scripts/Voice/VoiceBootstrap.cs`)
- **Internet-mode flow** (`Mode.Relay`):
  1. Host: `ServicesBootstrap.HostRelayAsync()` → Unity Relay allocation → join code in memory
  2. Host: `VoiceBootstrap.JoinRoom(letter)` → wait for `voice_joined`
  3. Host: `PublishRelayCodeToJoiners(code)` → stores code in `_hostRelayCodeForBroadcast` for outbound broadcast on player-join
  4. Host: `SetAndVerifyRelayCodeAsync(code)` → writes `rc` to own LocalPlayer custom properties (diagnostic only — confirms host can write to its own state)
  5. Joiner: `VoiceBootstrap.JoinRoom(letter)` → wait for `voice_joined`
  6. Joiner: `WaitForRelayCodeEventAsync(8s)` → sends `RelayCodeRequest` immediately, awaits broadcast (code 1) or response (code 3)
  7. Joiner: `ServicesBootstrap.JoinRelayAsync(realCode)` → Unity Relay JoinAllocation → NGO handshake
- **LAN-mode flow** (`Mode.Lan`): direct UDP to `serverAddress` (currently `127.0.0.1` — see backlog 4.101).
- **Focus handling**: `AppLifecycle` mutes mic on focus loss. Does not reconnect Photon on focus regain (this plan adds that).

## C1 design review

Reading the shipping code (`VoiceBootstrap.cs:33–35`, `:270–401` and `NetworkBootstrap.cs:441–442`, `:554`) against the spec ("explicit request/response via Photon events"):

### What is sound

- **Two event-code channels** (broadcast + request/response) is the right shape. Whichever delivery succeeds first wins; the other is no-op via `TrySetResult` on a completed TCS.
- **`ReceiverGroup.MasterClient`** for the joiner's request: targets exactly the host. Saves bandwidth and removes any ambiguity about who replies.
- **`TargetActors = [photonEvent.Sender]`** for the host's response: targeted at the actual requester, not a broadcast. Correct.
- **`SendReliable`** on both directions: correct — discovery cannot tolerate drops.
- **Single-flight via `_busy`** in `NetworkBootstrap.StartHost`/`StartClient`: prevents two pending TCS instances.
- **`OnPlayerEnteredRoom` broadcast as a parallel path**: belt-and-braces. If the joiner's `OnEvent` is already registered when the host's callback fires, broadcast wins and the request never needs to be answered.

### What is missing or fragile

| Gap | Impact | Fix difficulty |
|---|---|---|
| **No joiner-side retry of `RelayCodeRequest`** — single send, 8 s wait. If that one event is silently dropped or arrives before host's `OnEvent` has registered, we time out with no second attempt. | Medium — host's `OnPlayerEnteredRoom` broadcast is the backup, but it relies on the joiner having registered `IOnEventCallback` before the host's callback fires (race). | Low — add 2–3 retry sends spaced ~1.5 s inside `WaitForRelayCodeEventAsync` |
| **No correlation / request IDs** | Low — `_busy` guards single-flight on each side, so a stale response can't accidentally fill a new request. But makes log analysis harder when both sides log the same event names with no shared ID. | Low — append an integer `req_id` to event payloads |
| **No precondition check that Photon Voice is `Joined` before sending the request** — code does check `_voice.Client.InRoom`, which is true only when state is `Joined`, so this is actually fine in practice. Worth noting as a latent risk if Photon's state semantics ever change. | None today | n/a |
| **`AddCallbackTarget(this)` happens in `Update()` (line 75–79)** rather than in `Start()` after `_voice = GetComponent<VoiceConnection>()` | Medium — between `Start()` and the first `Update()` frame, any Photon event addressed to us is dropped. Unlikely to bite the discovery path (the joiner can't be in a room yet) but it's fragile. | Low — move `AddCallbackTarget` to immediately after `_voice.ConnectUsingSettings()` in `Start()` |
| **Host's `_hostRelayCodeForBroadcast` not cleared between sessions** — `ClearPublishedRelayCode` is called from `Stop()` but not on transport-mode switch (backlog 4.102) | Low | Already covered by mode-toggle-doesn't-stop-session backlog item |
| **No "host wasn't in room yet when joiner asked" handling** — if joiner sends request before host has finished `WaitForRoomJoinedAsync`, host's `_hostRelayCodeForBroadcast` is still null, so the request is silently dropped (early return at line 383). No retry from the joiner = timeout. | Medium — only matters if joiner races ahead of host on a re-host attempt. Real risk during reconnect storms. | Joiner-side retry (above) solves this transparently |

### Verdict

**Keep C1.** The protocol shape is sound. The two material gaps are (1) no joiner-side retry, and (2) `AddCallbackTarget` is registered later than ideal. Both are small, additive changes — not redesigns.

What is bigger and outside C1 itself is **Axis A** (Photon Voice connection state) and the lack of a way to verify any of this locally. Those are the next two sections.

## Recommended design

### 1. C1 stays. Additive hardening only.

- **Joiner retry**: `WaitForRelayCodeEventAsync` sends the request, then re-sends every 2 s up to 3 total sends, then continues waiting for the remainder of the timeout window. Each send gets a `(attempt, req_id)` pair logged.
- **`AddCallbackTarget` early**: move from `Update()` to right after `_voice = GetComponent<VoiceConnection>()` in `Start()`. (`_voice.Client` is null until `ConnectUsingSettings` completes, so guard accordingly — may need to retry registration on first Client availability.)
- **Correlation IDs**: optional; add only if log-analysis pain shows up during harness runs.

### 2. Axis A — connection-state hardening (new, this is the real fix)

Treat Photon Voice as unreliable on Quest/mobile. Before any host/join button action:

- Verify `_voice.Client.State == ClientState.ConnectedToMasterServer` (or `Joined` for re-host case).
- If not, kick reconnect (`_voice.ConnectUsingSettings()`) and `await WaitForConnectedAsync(timeout)`.
- If still not connected after timeout, surface a clear state to the user (`"Voice offline — reconnecting…"`) and abort the host/join attempt cleanly.

During session:

- On `app_focus_lost`: mute mic (already happens). Log `voice_state` at the moment of focus loss. **Do not** force a reconnect — let Photon detect the disconnect naturally.
- On `app_focus_gained`: check `_voice.Client.State`. If `Disconnected`, log `voice_reconnect_attempt` and call `ConnectUsingSettings()`. Do not auto-rejoin the room — user must press host/join again.
- On idle `voice_disconnected_while_in_room`: log it (already happens). If `Application.isFocused`, attempt one reconnect after a 5 s backoff. Do not loop.

This adds three new logged events and one new public API on `VoiceBootstrap`:

- `voice_reconnect_attempt` / `voice_reconnect_succeeded` / `voice_reconnect_failed`
- `Task<bool> WaitForConnectedAsync(float timeoutSeconds)` on `VoiceBootstrap`

### 3. Three-step preflight before host/join

Wrap the existing `StartHost` / `StartClient` entry points with:

1. **Services ready?** Already checked (`_services.IsReady`). Keep.
2. **Voice connected to master?** New — `await _voiceBootstrap.WaitForConnectedAsync(5f)`. Abort with `relay_host_voice_offline` / `relay_join_voice_offline` if false.
3. **In Internet mode, do we already have a stale session?** Already partially checked (`_services.InRelaySession`). Keep.

Then proceed with the existing flow.

## Local verification strategy

Johan currently has one Quest. We need to validate Axis A + Axis B without Henrik.

### Recommendation: Editor-as-peer (Option A) — primary

**Why**: NetworkBootstrap *already* has Editor key shortcuts (`H` host, `C` join, `M` mode, `X` stop) at `NetworkBootstrap.cs:159–166`. The Unity Editor running the scene connects to the same Photon Voice cloud and Unity Relay services the headset uses. We can run the Editor as peer A and the Quest as peer B, on the same network or different networks. Zero new dependencies.

**Why not the alternatives**:

- **Option B (Mac standalone player)**: heavier, requires a Mac IL2CPP build, no benefit over Editor.
- **Option C (Editor-only Photon harness scene)**: would isolate Axis B from Axis A, but we'd be re-implementing what `VoiceBootstrap` already does. Only worth building if Option A reveals that Editor + Quest run paths diverge in ways that hide Axis B bugs. We're not there yet.
- **Option D (headless CLI)**: out of scope — overbuilding.

### What the harness must prove (acceptance for Option A)

In ascending order — each gate must pass before the next is meaningful:

| Gate | What we observe | Pass criterion |
|---|---|---|
| **G1: Editor reaches `voice_state=Joined`** in room A | Editor Console + JSONL log | `voice_joined` fires within 10 s of pressing `H` |
| **G2: Quest reaches `voice_state=Joined`** in room A | Quest log (adb pull) | Same |
| **G3: Host's `relay_alloc_succeeded` fires before voice room joined** | Editor log | Timestamps confirm |
| **G4: Joiner sends `relay_code_request_sent` after `voice_joined`** | Quest log | Event present with `queued=true` |
| **G5: Host receives `relay_code_request_received`** | Editor log | Event present with `from_actor=<quest's actor>` |
| **G6: Host sends `relay_code_response_sent`** | Editor log | Event present with `queued=true` |
| **G7: Joiner receives `relay_code_response_received`** | Quest log | Event present with `code_length>0` |
| **G8: Joiner's `relay_join_succeeded`** | Quest log | NGO peer connection established |
| **G9: NGO `client_connected` fires on both sides** | Both logs | IDs visible in both logs |
| **G10: Photon disconnect during host's idle is detected** | Take headset off for 60 s, put back on, check Editor log | `voice_disconnected_while_in_room` followed by `voice_reconnect_attempt` after focus regain (only after Axis A hardening lands) |

G1–G9 prove C1's design end-to-end. G10 proves Axis A hardening.

### How Johan runs Option A

Pre-Axis-A-hardening (today, with current code):

1. Open `Assets/Scenes/CampfireRoom.unity` in Editor.
2. Build APK and install on Quest (`./scripts/build-quest.sh --install`).
3. In Editor, press Play. Press `M` until mode reads `Relay`. Press `H` to host.
4. On Quest, launch the app. Switch to Internet mode (Y short tap). Press B to join.
5. Pull Quest logs (`./scripts/pull-quest-logs.sh`) + read Editor log from `~/Library/Application Support/CampfireVR/CampfireVR/debug-logs/`.
6. Compare against G1–G9 above. Note which gate fails first.

This works *today* without writing any new code. The only friction is needing to manually press both Quest and Editor at the right times. A minimal Editor menu (`Tools/Networking/Auto Host`, `Tools/Networking/Auto Join Room A`) would remove that friction but is not blocking.

### Why this isolates Axis A from Axis B

- Editor doesn't have a headset → no focus-loss / pause / sensor-off events ever fire. Editor's Photon Voice connection is stable as long as Mac stays awake. So **Axis A is removed on at least one peer.**
- If C1 fails Editor↔Quest, we know Axis B is the problem (Editor side has no connection-state issues).
- If C1 succeeds Editor↔Quest but fails Quest↔Quest with Henrik, Axis A is the problem on Henrik's side.

This is the diagnostic separation we couldn't get from the 2026-06-15 sessions because both sides were Quests with unstable Photon connections.

## Minimal harness slice (proposed)

Only worth building if Option A's manual flow shows specific friction. The minimum is:

- **Editor menu**: `Tools/Networking/Run As Host (Room A, Relay)` and `Tools/Networking/Run As Joiner (Room A, Relay)`. Each menu item plays the scene and synthesizes the H or B button press once `_services.IsReady` and Photon is `ConnectedToMasterServer`.
- **No new networking code**. Just an Editor wrapper that drives the existing `StartHost` / `StartClient`.
- **No new UI**, no new harness scene, no headless mode.

If Option A's manual flow proves the gates without this menu, **don't build it.** YAGNI.

## Acceptance gates for next remote test

Before we ask Henrik to install another build:

1. **G1–G9 pass locally** (Editor + Johan's Quest) at least twice in a row, with all expected JSONL events in both logs.
2. **Axis A hardening landed** (preflight check + `WaitForConnectedAsync` + focus-regain reconnect). G10 passes locally.
3. **C1 either kept as-is or hardened with joiner retries** (decision made after G1–G9; if those pass clean, retries are optional).
4. **Backlog row 4.101 (LAN `127.0.0.1` trap) closed** — default mode flipped to `Relay` so a fresh tester can't accidentally hit it. (One-line change, cheap, removes a confounding variable from Henrik's next test.)
5. **Backlog row 4.102 (mode-toggle doesn't stop session) closed** — `ToggleMode()` calls `Stop()` first if in a session. (Same as above — removes confounding orphan sessions.)
6. **Two-headset preconditions documented**: which scene version (commit hash), which APK filename, what Henrik should see at each step.

Items 4 and 5 are not strictly C1 work but they are baseline cleanup that must happen for the next session to give us interpretable logs.

## Out of scope

This plan deliberately does **not** touch:

- Dog companion, hands, controllers, forest, grass, environment
- Visual polish, avatar rendering, materials
- Store assets, App Lab compliance settings, build artifact naming
- LAN mode beyond default-mode flip (we're not investing in LAN — it's a developer convenience)
- Voice quality, audio routing, spatial audio placement
- Photon Voice 2 NRE on scene-dirty (pre-existing Editor-only bug)

If any of the above need work, they get their own slice.

## Open questions

These need answers before we move from plan → implementation:

1. **Does Photon Voice's LoadBalancingClient definitely support `OpRaiseEvent`?** Strong prior yes — voice signaling itself uses events. But the only proof is to see `relay_host_event_broadcast queued=true` and a matching `_received` on the other side. First gate G4–G7 settles this.
2. **Is the 8 s `WaitForRoomJoinedAsync` timeout too tight on Quest under poor Wi-Fi?** Henrik's `relay_join_voice_timeout` hit at exactly 8 s once. Worth bumping to 12 s or making it adaptive.
3. **Should Axis A hardening's reconnect logic distinguish "intentional Stop" from "lost connection"?** Probably yes — don't reconnect if the user pressed long-press-Y or the app is shutting down. Easy via an `_intentionallyDisconnected` flag.
4. **Do we want a single in-VR "voice status" indicator?** Backlog UX item 3 suggests yes. Out of scope here, but coordinated.

---

## Plan status

This document is the contract for the next iteration. It will be edited in-place as gates pass or fail; do not strike-through, just update sections with `[updated: <date>]` markers.

**Next concrete action**: run Option A's manual G1–G9 sequence with the current commit (`7b333b9`) before changing any code. The result of that run determines whether we need joiner retries, whether `AddCallbackTarget` timing matters in practice, and whether the 8 s timeout needs tuning.
