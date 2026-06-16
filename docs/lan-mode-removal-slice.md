---
title: LAN mode removal slice
description: Removes LAN ("Same Wi-Fi") as a user-facing networking path. CampfireVR is now Internet/Relay-only. The Mode enum, serverAddress/port fields, LAN host/join branches, mode toggle UX (Y short-tap + M key), ConfigureLanTransport helper, mode display in OnGUI, and the "PRESS Y TO SWITCH MODE" tutorial line are all gone. No NAT-traversal / mDNS / LAN discovery fallback was added — same-LAN failures stay as documented dev/test-environment traps with the existing workarounds (Quest on hotspot for L2, two headsets on separate networks for L3).
category: networking
status: shipping
last_updated: 2026-06-16
sections:
  - What we did
  - Why
  - Files changed
  - User-facing behavior changes
  - What we deliberately did not do
  - Verification
  - Backlog rows closed
  - What remains LAN-adjacent
---

# LAN mode removal slice

## What we did

Deleted LAN as a user-facing networking path. CampfireVR is now Internet/Relay-only with no mode toggle, no LAN code paths, and no LAN-related UX.

Specifically removed from `UnityProject/Assets/Scripts/Networking/NetworkBootstrap.cs`:

- `public enum Mode { Lan, Relay }` — enum gone entirely.
- `[SerializeField] private string serverAddress = "127.0.0.1";` — field gone.
- `[SerializeField] private ushort port = 7777;` — field gone.
- `[SerializeField] private Mode mode = Mode.Lan;` — field gone.
- `public Mode CurrentMode` and `public string CurrentModeLabel` properties — gone.
- `private const string LanRoomName = "lan-campfire";` — gone.
- `void OnLeftSecondary()` — gone (handled Y short-tap → ToggleMode).
- `async void ToggleMode()` — gone entirely.
- `bool ConfigureLanTransport()` — gone.
- The `M` key handler in Editor `Update()` — gone.
- The LAN early-return branches at the top of `StartHost` and `StartClient` — gone.
- The `if (mode == Mode.Lan)` check in `OnRightSecondary` (B button) for the `_lastAction` text — gone.
- The `mode == Mode.Relay` guard on the preflight in `StartHost`/`StartClient` — never needed (preflight always runs now since LAN is gone).
- The `Mode: {mode}` `OnGUI` text label and its `_modeStyle` GUIStyle — gone.
- `("mode", mode.ToString())` fields from log events `network_bootstrap_ready`, `host_pressed`, `join_pressed`, `stop_completed`, `ngo_transport_failure_detected` — gone.
- `using Unity.Netcode.Transports.UTP;` import (was only for `ConfigureLanTransport`).
- The release-edge ToggleMode call inside `PollYLongPress` — gone (Y short-tap is now unbound).

Removed from `UnityProject/Assets/Scripts/UI/TutorialOverlay.cs`:

- `string modeLine = $"mode · {_net.CurrentModeLabel}"` in the Idle phase rendering, and the `+ modeLine +` concatenation that put it on screen.
- The `"Y       mode\n"` line from `BuildLegend(letter)`.

Removed from `UnityProject/Assets/Scenes/CampfireRoom.unity`:

