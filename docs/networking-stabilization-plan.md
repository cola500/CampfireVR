---
title: Networking stabilization plan
description: Verification-first plan for CampfireVR's two-peer discovery (Photon Voice + Unity Relay). Separates connection-state failures from join-code-discovery failures, reviews the shipping C1 event-based protocol, and defines a local Editor-as-peer harness that lets us prove the flow with a single Quest before involving Henrik again.
category: networking
status: draft
last_updated: 2026-06-15 (added Level 0–3 verification ladder; replaces flat "Local verification strategy" + "Minimal harness slice" sections)
sections:
  - Purpose
  - Two failure axes
  - What we know
  - What is unverified
  - Current architecture
  - C1 design review
  - Recommended design
  - Verification ladder
  - Level 0 — Static dry-run
  - Level 1 — Simulated dry-run
  - Level 2 — Editor-as-peer verification
  - Level 3 — Two-headset verification
  - Acceptance gates summary
  - Out of scope
  - Open questions
---

# Networking stabilization plan

## Purpose

The 2026-06-15 Henrik+Johan session burned ~8 minutes per attempt mashing buttons across three independent fix attempts (`henrik2/johan2`, `henrik3/johan3`, `henrik5/johan5`, `henrik6/johan6`). The scheme Henrik actually tested was **Plan B** (host writes `rc` to its own LocalPlayer custom property; joiner reads it via master-client lookup), shipped in commit `7b333b9`. Plan B failed because Photon Voice's LoadBalancingClient does not propagate custom properties to new joiners — the joiner saw `relay_join_master_property_missing` after 8 s on every attempt. **C1** (`relay_code_request` / `_response` / `_broadcast` via `OpRaiseEvent`) was authored as the replacement after that session and committed as `e669c05` on the same day. C1 has **never been observed working end-to-end on real Photon** — it postdates the 2026-06-15 session and has not been exercised since.

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

### 2. Axis A — connection-state hardening (new, this is the real fix) [landed: 2026-06-15, see `docs/axis-a-hardening-slice.md`]

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

## Verification ladder

Johan has one Quest. We have no Henrik available. The 2026-06-15 sessions taught us that ad-hoc two-headset tests produce ambiguous logs because too many failure surfaces are tangled at once. The cure is to climb a ladder of increasingly realistic verification, where each rung is **cheap to repeat** and **fails for one specific reason**.

| Level | What it proves | Photon? | Relay? | Unity Play? | Quest? | Henrik? |
|---|---|---|---|---|---|---|
| **L0** Static dry-run | Code paths and log events exist and match the plan | no | no | no | no | no |
| **L1** Simulated dry-run | Joiner TCS / timeout / event-handler logic is correct in isolation | **no** (mocked) | **no** (mocked) | yes (Editor) | no | no |
| **L2** Editor-as-peer | Real Photon Voice + real Unity Relay round-trip between Editor and Quest | yes | yes | yes (Editor) | yes (1) | no |
| **L3** Two-headset | Two real Quests over the open internet — production-equivalent | yes | yes | no | yes (2) | yes |

A failure at level *N* is a debugging hint that's pre-localised to the new surface introduced at *N*. If L0 passes and L1 fails, the bug is in the discovery-logic surface (TCS handling, event-code routing). If L1 passes and L2 fails, the bug is at the Photon/Relay integration surface (real network, real Photon Voice client). If L2 passes and L3 fails, the bug is at the headset surface (focus loss, Quest-specific Photon behaviour).

**Rule**: do not climb to level *N* until level *N−1* has passed twice in a row.

---

## Level 0 — Static dry-run

**Purpose**: confirm the code we *think* exists, exists. No Unity, no Play mode, no log file. ~5 minutes with an editor and `grep`.

**Why this exists**: the C1 row in the backlog references `VoiceBootstrap.cs:33–35, 270–401` and a list of seven log events. If those line numbers drift or an event name was typo'd, the rest of the ladder is wasted effort. L0 catches that in under a minute.

### Audit checklist

Walk this list top-to-bottom against the current commit. Tick each item; on the first FAIL, stop and fix before continuing.

**A. Event-code constants (`VoiceBootstrap.cs`)**

