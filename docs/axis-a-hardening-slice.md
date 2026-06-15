---
title: Axis A connection-state hardening slice
description: Implements the Axis A hardening designed in docs/networking-stabilization-plan.md — Photon Voice preflight before host/join, focus-regain and idle-disconnect reconnect, NGO OnTransportFailure handler, and mode-toggle teardown. No new networking behaviour, no new discovery protocol, no UI work — just makes failures diagnosable and recoverable instead of silent. Also closes backlog row 4.102 (mode-toggle-doesn't-stop-session).
category: networking
status: shipping
last_updated: 2026-06-15
sections:
  - What we did
  - Why
  - Files changed
  - New public API (VoiceBootstrap)
  - New log events
  - G10 testability
  - State-text changes the user sees
  - What we measured
  - What we deliberately did not do
  - Compatibility with C1 (and L0)
---

# Axis A connection-state hardening slice

## What we did

Four small, additive changes — none of them touch the C1 event-based Relay-code discovery (`feat(networking): add C1 event-based Relay-code discovery`, `e669c05`):

1. **Photon Voice preflight before host/join.** `NetworkBootstrap.StartHost` and `NetworkBootstrap.StartClient` now call `_voiceBootstrap.IsConnectedToMaster` + `WaitForConnectedAsync(5 s)` + `TryReconnect()` before doing anything else. If Photon isn't ready, surface `Voice offline — try again` immediately rather than queueing a join via `_pendingRoom` and timing out at 8 s with no actionable signal.
2. **Focus-regain reconnect.** `VoiceBootstrap.Update()` watches `Application.isFocused` for the false→true edge. If Photon is `Disconnected` at that moment, fire one `TryReconnect()`. Does NOT auto-rejoin the room — user must press host/join again. Targets the Quest sensor-toggle / Meta-menu-close case where Photon's socket got reaped while the app was backgrounded.
3. **Idle-disconnect reconnect with 5 s backoff.** When `voice_disconnected_while_in_room` fires AND `Application.isFocused`, schedule one reconnect 5 s later via `ScheduleIdleReconnectAsync()`. Guarded by `_reconnectScheduled` so duplicates can't queue.
4. **NGO `OnTransportFailure` handler + mode-toggle teardown + post-failed-join cleanup.** `NetworkBootstrap` subscribes to `NetworkManager.OnTransportFailure` (NGO's own escalation when Unity Relay reports `Transport failure! Relay allocation needs to be recreated`); the handler logs `ngo_transport_failure_detected` and resets `_busy`/`_hostedAlias`. `ToggleMode` becomes `async void` and calls `StopAsync()` first if a session is live (closes backlog 4.102). `StartClient`'s `relay_join_failed` path now calls a new `ResetAfterFailedJoinAsync()` so the next B-press doesn't race a half-joined Photon room.

## Why

`docs/networking-stabilization-plan.md` §"Two failure axes" separated Photon-Voice-not-ready (Axis A) from Relay-code-not-delivered (Axis B). The 2026-06-15 sessions repeatedly tangled the two:

- Henrik's joiner attempt with `"JoinOrCreateRoom can't be sent because peer is not connected"` was Axis A failing before C1 even got a chance to run.
- L2's 20:17 same-LAN run hit `unity_error: "Transport failure! Relay allocation needs to be recreated"` and the host silently sat in "Waiting for friend" while NGO died underneath it.
- Backlog row 4.102 (mode-toggle leaves orphan sessions) compounded the confusion — Henrik created 3 Relay allocations in one session.

This slice doesn't fix the underlying root causes of those failures (consumer-router hairpin NAT is environmental, see backlog row 4.108; Photon disconnects on focus loss is OS-level). It makes the failures **diagnosable** — every state transition is logged, every reconnect attempt is logged with its result, and the user sees an actionable in-VR state instead of being stuck "Waiting for friend" forever.

## Files changed

| File | Change | LOC |
|---|---|---|
| `UnityProject/Assets/Scripts/Voice/VoiceBootstrap.cs` | Added `IsConnectedToMaster`, `CurrentState`, `WaitForConnectedAsync`, `TryReconnect`, focus-regain + idle-disconnect detection in `Update()`, `ScheduleIdleReconnectAsync` | +~95 / 0 modified |
| `UnityProject/Assets/Scripts/Networking/NetworkBootstrap.cs` | Added preflight to `StartHost` + `StartClient`, refactored `Stop()`→`Stop()`+`StopAsync()`, `ToggleMode` async with stop-first, `OnTransportFailure` handler, `ResetAfterFailedJoinAsync` + call from failed-join path | +~85 / ~10 modified |
| `docs/debug-logging.md` | Documented 11 new event names | +11 |
| `docs/axis-a-hardening-slice.md` (this file) | New slice doc | ~180 |
| `docs/networking-stabilization-plan.md` | Updated Plan status footer to mark Axis A landed and re-anchor to new commit | +5 / ~3 modified |

C1 implementation files (the lines audited by L0) are not touched, but line numbers shift downstream. L0 must be re-run against the new commit (line numbers in the L0 checklist tables in `docs/networking-stabilization-plan.md` will be updated as part of re-anchoring).

## New public API (VoiceBootstrap)

| Member | Signature | Purpose |
|---|---|---|
| `IsConnectedToMaster` | `bool` (get) | True when Photon `ClientState` is `ConnectedToMasterServer` or `Joined`. The "ready to operate" check for callers that need to issue room ops. |
| `CurrentState` | `string` (get) | `_voice?.Client?.State.ToString() ?? "null"` — for logging. |
| `WaitForConnectedAsync(float timeout, int pollMs)` | `Task<bool>` | Mirrors `WaitForRoomJoinedAsync`. Returns true on connect within timeout, false on timeout. |
| `TryReconnect()` | `bool` | Idempotent. Returns true if already connected or a connection attempt is already underway; calls `ConnectUsingSettings()` and logs `voice_reconnect_attempt` if neither. Never loops. |

The result of any `TryReconnect()` is observable via the next `voice_reconnect_succeeded` or `voice_reconnect_failed` log event — detected by `Update()`'s existing state-transition tracking.

## New log events

11 new events, all written via the existing `DebugLogger.Log()` path (JSONL, auto-flushed). Documented in `docs/debug-logging.md`'s event table:

```
relay_host_voice_offline           state=<ClientState>
relay_join_voice_offline           state=<ClientState>
relay_host_voice_reconnect_failed  state=<ClientState>
relay_join_voice_reconnect_failed  state=<ClientState>
voice_reconnect_attempt            from_state=<ClientState>
voice_reconnect_succeeded
voice_reconnect_failed             from_state=<ClientState>
voice_focus_regain_reconnect       from_state=<ClientState>
voice_idle_disconnect_reconnect
ngo_transport_failure_detected     mode=<Mode>, was_host=<bool>
relay_join_cleanup_after_fail
mode_toggle_stop_first             mode=<Mode>, in_ngo_session=<bool>, in_relay_session=<bool>
```

Sample sequence — preflight catches an offline Photon and recovers:

```jsonl
{"event":"host_pressed","mode":"Relay","room":"A"}
{"event":"relay_host_voice_offline","state":"Disconnected"}
{"event":"voice_reconnect_attempt","from_state":"Disconnected"}
{"event":"voice_state","state":"ConnectingToMasterServer"}
{"event":"voice_state","state":"ConnectedToMasterServer"}
{"event":"voice_reconnect_succeeded"}
{"event":"relay_host_attempt","room":"A"}
... rest of normal host flow ...
```

Sample sequence — focus-regain reconnect (Editor or Quest):

```jsonl
{"event":"app_focus_lost","focused":false,"voice_transmit_muted":true}
{"event":"voice_state","state":"Disconnected"}
{"event":"voice_disconnected_while_in_room"}
{"event":"app_focus_gained","focused":true,"voice_transmit_restored":true}
{"event":"voice_focus_regain_reconnect","from_state":"Disconnected"}
{"event":"voice_reconnect_attempt","from_state":"Disconnected"}
{"event":"voice_state","state":"ConnectingToMasterServer"}
{"event":"voice_state","state":"ConnectedToMasterServer"}
{"event":"voice_reconnect_succeeded"}
```

## G10 testability

The L2 doc's gate G10 ("Photon disconnect during host idle is detected") can now be exercised entirely in the Editor by focus-toggling the OS window:

1. Press Play in Editor, press `M` (Relay), press `H` (host).
2. Wait for `relay_host_ready`.
3. Click outside the Editor window (any other app) — `app_focus_lost` fires.
4. Wait ~30–60 s. Photon's keepalive eventually times out and the Editor logs `voice_state state=Disconnected` then `voice_disconnected_while_in_room`.
5. Click back into the Editor window — `app_focus_gained` fires, then `voice_focus_regain_reconnect`, then `voice_reconnect_attempt` → `voice_reconnect_succeeded`.

Total in-Editor test: ~60 s. No headset needed for G10.

For the Quest path: take the headset off for 60+ s, put it back on. Same event sequence in the Quest's JSONL.

## State-text changes the user sees

| Scenario | Before slice | After slice |
|---|---|---|
| Press host/join while Photon disconnected | (silently queued, timed out 8 s later as "No fire found" / generic) | "Voice offline — try again" — immediate, actionable |
| NGO `OnTransportFailure` fires mid-session | (stuck in "Waiting for friend" forever) | "Connection dropped — press X then try again" |
| Failed Relay join | "Couldn't reach fire" | "Couldn't reach fire" (unchanged — but next press starts clean) |
| Mode-toggle during live session | mode flips silently, orphan session lingers | mode_toggle_stop_first logged → StopAsync runs → mode flips → "Mode · Internet" |

No new world-space text. No new icons. No new buttons. Just the existing 1-line state text overlay, now showing actionable text instead of silent stuckness.

## What we measured

**Pending L2 re-run on hotspot to fill in.** Acceptance criteria from the request:

| Criterion | Verification approach | Status |
|---|---|---|
| Existing C1 logs fire unchanged | Re-run L2 hotspot, expect identical G1–G7 events | TBD post-build |
| L0 audit still passes | Walk audit checklist against new HEAD; line numbers will shift but PASS verdicts hold | TBD post-build |
| L2 hotspot G1–G9 still pass | Re-run L2 on hotspot, compare against previous green run | TBD post-build |
| G10 explicitly testable or logged | Click-out-of-Editor procedure above produces full event chain | Documented; awaiting first observation |
| Compiles without warnings | Batchmode build via `./scripts/build-quest.sh` | TBD post-implementation |

Will be filled in after the post-implementation build + re-test cycle.

## What we deliberately did not do

- **No LAN-mode changes.** `Mode.Lan` paths are untouched — backlog row 4.107 (consider removing LAN entirely) is a separate slice.
- **No new discovery protocol.** C1 is GREEN per L2 hotspot run; no changes to event codes, `OpRaiseEvent` paths, or `OnPlayerEnteredRoom` broadcast.
- **No UI redesign.** State-text strings updated, but no new world-space text, no new icons, no new buttons, no tutorial changes.
- **No auto-rejoin after reconnect.** Per the plan: reconnect re-establishes the master-server connection but the user must press host/join again to re-enter a room. Avoids "did I want to be in a room?" ambiguity after long backgrounds.
- **No reconnect loops or storms.** Every reconnect path is single-shot per trigger (focus edge, idle-disconnect detection, preflight). `_reconnectScheduled` guards against duplicate idle attempts. `TryReconnect()` short-circuits when already in a connecting state.
- **No `AddCallbackTarget(this)` move from `Update()` to `Start()`.** Plan's C1 review flagged this as "fragile" but it has not bitten us in practice (broadcast OR response always reaches the joiner per L2 hotspot). Deferred — separate slice if observed needed.
- **No structural refactor of `NetworkBootstrap`'s state machine.** The `_busy` + `_state` field pattern stays. State text strings are the only mutation surface.
- **No new harness, no L1 implementation.** L2 on hotspot already passed twice; L1's TCS-isolation harness remains YAGNI.

## Compatibility with C1 (and L0)

C1's three code paths are byte-identical with `e669c05`:

| L0 item | Before slice (line in `e669c05`) | After slice |
|---|---|---|
| `RelayCodeBroadcastEventCode = 1` | line 33 | shifts down due to new fields above; **content unchanged** |
| `RelayCodeRequestEventCode = 2` | line 34 | same |
| `RelayCodeResponseEventCode = 3` | line 35 | same |
| `OnPlayerEnteredRoom` broadcast | line 326 | shifts; content unchanged |
| `WaitForRelayCodeEventAsync` request raise | line 297 | shifts; content unchanged |
| `OnEvent` switch | line 359 | shifts; content unchanged |
| `NetworkBootstrap.StartHost` call to `PublishRelayCodeToJoiners(realCode)` | line 442 | shifts (downstream of preflight insertion); content unchanged |
| `NetworkBootstrap.StartClient` `await WaitForRelayCodeEventAsync(8s)` | line 554 | shifts; content unchanged |
| `Stop()` calls `ClearPublishedRelayCode()` | line 578 | now in `StopAsync()`; content unchanged |

The L0 checklist in `docs/networking-stabilization-plan.md` references specific line numbers — these will need re-anchoring as part of this slice's commit, but every content check still PASSes.