- The `PRESS Y TO SWITCH MODE` stanza from the TutorialPanel `m_Text` field.
- (Left as-is: the serialized `serverAddress: 127.0.0.1` / `port: 7777` / `mode: 0` field values on the NetworkBootstrap component. These are now orphan refs that Unity tolerates silently — they'll get cleaned up automatically the next time the scene is opened and saved in the Editor.)

Removed from `docs/debug-logging.md`:

- `mode_changed` event row.
- `mode_toggle_stop_first` event row.
- `lan_host_attempt` / `lan_host_ready` / `lan_host_failed` event row.
- `lan_join_attempt` / `lan_join_started` / `lan_join_failed` event row.
- "Initial mode, …" → "Initial room letter, …" wording on `network_bootstrap_ready`.
- "H/C/X/**M**/L" → "H/C/X/L" in the `editor_key` row.

## Why

LAN mode had two consistently observed failure modes that wasted real two-headset session time:

1. **`serverAddress: 127.0.0.1` trap** (backlog row 4.101): the scene shipped with LAN as the default mode and `serverAddress` defaulting to localhost. A tester pressing host on LAN was hosting on their own loopback, so cross-device connection was structurally impossible. Verified by the 2026-06-15 Henrik+Johan session where both testers spent 8+ minutes confused before they thought to toggle to Internet mode.
2. **Same-LAN hairpin-NAT failure** (backlog row 4.108): even after switching to Relay mode, if both peers were on the same consumer router, Unity Relay's join-allocation traffic couldn't be looped back through the router — `Transport failure! Relay allocation needs to be recreated`. Verified by the 2026-06-15 L2 same-LAN run.

Together these two cover essentially every realistic dev-LAN deployment. The product use case is remote fika — two people in different locations — not same-room same-Wi-Fi local play. LAN mode added no value to that use case and consistently hurt tester onboarding.

The plan doc's verification ladder already named L3 as "two real Quests on separate networks" — production-equivalent by definition, never on the same LAN. So removing LAN doesn't affect the ladder, only simplifies what gets built and tested.

## Files changed

| File | Change | LOC |
|---|---|---|
| `UnityProject/Assets/Scripts/Networking/NetworkBootstrap.cs` | Net deletion of LAN code paths, mode toggle, mode display, mode log fields | −125 / +13 |
| `UnityProject/Assets/Scripts/UI/TutorialOverlay.cs` | Removed `CurrentModeLabel` refs + "Y mode" legend line | −5 / +3 |
| `UnityProject/Assets/Scenes/CampfireRoom.unity` | Removed "PRESS Y TO SWITCH MODE" from TutorialPanel | −2 / 0 |
| `docs/debug-logging.md` | Removed 4 event rows + 2 wording fixes | −5 / +2 |
| `docs/networking-stabilization-plan.md` | Updated "Out of scope" line | −1 / +1 |
| `docs/post-sprint-backlog.md` | Struck through 3 closed rows + frontmatter update | −4 / +4 |
| `docs/lan-mode-removal-slice.md` (this file) | New slice doc | new |

## User-facing behavior changes

Before:
- Top-of-Editor-window: "Mode: Lan" / "Mode: Relay" status label.
- In-VR tutorial panel: "PRESS Y TO SWITCH MODE" line.
- In-VR tutorial overlay (via TutorialOverlay): "mode · Same Wi-Fi" / "mode · Internet" idle line + "Y mode" legend entry.
- Y short-tap on the left controller toggled LAN ↔ Relay.
- M key in Editor toggled LAN ↔ Relay.
- Pressing H on LAN mode bound to `127.0.0.1:7777` via `ConfigureLanTransport` and joined Photon voice room `lan-campfire`.

After:
- No mode label anywhere in UI.
- No mode-related lines in tutorial panel or overlay.
- Y short-tap on the left controller does nothing (long-press still stops the session).
- M key in Editor does nothing (H / C / X / L are the remaining shortcuts).
- Pressing H always allocates Unity Relay and joins Photon voice room A.

There are no user-visible additions. The user experience is strictly subtraction: one fewer mode, one fewer button binding, one fewer screen label, one fewer line in the tutorial.

## What we deliberately did not do

Per the slice scope:

- **No new networking strategies**: no NAT traversal, no mDNS, no LAN discovery, no hole-punching, no fallback chain. Same-LAN failures stay as documented dev/test environment traps — the existing workarounds (Quest on phone hotspot for L2 verification, two real Quests on separate networks for L3 production-equivalent testing) are sufficient and already documented in `docs/networking-stabilization-plan.md` and `docs/l2-editor-quest-test.md`.
- **No `#if UNITY_EDITOR` debug-only LAN gate**: the user's request explicitly asked whether such gating was worth keeping. The answer was no. LAN was already not used for any verification path — L0 is static, L1 is editor-only stubbed, L2 is Editor+Quest via real Relay, L3 is two Quests via real Relay. There is no remaining workflow that needed a LAN escape hatch.
- **No scene save to clean up orphan serialized fields**: `CampfireRoom.unity` still contains the serialized `serverAddress: 127.0.0.1` / `port: 7777` / `mode: 0` values on the NetworkBootstrap component. Unity tolerates orphan values silently — they'll get pruned automatically the next time the scene is opened and saved in the Editor. Manually editing the scene YAML to remove them was deemed higher risk (formatting / GUID errors) than leaving harmless orphans.
- **No new tutorial content**: the tutorial panel and overlay just got shorter. No new "Internet only" copy added — the absence of choice is the message.
- **No removal of the `_lastAction` debug string mechanism**: still useful for in-VR diagnostics and unrelated to mode.

## Verification

1. **L0 audit**: re-ran spot checks against the post-slice state. Event codes still at expected positions (`VoiceBootstrap.cs:51-53`), `PublishRelayCodeToJoiners` / `WaitForRelayCodeEventAsync` / `ClearPublishedRelayCode` wiring intact in NetworkBootstrap. C1 untouched.
2. **Batchmode build**: `./scripts/build-quest.sh` → `[build] OK · 117M · CampfireVR-v0.1.2-session-fix-20260616-1506.apk`. No compile errors. No warnings about missing types or unused fields.
3. **Grep clean**: zero remaining references to `Mode.Lan`, `Mode.Relay`, `LanRoomName`, `serverAddress`, `ConfigureLanTransport`, `ToggleMode`, `CurrentMode`, `CurrentModeLabel`, `mode_changed`, `mode_toggle_stop_first` in any `.cs` file under `Assets/Scripts/`.

Not run:
- Headset L2 verification with the new APK. The L2 hotspot path was last green at commit `e57986c` (2026-06-15 10:48). Since this slice only removes code paths that L2 never exercised in the Relay flow, the L2 path is unaffected by these changes — but a real-Quest re-verification is worth doing the next time the user puts the headset on.

## Backlog rows closed

- `4.101` LAN default `serverAddress: 127.0.0.1` trap — closed (the trap no longer exists).
- `4.107` Product simplification: remove LAN as user-facing path — closed (this slice).
- `§3` Room / mode explanation — closed (no longer relevant).

Open rows that remain LAN-adjacent (kept open for future awareness, not blockers):
- `4.108` Same-LAN hairpin-NAT trap for Editor↔Quest L2 testing — kept open as a dev-environment note. The workaround (Quest on hotspot) is documented and verified.

## What remains LAN-adjacent

Nothing in the runtime code path. The serialized scene file still contains harmless orphan values for `serverAddress` / `port` / `mode` on the NetworkBootstrap component — these will be cleaned up automatically on the next Editor scene save. No follow-up required.