- [ ] `RelayCodeBroadcastEventCode = 1` declared (currently line 33)
- [ ] `RelayCodeRequestEventCode = 2` declared (line 34)
- [ ] `RelayCodeResponseEventCode = 3` declared (line 35)
- [ ] All three are in the 1–199 app-event range (200+ reserved by Photon)

**B. Host-side broadcast path**

- [ ] `OnPlayerEnteredRoom` (line 326) early-returns if `_hostRelayCodeForBroadcast` is null
- [ ] `OnPlayerEnteredRoom` raises code 1 with `TargetActors = [newPlayer.ActorNumber]` (line 331) — targeted, not broadcast
- [ ] `SendOptions.SendReliable` is used (line 336)
- [ ] Result is logged as `relay_host_event_broadcast` with `target_actor` + `queued` fields (line 338)

**C. Joiner-side request + wait**

- [ ] `WaitForRelayCodeEventAsync` creates one TCS per call and assigns to `_relayCodeWaiter` (line 291–292)
- [ ] Request is raised only when `_voice.Client.InRoom` is true (line 297)
- [ ] Request is raised with `Receivers = ReceiverGroup.MasterClient` (line 299)
- [ ] Request send is logged as `relay_code_request_sent` with `queued` field (line 305)
- [ ] Method awaits `Task.WhenAny(tcs.Task, timeoutTask)` with the caller's `timeoutSeconds` (line 308–309)
- [ ] `_relayCodeWaiter` is nulled before return so a late event can't fill a future TCS (line 313)
- [ ] Timeout path logs `relay_join_code_event_timeout` with `timeout_seconds` (line 316–317)

**D. Joiner-side event handler**

- [ ] `OnEvent` switches on `photonEvent.Code` (line 359)
- [ ] Code 1 (broadcast) → log `relay_code_broadcast_received` with `code_length`, complete TCS (line 361–367)
- [ ] Code 3 (response) → log `relay_code_response_received` with `code_length`, complete TCS (line 370–376)
- [ ] Code 2 (request) → log `relay_code_request_received` with `from_actor`, raise code 3 response targeted at sender (line 379–399)
- [ ] Response send is logged as `relay_code_response_sent` with `target_actor` + `queued` (line 395–397)

**E. NetworkBootstrap integration**

- [ ] `StartHost` calls `_voiceBootstrap?.PublishRelayCodeToJoiners(realCode)` after voice-room-joined and before declaring `_state = "Waiting for friend"` (line 442)
- [ ] `StartClient` awaits `_voiceBootstrap.WaitForRelayCodeEventAsync(RelayJoinPropertyTimeoutSec)` with the 8 s timeout (line 554)
- [ ] `Stop()` calls `_voiceBootstrap?.ClearPublishedRelayCode()` so a re-host doesn't reuse a stale code (line 578)

**F. Log-event documentation parity (`docs/debug-logging.md`)**

- [ ] All seven C1 events documented in the table: `relay_host_event_broadcast`, `relay_code_request_sent`, `relay_code_request_received`, `relay_code_response_sent`, `relay_code_response_received`, `relay_code_broadcast_received`, `relay_join_code_event_timeout`

### First-failing-gate interpretation

| First failing item | Diagnosis | What to do |
|---|---|---|
| A.* | Event codes wrong or missing → C1 not actually shipping | Re-check `git log VoiceBootstrap.cs`; the plan's references are stale. **Stop the ladder.** |
| B.* | Host can't broadcast on join → unsolicited path broken | Fix in source. Re-run L0. |
| C.1–C.4 | Joiner won't even send a request → request/response backup useless | Fix in source. Re-run L0. |
| C.5–C.7 | TCS lifecycle bug → late events could leak into next call, or timeout never fires | Fix in source. **Critical — L1 will not catch this if the TCS field handling is wrong.** |
| D.* | Joiner won't consume incoming events → no code path can succeed | Fix in source. Re-run L0. |
| E.* | Integration broken between NetworkBootstrap and VoiceBootstrap → C1 wired wrong even if both halves work | Fix in source. Re-run L0. |
| F.* | Logs won't tell us what happened during L1/L2/L3 → we'll be debugging blind | Update `debug-logging.md`. Not a code bug but a verification blocker. |

L0 is intentionally pessimistic: even one FAIL aborts the ladder. The whole point is to never run L1/L2/L3 against a baseline we can't trust.

---

## Level 1 — Simulated dry-run

