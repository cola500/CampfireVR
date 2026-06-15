---
title: L2 Editor-as-peer test execution checklist
description: Step-by-step procedure to walk Level 2 of the networking verification ladder. One peer in Unity Editor, one peer on Johan's Quest. Each of the ten gates (G1–G10) defines its expected log line, pass criterion, failure interpretation, and the next diagnostic step — so a single test run identifies exactly which gate failed and what to investigate.
category: networking
status: draft
last_updated: 2026-06-15
sections:
  - Purpose
  - Audit anchor
  - Prerequisites
  - Pre-flight (before pressing Play)
  - Test sequence (what to press, in what order)
  - Gates G1–G10
  - Post-run analysis
  - First-failing-gate decision tree
  - What success looks like
  - Anti-patterns
---

# L2 Editor-as-peer test execution checklist

## Purpose

L2 of `docs/networking-stabilization-plan.md` — verify the full C1 round-trip over **real** Photon Voice and **real** Unity Relay, with one peer in Unity Editor and one peer on Johan's Quest. Single Quest, no Henrik.

This document is structured so that **one test run** produces enough evidence to identify exactly which of the ten gates failed (or to confirm they all passed). Walk top-to-bottom; do not skip pre-flight.

## Audit anchor

**Commit `e669c05`** ("feat(networking): add C1 event-based Relay-code discovery") or any later commit that L0 has been re-run against. If you're testing a different commit, walk L0 first against that commit and confirm 24/24 PASS before continuing.

To confirm the committed code matches the gates below:

```sh
git log --oneline -1                                # head should be e669c05 or later
git diff e669c05..HEAD UnityProject/Assets/Scripts/Voice/VoiceBootstrap.cs UnityProject/Assets/Scripts/Networking/NetworkBootstrap.cs
```

If the diff is non-empty, re-run L0 before continuing — line numbers in this doc may have drifted.

## Prerequisites

- [ ] L0 audit GREEN against the current commit (per `docs/networking-stabilization-plan.md` §"Level 0")
- [ ] Quest charged ≥ 30 %, connected to the same Wi-Fi as the Mac (same router — does not need to be the same SSID, but reduces NAT variables)
- [ ] adb reachable: `adb devices` shows one Quest authorized
- [ ] APK built from the current commit: `./scripts/build-quest.sh --install`
  - APK name visible in `app_started` will be `CampfireVR-remote-fika-test-v0.1.apk` (intentionally static; build identity comes from `app_started` fields)
- [ ] Unity Editor open with `Assets/Scenes/CampfireRoom.unity` loaded, not playing yet
- [ ] No other instances of CampfireVR running (`adb shell am force-stop com.unitymcplab.campfireroom` if unsure)
- [ ] `quest-logs/` and Editor log directory exist and are writable
- [ ] `scripts/pull-quest-logs.sh` is executable and adb-aware (try `./scripts/pull-quest-logs.sh --help` if unsure)

If any prereq fails, fix it before pressing Play. A failed prereq invalidates every gate below.

## Pre-flight (before pressing Play)

These checks catch the boring failures (wrong scene, wrong default mode, wrong room letter) before they masquerade as gate failures.

1. **Scene check**: confirm `Hierarchy` shows `NetworkBootstrap`, `VoiceBootstrap`, `ServicesBootstrap` under the scene root, plus a non-disabled `NetworkManager`.
2. **Default mode**: open `NetworkBootstrap` in the Inspector. `Mode` should be `Relay` (NOT `Lan`). If it reads `Lan`, abort and either flip it in the Inspector for this run or land backlog row 4.101 first. Testing in LAN mode will produce confusing failure modes that aren't real C1 bugs.
3. **Default room letter**: confirm `NetworkBootstrap._codeChars[0]` defaults to `'A'`. (Read-only confirmation; if it's set to something else, this run won't match the gate expectations.)
4. **Photon credentials**: open `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` and confirm `App Id Voice` is populated. An empty Voice app id will fail G1 with `voice_state=ConnectedToNameServer` looping.
5. **Disable other voice clients**: Discord, Zoom, etc. can grab the mic device. Not a gate-killer but pollutes the Photon Voice connection.
6. **Quest is awake** with proximity sensor not triggered (put it on or hold the sensor tab down before pressing B).