**Purpose**: prove that joiner-side TCS completion, timeout, and event-handler logic behave correctly **without any Photon Voice cloud connection or Unity Relay allocation**. Catches logic bugs that would otherwise hide behind real network non-determinism at L2.

**Cost**: one new editor-only file. Zero production C# changes.

### Why this rung is worth the small harness

If L0 + L2 pass, L1 is technically optional. But L1 is also the **only** rung where we can deterministically reproduce a specific edge case (e.g. "what happens if a late broadcast arrives after timeout completed the TCS as null?"). Real Photon will not let us reproduce that on demand. So L1 lives between "checklist" and "real network" specifically to harden the logic without flakiness.

### State of existing code (audit result)

Searched `VoiceBootstrap.cs` and `NetworkBootstrap.cs` for `dry.?run|simulate|UNITY_EDITOR|debug.?inject|test.?hook` — **zero matches**. No existing hook lets us drive `OnEvent` synthetically or complete `_relayCodeWaiter` from a test harness. So L1 needs a small new surface.

### Proposed harness — smallest safe slice

**Not yet implemented; awaiting approval before adding any C#.**

Two parts, both **editor-only**, both behind `#if UNITY_EDITOR` guards so they cannot reach a Player build:

**Part 1: One editor-only inject method on `VoiceBootstrap`** (~15 lines, `#if UNITY_EDITOR` guarded):

```csharp
#if UNITY_EDITOR
// Editor-only hook for Level 1 dry-run verification. Bypasses Photon by
// directly completing the pending relay-code TCS. Throws if no waiter is
// active so a misuse fails loud instead of silently no-op'ing.
// Never compiled into Player builds.
internal void DryRun_CompleteRelayCodeWaiter(string code, string source)
{
    if (_relayCodeWaiter == null)
        throw new System.InvalidOperationException(
            "DryRun_CompleteRelayCodeWaiter called with no active waiter");
    DebugLogger.Log("dryrun_relay_code_injected", null,
        ("source", source), ("code_length", code?.Length ?? 0));
    _relayCodeWaiter.TrySetResult(code);
}
#endif
```

**Part 2: New editor-only file `UnityProject/Assets/Editor/Networking/RelayDiscoveryDryRun.cs`** (~80 lines). Adds three menu items under `Tools/Networking/Dry-Run/`:

- **`Simulate joiner receives response`** — starts a `WaitForRelayCodeEventAsync(8s)` on the active `VoiceBootstrap`, schedules a coroutine that calls `DryRun_CompleteRelayCodeWaiter("FAKE-CODE", "response")` after 1 s, asserts the awaited result equals `"FAKE-CODE"`. Pass = `dryrun_l1_pass` log event.
- **`Simulate joiner times out`** — same start, no inject. Asserts the awaited result is null after ~8 s, and `relay_join_code_event_timeout` was logged exactly once. Pass = `dryrun_l1_pass` log event.
- **`Simulate late event after timeout`** — starts a 3 s wait, schedules an inject at 5 s. Asserts the await returns null at ~3 s, the late inject throws (because `_relayCodeWaiter` is null by then), and no second TCS gets corrupted. Pass = `dryrun_l1_pass` log event.

All three log to the existing `DebugLogger` so the results land in the same JSONL stream as Level 2/3.

### Why this design is the smallest defensible slice

- **No production behaviour change**: the inject method is `#if UNITY_EDITOR`. It cannot be reached by code paths the Quest binary executes — verified by the compile guard, not by convention.
- **No new networking code**: we are not adding event codes, transports, or state. Only a way to *complete* the existing TCS from outside the existing OnEvent path.
- **No mock VoiceConnection / fake Photon**: those would be larger surfaces and easy to drift from the real one. Bypassing at the TCS layer means we still exercise our real `WaitForRelayCodeEventAsync` body.
- **Three tests cover the three branches** of the existing logic (response wins, timeout wins, late event arrives) — no more, no less.

### What L1 does NOT cover

L1 is deliberately partial. It does **not** prove:

- That Photon Voice's `OpRaiseEvent` actually delivers (that's L2/L3).
- That `OnEvent` is registered early enough to catch the host's broadcast (that's L2 — needs real Photon timing).
- That Photon Voice doesn't drop the connection on idle (that's L2/L3 with real network conditions).
- Host-side `OnPlayerEnteredRoom` broadcast logic — we'd need a fourth test that fires a synthetic `Player`-entered callback, which requires reflection or another inject method. **Defer** to L2 unless L2 specifically fails the host broadcast path.

### Acceptance for L1

- All three menu items pass twice in a row from a fresh Editor Play session.
- The DebugLogger JSONL contains three `dryrun_l1_pass` events per pair of runs.
- No `unity_error` events fire during any of the three tests.

If L1 fails on test 1 or 2, the bug is in the TCS plumbing (likely in `WaitForRelayCodeEventAsync` or `OnEvent`). If L1 fails on test 3, the bug is in the late-event guard at line 313 (`_relayCodeWaiter = null` before return).

---

## Level 2 — Editor-as-peer verification

**Purpose**: prove the full C1 round-trip over real Photon Voice and real Unity Relay, with one peer in Unity Editor and one peer on Johan's Quest. This is "Option A" from the earlier draft of this plan.

**Why this rung exists separately from L3**: Editor running on a Mac has no focus loss, no sensor-off events, no Quest-specific Photon Voice quirks. So the Editor side **cannot** fail Axis A. If L2 succeeds and L3 fails, the bug is Quest-only — exactly the diagnostic separation we couldn't get on 2026-06-15.

### Setup (works today with current code — no new C#)

NetworkBootstrap already has Editor key shortcuts (`H` host, `C` join, `M` mode toggle, `X` stop) at `NetworkBootstrap.cs:159–166`. The Editor's `VoiceBootstrap` connects to the same Photon Voice cloud the Quest does, and the Editor's `ServicesBootstrap` allocates from the same Unity Relay project.

### How Johan runs L2

1. Build APK + install on Quest: `./scripts/build-quest.sh --install`
2. Open `Assets/Scenes/CampfireRoom.unity` in Editor.
3. Editor: press Play. Press `M` until top-of-screen reads `Mode: Relay`. Press `H` to host.
4. Quest: launch the app. Short-tap Y until tutorial overlay shows Internet mode. Press B to join.
5. Pull Quest logs: `./scripts/pull-quest-logs.sh`
6. Editor log is at `~/Library/Application Support/CampfireVR/CampfireVR/debug-logs/`
7. Walk the gates below in order. Stop and diagnose at the first failing gate.

### Gates (each must pass before the next is meaningful)

| Gate | What we observe | Pass criterion |
|---|---|---|
| **G1** Editor reaches `voice_state=Joined` in room A | Editor JSONL | `voice_joined` fires within 10 s of pressing `H` |
| **G2** Quest reaches `voice_state=Joined` in room A | Quest JSONL | Same |
| **G3** Host's `relay_alloc_succeeded` fires before `voice_joined` | Editor JSONL | Timestamps confirm order |
| **G4** Joiner sends `relay_code_request_sent` after its `voice_joined` | Quest JSONL | Event with `queued=true` |
| **G5** Host receives `relay_code_request_received` | Editor JSONL | Event with `from_actor=<n>` |
| **G6** Host sends `relay_code_response_sent` | Editor JSONL | Event with `queued=true` |
| **G7** Joiner receives `relay_code_response_received` (or `_broadcast_received`) | Quest JSONL | Event with `code_length>0` |
| **G8** Joiner's `relay_join_succeeded` | Quest JSONL | NGO peer connection established |
| **G9** NGO `client_connected` fires on both sides | Both JSONL | IDs visible in both |
| **G10** Photon disconnect during host idle is detected | Take Quest off 60 s, put back on | `voice_disconnected_while_in_room` then `voice_reconnect_attempt` after focus regain (**only after Axis A hardening lands**) |

G1–G9 prove C1's design end-to-end. G10 proves Axis A hardening, and can only pass once the hardening from the "Recommended design" section is implemented.

### What can fail at L2 that L0+L1 won't catch

- `OpRaiseEvent` not actually delivering on Photon Voice's LoadBalancingClient (unverified — see "What is unverified").
- `AddCallbackTarget` registered too late (`Update()` line 75–79) so the broadcast misses the joiner. If G7 only ever shows `_response_received` and never `_broadcast_received` across multiple runs, this is the culprit. (Diagnosis-only — does not change correctness because response path is the backup.)
- 8 s timeout too tight under realistic Wi-Fi.
- Photon master-client identity reassigned during the discovery window.