## Test sequence (what to press, in what order)

Total time: ~3 minutes for G1–G9, +1 minute idle for G10.

| Step | Side | Action |
|---|---|---|
| 1 | Editor | Press Play. Wait until top-of-screen reads `Mode: Relay`. If it reads `Lan`, press `M` once. |
| 2 | Editor | Press `H` (host). Watch the state text. Note wall-clock time. |
| 3 | Editor | Wait for state text to read `Waiting for friend` (≤ 10 s). If it reads `Host code didn't sync…` or `Voice room failed`, stop and analyse G1/G3. |
| 4 | Quest | Launch app. Wait for `Mode: Relay` (short-tap Y if needed). Note wall-clock time. |
| 5 | Quest | Press B (right secondary). Watch state text. |
| 6 | Quest | Wait until state text reads `Joining fire` then a friend visual appears across the campfire. Total Quest-side time from B-press to friend-visible should be ≤ 15 s. |
| 7 | Both | Hold position for 10 seconds (so G9 has clean idle data). |
| 8 | Quest | Optional G10: take Quest off head for 60 s with `app_focus_lost` expected. Put back on. |
| 9 | Editor | Press `X` to stop. |
| 10 | Both | Pull logs (next section). |

## Gates G1–G10

Each gate names the expected JSONL event(s), the success condition, what a missing/wrong event means, and the next thing to check.

Log fields in the format `event_name field=value` mirror the JSONL — actual JSON uses `{"event":"event_name","field":"value", ...}`.

---

### G1: Editor reaches `voice_state=Joined` in room A

| Field | Value |
|---|---|
| **What we do** | Press Play, press M to set Relay mode, press H |
| **Expected log lines** (Editor) | `voice_connect_attempt` → `voice_state state=ConnectedToMasterServer` → `voice_room_join_attempt room=A` → `voice_state state=Joined` → `voice_joined room=A` |
| **Success condition** | `voice_joined room=A` appears within 10 s of `host_pressed` |
| **Failure interpretation** | If `voice_state` stalls at `ConnectingToNameServer`: Photon app id wrong or no internet. If stalls at `ConnectedToMasterServer` without progressing: room-join failed silently — look for `unity_error` near that timestamp. If `voice_state=Disconnected` appears: Photon credentials are rejected. |
| **Next diagnostic step** | Open `PhotonServerSettings.asset` and re-check `App Id Voice`. Try a fresh `voice_connect_attempt` by closing Editor and re-opening — the connection lifecycle is one-shot per launch. |

---

### G2: Quest reaches `voice_state=Joined` in room A

| Field | Value |
|---|---|
| **What we do** | Launch app on Quest, press B (B handles join including JoinRoom) |
| **Expected log lines** (Quest) | Same as G1 (`voice_connect_attempt` → … → `voice_joined room=A`) but the `voice_room_join_attempt` is triggered by `relay_join_attempt` in `NetworkBootstrap.StartClient` |
| **Success condition** | `voice_joined room=A` appears within 10 s of `join_pressed` |
| **Failure interpretation** | Same diagnoses as G1, but on Quest. Additional possibility: `app_focus_lost focused=false` appears just before — user moved their head during the join and Photon disconnected. If `relay_join_voice_timeout room=A` appears, the 8 s `WaitForRoomJoinedAsync` budget was exceeded — bump or investigate. |
| **Next diagnostic step** | Check Quest log around `voice_connect_attempt` for any `unity_error`. Confirm Quest is on the right Wi-Fi (`adb shell dumpsys wifi` lists current SSID). |

---

### G3: Host's `relay_alloc_succeeded` fires before host's `voice_joined`