### Acceptance for L2

- G1–G9 pass twice in a row.
- After Axis A hardening lands: G10 also passes.

---

## Level 3 — Two-headset verification

**Purpose**: prove the system works in the actual production configuration — two Quests on separate networks, with real focus loss, real sensor toggling, real cross-NAT Relay traffic.

**Precondition**: every other level green. We do not ask Henrik to install a build until L0–L2 are all passing.

### Henrik-readiness checklist (must all be true)

- [ ] **L0 passes** on the current commit (audit complete, all items ticked)
- [ ] **L1 passes** twice in a row (three menu tests, three `dryrun_l1_pass` events × 2)
- [ ] **L2 G1–G9 pass** twice in a row (Editor + Johan's Quest)
- [ ] **L2 G10 passes** at least once (Axis A hardening landed)
- [ ] **Backlog row 4.101 closed** — scene default flipped to `Mode.Relay` so LAN can't trap a fresh tester
- [ ] **Backlog row 4.102 closed** — `ToggleMode()` calls `Stop()` first if in a session
- [ ] **APK identity captured**: which commit hash, which APK filename, which `build_version` string in `app_started` — sent to Henrik
- [ ] **Expected per-step state documented** for Henrik: "press B, expect to see *X* on screen within *Y* seconds"

### What L3 proves that L2 doesn't

- Cross-NAT Relay traversal (Editor + Quest on same LAN don't exercise this fully).
- Focus loss / sensor off-on triggering Photon disconnect → reconnect path on a real Quest.
- Variance across two separate Quest devices on two separate Wi-Fi networks.
- Subjective UX of the discovery delay (8 s timeouts can feel different in headset than at a desk).

### What success at L3 looks like in the logs

Same gate pattern as L2, but now with both peers being Quests:

- Both `henrik-*.jsonl` and `johan-*.jsonl` show G1–G9 in their respective roles
- Either or both show `voice_disconnected_while_in_room` followed by a clean `voice_reconnect_succeeded` if anyone toggles their headset off-on mid-session (G10 in production)
- `client_connected` events appear in both logs within 30 s of the joiner pressing B

---

## Acceptance gates summary

Single-page roll-up of what must be true at each rung before climbing:

| Rung | Pass criterion | Repeats required | Time cost (approx) |
|---|---|---|---|
| L0 | Every checklist item ticked, no FAIL | 1 (re-run on every commit that touches C1) | 5 min |
| L1 | All three menu tests pass, 3× `dryrun_l1_pass` events | 2 fresh-Editor-Play sessions in a row | 10 min |
| L2 G1–G9 | All nine gates pass in JSONL order | 2 runs in a row | 20 min per run |
| L2 G10 | Disconnect + reconnect detected after focus regain | 1 (requires Axis A hardening landed) | 5 min |
| L3 | Same gates as L2 with both peers being Quests, full per-step doc shared | 1 successful run | 60 min including setup |

The ladder is deliberately monotonic: a passing rung never becomes invalid until production code on the verified path changes. Each commit that touches `VoiceBootstrap` or the `StartHost`/`StartClient` flow in `NetworkBootstrap` invalidates L0 onwards and the ladder must be re-climbed from L0.

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

**Status as of 2026-06-15 evening:**

- L0 GREEN against `e669c05` (24/24).
- L2 hotspot GREEN twice in a row against `e669c05` (`docs/l2-editor-quest-test.md` gates G1–G9 all PASS — see `quest-logs/20260615-203050/campfirevr-log-20260615-202723.jsonl`).
- L2 same-LAN FAIL identified as router hairpin NAT, not code (backlog row 4.108).
- Axis A connection-state hardening landed in `docs/axis-a-hardening-slice.md`. C1 lines shift downstream of the additions; L0 must be re-run against the new commit and re-anchored here.

**Next concrete action**: re-run L0 audit against new HEAD (post-Axis-A). When L0 GREEN, re-run L2 on hotspot to confirm acceptance criteria from the Axis A slice. Then L3 (Henrik) is unblocked once backlog rows 4.101 (LAN default flip) and 4.102 (now closed by Axis A's mode-toggle stop-first) are also resolved.

The L0 → L1 → L2 → L3 order is non-negotiable: each rung is a precondition for the next, and a fail at any rung means stop, fix, re-run from that rung.