| Field | Value |
|---|---|
| **What we do** | Same as G1 — implicit timing check |
| **Expected log lines** (Editor) | `relay_host_attempt room=A` → `relay_alloc_succeeded` → `voice_room_join_attempt room=A` → `voice_joined room=A` → `relay_host_ready` |
| **Success condition** | `relay_alloc_succeeded` mono-timestamp < `voice_joined` mono-timestamp (strictly less than) |
| **Failure interpretation** | If `relay_alloc_failed`: Unity Relay free-tier quota exhausted (1 GB/month) or service-account auth failed. If `relay_alloc_succeeded` comes AFTER `voice_joined` (reversed order): the StartHost code path was edited and the C1 flow may have broken — re-run L0. |
| **Next diagnostic step** | On `relay_alloc_failed`: check Unity Dashboard → Relay → usage. On reversed order: `git diff e669c05 NetworkBootstrap.cs` to see what changed. |

---

### G4: Joiner sends `relay_code_request_sent` after its `voice_joined`

| Field | Value |
|---|---|
| **What we do** | Press B on Quest — request is sent automatically by `WaitForRelayCodeEventAsync` once Client.InRoom |
| **Expected log lines** (Quest) | `voice_joined room=A` → `relay_code_request_sent queued=true` |
| **Success condition** | `relay_code_request_sent` appears within 1 s of `voice_joined`, with `queued=true` |
| **Failure interpretation** | If absent entirely: `_voice.Client.InRoom` was false when `WaitForRelayCodeEventAsync` was called — race between voice state callback and the wait method. If `queued=false`: `OpRaiseEvent` rejected the send synchronously (Photon Voice client wasn't ready). If neither: the joiner reached `WaitForRoomJoinedAsync` but timed out — see G2's `relay_join_voice_timeout`. |
| **Next diagnostic step** | Cross-reference mono-timestamps: how many seconds between `voice_joined` and the (missing) `relay_code_request_sent`? If gap > 0.5 s, the issue is timing inside `WaitForRoomJoinedAsync` returning before InRoom is actually true. |

---

### G5: Host receives `relay_code_request_received`

| Field | Value |
|---|---|
| **What we do** | Passive — host's `IOnEventCallback.OnEvent` fires when request reaches Photon cloud |
| **Expected log lines** (Editor) | `relay_code_request_received from_actor=<n>` where `<n>` matches the joiner's actor number (typically 2 — host is 1) |
| **Success condition** | Event present, `from_actor > 1`, mono-timestamp within ~500 ms of joiner's `relay_code_request_sent` (allowing for round-trip + clock drift) |
| **Failure interpretation** | If absent despite joiner's G4 PASS: either Photon `OpRaiseEvent` doesn't propagate as expected (most-feared failure mode — would invalidate C1), or host's `AddCallbackTarget(this)` hasn't fired yet (registered in `Update()` line 75–79; should be long before joiner joins, but verify). If `from_actor=1`: the host received its own event back (bug). |
| **Next diagnostic step** | Look at host's earliest `voice_state` log to confirm `AddCallbackTarget` had time to register. If G6 (`relay_host_event_broadcast`) is present, the host's callback channel works — so the issue is specifically with code-2 events. |

---

### G6: Host sends `relay_code_response_sent`

| Field | Value |
|---|---|
| **What we do** | Passive — fires automatically when host's OnEvent handles the request |
| **Expected log lines** (Editor) | `relay_code_response_sent target_actor=<n> queued=true` where `<n>` matches the `from_actor` from G5 |
| **Success condition** | Event present within 50 ms of G5, with `target_actor` matching G5's `from_actor` and `queued=true` |
| **Failure interpretation** | Only happens if `_hostRelayCodeForBroadcast` was null when the request arrived (early return at `VoiceBootstrap.cs:383`) or `_voice.Client` is null. The former means `PublishRelayCodeToJoiners` was never called — but L0 confirms it's called at `NetworkBootstrap.cs:442`, so this would mean G3's `relay_alloc_succeeded` is missing or out of order. |
| **Next diagnostic step** | Walk the host log from `host_pressed` onward and confirm `PublishRelayCodeToJoiners` was reached. The proxy for that is `relay_host_ready` appearing — if `relay_host_ready` is present, `PublishRelayCodeToJoiners` was called. |

---

### G7: Joiner receives `relay_code_response_received` (or `_broadcast_received`)

| Field | Value |
|---|---|
| **What we do** | Passive — fires when host's response or broadcast reaches joiner |
| **Expected log lines** (Quest) | Either `relay_code_response_received code_length=<n>` (from G6's response) OR `relay_code_broadcast_received code_length=<n>` (from host's unsolicited push in `OnPlayerEnteredRoom`). First one wins; the late one is silently dropped (correct behaviour). |
| **Success condition** | At least one of those two events present with `code_length` > 4 (real Unity Relay codes are 6-character base-N strings) |
| **Failure interpretation** | If `relay_join_code_event_timeout timeout_seconds=8` appears instead: neither path delivered within budget. Likely Photon event delivery broken — but G5 + G6 prove the request/response path is half-working on Editor side, so the failure is specifically on the Quest's `OnEvent` receiving code 3. Could be `AddCallbackTarget` registration race on Quest startup. |
| **Next diagnostic step** | Check whether `relay_code_broadcast_received` appears at all across multiple runs. If `_response_received` always wins and `_broadcast_received` never fires, that's the `AddCallbackTarget` timing issue described in the plan's C1 review (`VoiceBootstrap.cs:75–79` registers in `Update()` — would fire too late to catch host's `OnPlayerEnteredRoom` broadcast). Not a correctness bug — response is the backup path — but worth fixing for resilience. |

---

### G8: Joiner's `relay_join_succeeded`

| Field | Value |
|---|---|
| **What we do** | Passive — `StartClient` continues to `JoinRelayAsync` with the received code |
| **Expected log lines** (Quest) | `relay_join_calling` → `relay_join_succeeded` |
| **Success condition** | `relay_join_succeeded` present within 5 s of G7 |
| **Failure interpretation** | If `relay_join_failed` instead: Unity Relay rejected the JoinAllocation call. Code may be malformed (corrupted in transit — check G7's `code_length`; real codes are exactly 6 chars), expired (Relay allocations have a TTL — usually generous, but possible), or for a different region than the joiner is in. If `relay_join_calling` appears but neither succeeded/failed: NGO `UnityTransport` set-up fault — look for `unity_error` around the timestamp. |
| **Next diagnostic step** | Confirm code_length from G7 is exactly 6. Check Unity Dashboard → Relay for the allocation entry corresponding to host's `relay_alloc_succeeded` timestamp — confirm it's "active" and in the joiner's region. |

---

### G9: NGO `client_connected` fires on both sides

| Field | Value |
|---|---|
| **What we do** | Passive — fires from `NetworkManager.OnClientConnectedCallback` |
| **Expected log lines** | (Editor) `client_connected id=<n> role=host` (this is the joiner showing up on the host); (Quest) `client_connected id=<n> role=client` (this is the joiner's own self-connected notification) |
| **Success condition** | Both events present; host log's id should match joiner's connection-issued id (typically 1, since host is 0) |
| **Failure interpretation** | If G8 succeeded but G9 doesn't fire on the host side: Relay handshake worked but NGO connection-approval rejected (unlikely without custom approval callbacks). If G9 fires on Quest side only: host's NGO server isn't accepting connections — `NetworkManager.IsServer` might be false. |
| **Next diagnostic step** | On the Editor side, confirm `NetworkManager.Singleton.IsHost` is true (the state text reading "Waiting for friend" implies yes). On the Quest side, confirm `IsConnectedClient` becomes true — the next event after `client_connected` on the joiner should be the `PlayerHead` prefab spawning. |

---

### G10: Photon disconnect during host idle is detected (Quest side only)

| Field | Value |
|---|---|
| **What we do** | Take Quest off head for 60 s, then put back on |
| **Expected log lines** (Quest) | `app_focus_lost focused=false` → (after 30–60 s) `voice_disconnected_while_in_room` or `voice_state state=Disconnected` → `app_focus_gained focused=true` → **(only after Axis A hardening lands)** `voice_reconnect_attempt` → `voice_state state=ConnectedToMasterServer` → `voice_reconnect_succeeded` |
| **Success condition** | **Today (pre-Axis-A-hardening)**: disconnect is detected (G10a). **After Axis A lands**: reconnect is also attempted (G10b). |
| **Failure interpretation** | If `app_focus_lost` fires but no `voice_disconnected_*` follows: Photon's keepalive survives focus loss for the duration of your idle period — not a bug, just a "we don't reach the failure mode in this window". Extend the off-head time to 5 minutes. If `app_focus_gained` fires but no reconnect attempt: Axis A hardening isn't landed yet — expected pre-hardening. If `voice_reconnect_attempt` appears WITHOUT prior Axis A hardening commit: someone landed the change without telling you (or this is the wrong commit). |
| **Next diagnostic step** | Look at `voice_state` transitions throughout the off-head period. If it stays `Joined` the whole time, Photon survived (extend the off-head time). If it goes `Disconnected` and stays there, focus regain doesn't auto-reconnect — Axis A hardening still needed. |

---

## Post-run analysis

### Pulling and merging the two logs

```sh
./scripts/pull-quest-logs.sh
# → quest-logs/<timestamp>/<one or more .jsonl files>

# Find the most recent Editor log (Mac):
ls -t ~/Library/Application\ Support/CampfireVR/CampfireVR/debug-logs/ | head -1
```

For interleaved analysis:

```sh
EDITOR_LOG=~/Library/Application\ Support/CampfireVR/CampfireVR/debug-logs/<latest>.jsonl
QUEST_LOG=quest-logs/<timestamp>/<file>.jsonl

{ sed 's/^/[editor] /' "$EDITOR_LOG";
  sed 's/^/[quest]  /' "$QUEST_LOG"; } |
  sort -k3 |          # sort by the "ts" field (third whitespace-delimited token)
  grep -E "relay_|voice_|client_connected|app_focus|host_pressed|join_pressed"
```

Output is one chronologically-sorted view of both sides — exactly the shape G1–G9 expect.

### Per-gate quick-check

Confirm or deny each gate with a one-liner:

```sh
# G1
grep -c voice_joined "$EDITOR_LOG"      # expect ≥ 1
# G2
grep -c voice_joined "$QUEST_LOG"       # expect ≥ 1
# G3 — order matters; eyeball the merged log
grep -nE "relay_alloc_succeeded|voice_joined" "$EDITOR_LOG"
# G4
grep relay_code_request_sent "$QUEST_LOG"   # expect queued=true
# G5
grep relay_code_request_received "$EDITOR_LOG"  # expect from_actor > 1
# G6
grep relay_code_response_sent "$EDITOR_LOG"     # expect queued=true
# G7
grep -E "relay_code_(response|broadcast)_received" "$QUEST_LOG"  # expect ≥ 1
# G8
grep relay_join_succeeded "$QUEST_LOG"
# G9
grep client_connected "$EDITOR_LOG"     # expect role=host (the joiner's appearance)
grep client_connected "$QUEST_LOG"      # expect role=client (own self-connected)
# G10 — only meaningful if you did the off-head test
grep -E "voice_disconnected_while_in_room|voice_reconnect_attempt" "$QUEST_LOG"
```

## First-failing-gate decision tree

Walk the gates in order. **Stop at the first gate that fails** — gates downstream are not meaningful until upstream ones pass.

```
G1 FAIL → Editor's Photon Voice can't reach room A.
         → Fix Photon credentials or network. Re-run from scratch.

G2 FAIL → Quest's Photon Voice can't reach room A.
         → Same diagnosis as G1 but Quest-side. Check Quest Wi-Fi and credentials.
         → If `relay_join_voice_timeout` appears: 8 s budget too tight or voice is racy.

G3 FAIL → Host's Unity Relay allocation broken.
         → Check Unity Dashboard → Relay → quota and project link.
         → If reversed order: code drift — re-run L0.

G4 FAIL → Joiner never sent the request.
         → Likely race: `WaitForRoomJoinedAsync` returned but Client.InRoom was false.
         → Investigate `_voice.Client.InRoom` vs `_inRoom` field discrepancy in VoiceBootstrap.

G5 FAIL → Host didn't receive the request despite joiner sending it.
         → Could be `OpRaiseEvent` not propagating (worst case — invalidates C1)
           OR host's `AddCallbackTarget` not yet active.
         → Check host's earliest `voice_state` and verify it preceded joiner's `voice_joined` by ≥ 2 s.
         → If G6 (host broadcast on OnPlayerEnteredRoom) ALSO fails: callback registration broken.
         → If broadcast works but request doesn't: code-2 event specifically blocked (very unusual).

G6 FAIL → Host received request but didn't send response.
         → `_hostRelayCodeForBroadcast` was null when request arrived.
         → Confirm `relay_host_ready` fired before G5's request_received timestamp.

G7 FAIL → Joiner timed out waiting for code.
         → If G5+G6 both PASS but G7 still fails: response didn't reach joiner's OnEvent.
         → Likely AddCallbackTarget timing on Quest. Try moving registration to Start().
         → Confirm `relay_join_code_event_timeout` appears at exactly 8 s — if earlier, bug.

G8 FAIL → Joiner got the code but Unity Relay rejected the JoinAllocation.
         → Check code_length from G7. Real codes are exactly 6 characters.
         → Check Unity Dashboard for the allocation matching host's relay_alloc_succeeded timestamp.

G9 FAIL → Relay handshake OK but NGO didn't connect.
         → Look for unity_error around the timestamp.
         → Confirm NetworkManager.Singleton.IsHost on host side.

G10 FAIL (Axis A hardening NOT landed) → Expected. Document the disconnect-detected state and move on.

G10 FAIL (Axis A hardening landed) → Reconnect path broken on real Quest.
         → Inspect VoiceBootstrap reconnect logic.
```

## What success looks like

A clean L2 run produces these timing shapes (approximate, ±2 s):

```
Editor log (timeline from H press):
  +0.0  host_pressed mode=Relay room=A
  +0.1  relay_host_attempt room=A
  +0.5  relay_alloc_succeeded
  +0.5  voice_room_join_attempt room=A
  +1.2  voice_joined room=A
  +1.3  relay_host_ready
  (idle until Quest joins)
  +12.0 relay_code_request_received from_actor=2
  +12.0 relay_code_response_sent target_actor=2 queued=true
  +13.5 client_connected id=1 role=host

Quest log (timeline from B press):
  +0.0  join_pressed mode=Relay room=A
  +0.1  relay_join_attempt room=A
  +0.1  voice_room_join_attempt room=A
  +1.5  voice_joined room=A
  +1.6  relay_code_request_sent queued=true
  +2.0  relay_code_response_received code_length=6   ← OR _broadcast_received
  +2.0  relay_join_calling
  +3.5  relay_join_succeeded
  +3.5  client_connected id=0 role=client
```

Total from joiner B-press to `client_connected`: ≤ 5 s on healthy Wi-Fi.

If your numbers are within ±50 % of these and all G1–G9 events are present, that's an unambiguous L2 PASS. Repeat once more (per the plan's "2 runs in a row" gate) and you're cleared for L3 prep.

## Anti-patterns

Things that look like fixes but invalidate the run — do not do these mid-test:

- **Don't restart the Editor between gates.** A restart resets the Photon Voice connection lifecycle; the resulting voice_state timing is different and not directly comparable to the gate expectations above.
- **Don't change the room letter mid-run.** G1–G9 all key off room A. Stick to defaults.
- **Don't toggle Mode mid-session.** Backlog row 4.102 — mode-toggle doesn't tear down session. You'll accumulate orphan Relay allocations and the next run will have stale Photon-side state.
- **Don't claim PASS based on the in-VR state text.** The state text is best-effort summary; the JSONL is ground truth. "Waiting for friend" on the host can show even if Plan B's verify failed silently.
- **Don't iterate without log preservation.** Before re-running, pull the previous run's logs and stash them under `quest-logs/<descriptive-name>/` — otherwise the rotation will eat them if you do many runs.
- **Don't fix things on assumption.** If G5 fails, do not "fix" by editing C1 — confirm L0 still passes against your current commit first. Code drift between runs is the silent killer of verification ladders.
- **Don't skip G3 because it's "just a timing thing".** Reverse-order G3 catches the case where someone refactored `StartHost` and broke the host-broadcast precondition (host alloc must complete before voice room join, so the code is published before joiner asks).
- **Don't test L2 if L0 hasn't been re-run after the last commit that touched `VoiceBootstrap` or `NetworkBootstrap`.** Per the plan's monotonicity rule.
