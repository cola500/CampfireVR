---
title: Post-sprint backlog
description: Consolidated deferred items, follow-up tasks, and headset-validation work captured from the App Lab compliance sprint. Living document — items move out as they land or get re-prioritised.
category: meta
status: living
last_updated: 2026-06-16 (added A3-style failure: suspected Photon Voice region mismatch — region logging shipped, FixedRegion held as option pending observed mismatch)
sections:
  - About this backlog
  - Conventions
  - 1. Headset validation tasks
  - 2. Technical follow-ups
  - 3. UX follow-ups
  - 4. Multiplayer / voice follow-ups
  - 5. Visual / environment follow-ups
  - 6. Store / App Lab later
  - Where each item came from
---

# Post-sprint backlog

Captured during the App Lab compliance sprint (commits `b871af7` through `034fd14`, 2026-05-19) so deferred work doesn't get lost. Each section corresponds to a category of work that can be picked up independently — many items pair naturally with a single headset session.

## About this backlog

This is **not** a roadmap (the strategic "what next" lives in `docs/roadmap.md`). This is the **operational** to-do list of small concrete items that came up during the sprint and were intentionally deferred to keep slices focused.

Nothing in this doc is being worked on right now. Items move out either when they land (into a new slice doc + the relevant audit / sprint-plan strikethrough), or when we explicitly decide a row is no longer worth doing.

## Conventions

For each item:

- **Priority** — `High` (blocks the next deliverable), `Medium` (should ship before the App Lab submission), `Low` (nice-to-have polish)
- **When** — `Before two-headset test` / `Before App Lab` / `Later polish`
- **Note** — one short line for context

Status conventions follow the rest of the repo: when an item lands, link the commit + the slice doc that landed it; when an item is killed, strike through with a one-line reason.

---

## 1. Headset validation tasks

Items that can only be confirmed wearing a Quest, ideally with a second tester. Most pair into one ~60-minute two-headset session.

| Item | Priority | When | Note |
|---|---|---|---|
| **Slice 4 voice mute** — open Meta menu mid-session, confirm via partner that no audio reaches them and via post-session log that `app_focus_lost` fired with `voice_transmit_muted: true` | High | Before two-headset test | Implementation landed `034fd14`; only headset verification remains. Cited in `docs/privacy-policy-draft.md` open question #8. |
| **Slice 4 focus regain** — close Meta menu, confirm voice resumes without re-join and `app_focus_gained` fires with `voice_transmit_restored: true` | High | Before two-headset test | Pairs with above; same session. |
| **Avatar head pose during partner's Meta menu** — partner opens menu, observe whether their head freezes naturally or drifts (XRHeadTracker is *not* frozen on focus loss by design — see Slice 4 conservative scope). | High | Before two-headset test | If drift looks bad, file follow-up to gate XRHeadTracker.LateUpdate on `AppLifecycle.IsFocused`. |
| **Recenter button through seat-relative rig** — press right A while seated and offset; verify the rig snaps to expected origin | Medium | Before App Lab | VRC.Quest.Functional requirement; flagged `PASS (pending physical-headset re-verification)` in the audit. |
| **Boot time < 4 s to head-tracked graphics** | Medium | Before App Lab | VRC requirement; audit flagged `UNKNOWN`. Use OVRMetricsTool or `adb logcat` timestamps. |
| **30-min connected soak test** — host + join + voice + 30 minutes of mixed activity, watch for crashes, memory growth, thermal throttling | Medium | Before App Lab | Audit's `UNKNOWN` for VRC.Quest.Functional crash-free criteria. Procedure now lives at `docs/soak-test-checklist.md` (Slice 6); item stays in backlog until the procedure has actually been *executed* on two headsets. |
| **Frame-time at 90 Hz during connected session** — perf overlay enabled, watch for missed frames during worst case (head + hand sync + voice + Relay) | Medium | Before App Lab | Audit's `UNKNOWN` for VRC.Quest.Performance. Procedure now in `docs/performance-checklist.md` (Slice 5) + integrated into `docs/soak-test-checklist.md` (Slice 6); item stays in backlog until actually executed. |
| **Hands / mittens final feel** — confirm the procedural mitten mesh reads "warm + cozy" rather than "off". Check thumb direction, cuff colour, palm size | Low | Later polish | From `docs/cozy-mittens-slice.md`. Subjective — wait until everything else is right and decide if mittens need a pass. |
| **Remote avatar readability** — partner head colour + scale, hand alignment opposite the campfire | Low | Before two-headset test | Already audited in `docs/remote-avatar-sanity.md`; quick visual re-check after any rig changes. |
| **Dog / materials / environment sanity** — dog SkinnedMesh, oak wind subtlety, grass density, stone grounding — all look right in actual headset | Low | Before two-headset test | Catch-all; mostly already validated, just a "nothing regressed" pass. |

## 2. Technical follow-ups

Stuff that can be done at the desk (no headset needed) but is non-trivial work.

| Item | Priority | When | Note |
|---|---|---|---|
| **Generate the actual release keystore** — `keytool -genkey` per `docs/release-keystore.md`, store at `~/.keystores/CampfireVR-release.keystore`, set the four `CAMPFIREVR_KEYSTORE_*` env vars in `~/.zshrc`, back up to encrypted SSD + password-manager attachment | High | Before App Lab | Slice 2 left this as the one-time manual step. `apksigner verify --print-certs` on a build should then show your DN, not `CN=Android Debug`. |
| **Privacy policy verification — 10 open questions** | High | Before App Lab | Each of the 10 open questions in `docs/privacy-policy-draft.md` (Photon retention, Unity retention, permission audit, hosting URL, legal review, etc.) must be answered before the draft becomes a hosted policy. |
| **Android manifest permission audit** — `aapt2 dump permissions Builds/CampfireVR-latest.apk`, verify every declared permission is justified | High | Before App Lab | Privacy draft open question #5. Expected: RECORD_AUDIO, INTERNET, ACCESS_NETWORK_STATE, Oculus-specific. Document any surprises. |
| **Package identifier decision — keep `com.unitymcplab.campfireroom`** | Medium | Before App Lab | `docs/project-rename-notes.md` already documents this as a deliberate hold. Re-read once more before submission day to confirm we still want the legacy id (changing it forces every tester to uninstall first). |
| **Headset log pull test from two devices** — run `scripts/pull-quest-logs.sh --zip` against two connected Quests in sequence, verify the zips land at sensible names and are non-empty | Medium | Before two-headset test | One-time check that the post-test debug-log workflow scales beyond a single device. |
| **Friend package smoke test** — run `./scripts/package-friend-test.sh`, then on a fresh "tester" install do MQDH drag-and-drop following the produced README, confirm the install path matches the doc | Medium | Before App Lab | The script is well-tested mechanically but the tester UX still hasn't been re-walked since the rename. |
| **Photon Voice 2 EULA vs Meta distribution** — confirm Photon's terms allow Store-distributed apps | Medium | Before App Lab | Audit's "Things I deliberately did NOT verify". |
| **Unity Relay free-tier ToS vs Store distribution** — same question | Medium | Before App Lab | Same source. Likely allowed but worth a 5-minute check. |
| **XRHeadTracker pose-freeze on focus loss** — gate `LateUpdate` pose writes on `AppLifecycle.IsFocused` | Low | Conditional | Only do this if headset validation (item 1.3 above) shows the unfrozen head looks bad to the remote partner. Otherwise leave alone — head stays at last-tracked pose, which reads cleanly. |
| **`SystemInfo.deviceName` content check** — log a session on a clean install and inspect what `deviceName` resolves to | Low | Before publishing privacy policy | Privacy draft open question #9. If it's a generic string we're fine; if Meta has started exposing user-set device names, the privacy policy needs to mention it. |
| **`Forest` orphan GameObject cleanup** — `CLAUDE.md` notes a leftover empty GameObject from a pre-cleanup slice at scene root | Low | Later polish | Safe to delete; cosmetic only. Quick MCP edit. |

## 3. UX follow-ups

Polish that improves "how does this feel" without changing what the app does.

| Item | Priority | When | Note |
|---|---|---|---|
| **In-VR status text for paused / stopped / session state** | Medium | Before App Lab | Optional from Slice 4 spec, deferred. Add a subtle world-space line ("Session paused", "Focus restored") via NetworkBootstrap's OnGUI overlay or a TextMesh. Decision pending headset test of current silent behaviour. |
| **Recovery messaging after long-press Y Stop** | Medium | Before App Lab | Current `_state = "Stopped session"` is functional but could read warmer ("Stepping back from the fire — press X to host or B to join again"). Subjective; not blocker. |
| **Onboarding / tutorial polish** — refine the world-space TutorialPanel copy + layout | **High** | Before next two-headset test | Current copy is dev-style ("X host, B join, Y mode"). Re-write in tester-friendly language; consider arrows or icons next to button references. **Bumped Low → High after 2026-06-15 Henrik+Johan session**: both testers spent 8 minutes mashing buttons without realising one needs to host and the other needs to join — the tutorial does not make this clear enough. |
| **Room / mode explanation** — clarify "Internet" vs "Same Wi-Fi" to a non-developer reader | **High** | Before next two-headset test | Currently a small line at the panel bottom. Could be a one-time tutorial overlay shown on first launch. **Bumped Low → High after 2026-06-15 session**: both testers started on Same Wi-Fi (the current default) and spent minutes confused before either thought to toggle. See also the related "LAN default `127.0.0.1` trap" item in section 4. |
| **Controller / help overlay** — a "press both grips for help" overlay listing controls | Low | Before App Lab | No help overlay today. Particularly useful for first-time testers who haven't read INSTALL.md. |
| **Boot fade-in** — currently the app cuts to scene; a 0.5–1 s fade from black would feel more polished | Low | Later polish | Cheap to add; pairs with Comfort Rating self-cert. |

## 4. Multiplayer / voice follow-ups

Edge cases around connection lifecycle that aren't covered by Slice 4's focus handling.

| Item | Priority | When | Note |
|---|---|---|---|
| **Reconnect after Quest sleep / wake** — what happens to NGO + Relay + Photon Voice if a player puts the headset down for 5 minutes then puts it back on? | High | Before two-headset test | Probably the session is torn down by Quest (long pause sends the app to background and eventually kills the process). Slice 4 doesn't handle a forced disconnect — verify the long-press-Y Stop is still reachable on resume. |
| **Stop / rejoin edge cases** — after long-press Y Stop, can the user immediately re-host? Re-join the same room? What if the partner has already re-hosted under the same letter? | High | Before two-headset test | Code path exists (`Stop` resets `_busy` etc.) but specific timing patterns haven't been stressed. |
| **Voice reconnect edge cases** — Photon master disconnects briefly (Wi-Fi blip, ISP hiccup); does VoiceBootstrap reconnect cleanly? | Medium | Before App Lab | `voice_state` events would tell us; need a real session with simulated network drop. |
| **Join-while-hosting regression test** — verify the existing guard (`OnRightSecondary` early return) still fires after the rename / Slice 2-4 changes | Medium | Before two-headset test | Was the original fix for a 2026-05-16 regression. Quick to verify in a test session. |
| **Stop teardown error paths** — each `try/catch` in `Stop()` swallows errors with a `stop_step_failed` log. Have we ever actually seen one fire? | Low | Before App Lab | Forensic look at `quest-logs/` archive next time we do a soak test. |
| **Photon Voice 2 NRE on scene-dirty** — pre-existing Editor-only bug per CLAUDE.md. Document for any future Photon Voice version bump in case it's been fixed upstream | Low | Later polish | No action; just a known. |
| **LAN default `serverAddress: 127.0.0.1` trap** — `NetworkBootstrap.serverAddress` defaults to localhost, so a tester who picks Same Wi-Fi mode (the current scene default!) and presses host is hosting on their own loopback. The remote tester is then NAT'd to their own loopback too — connection can never succeed cross-device. Voice still routes via Photon's cloud room (`lan-campfire`) so they may *hear* each other but never see each other as avatars. Verified in 2026-06-15 Henrik+Johan session — Johan's log shows `lan_join_attempt address=127.0.0.1` followed by `unity_error: "Failed to connect to server"` at 14:43:08. **Fix options:** (a) hide Same Wi-Fi mode in the UI entirely and ship as Internet-only; (b) show a clear warning when LAN is selected; (c) default the scene to `mode = Relay` instead of `Lan`. Option (c) is the cheapest one-line change. | **High** | Before next two-headset test | Cross-references the UX onboarding bump in section 3. |
| **Mode-toggle doesn't stop the active session** — pressing left-Y mid-session flips the `Mode` enum but doesn't tear down NGO / Relay / Voice. Result: orphan sessions accumulate as the tester re-presses host in the new mode. Verified in 2026-06-15 session: Henrik created 3 Relay sessions in sequence without ever Stop:ing the previous one; Johan toggled mode 41 times. **Fix options:** (a) make `ToggleMode()` call `Stop()` first if in a session; (b) hide / disable the mode toggle while in a session; (c) show a confirmation overlay. Option (a) matches the existing long-press-Y Stop's teardown semantics so it's the most consistent fix. | **High** | Before next two-headset test | Cumulative orphan sessions also burn Relay free-tier allocations. |
| **Photon Voice connection-state hardening (Axis A)** — Henrik's 2026-06-15 joiner attempt produced `JoinOrCreateRoom can't be sent because peer is not connected` because `NetworkBootstrap.StartClient` does not verify `_voice.Client.State == ClientState.ConnectedToMasterServer` before calling `JoinRoom`. Headset focus loss / sensor off-on can disconnect Photon silently. Plan: add `VoiceBootstrap.WaitForConnectedAsync(timeout)`, gate host/join entry on it, reconnect on `app_focus_gained` if state is `Disconnected`. New log events: `voice_reconnect_attempt` / `_succeeded` / `_failed`, `relay_host_voice_offline` / `relay_join_voice_offline`. Full design in `docs/networking-stabilization-plan.md` §"Axis A — connection-state hardening". | **High** | Before next two-headset test | Blocks every other Internet-mode fix — until Axis A is solid, Axis B verdicts are noise. Must land before harness G10 is meaningful. |
| **Local multiplayer verification harness (Editor-as-peer, Option A)** — Johan has one Quest; Henrik retest is gated on local proof. Recommendation in `docs/networking-stabilization-plan.md`: use the existing Editor key shortcuts (H/C/X/M in `NetworkBootstrap.cs:159–166`) and run Editor as one peer + Quest as the other. Same Photon Voice cloud, same Unity Relay. Gates G1–G10 in the plan define pass criteria. Optional follow-up: minimal Editor menu `Tools/Networking/Run As Host`/`Run As Joiner` to remove manual button presses (only build if friction warrants it — YAGNI default). | **High** | Before next two-headset test | No new networking code; the runtime path already works in Editor. Friction is purely "press two things at once" which a 30-line Editor menu can remove if needed. |
| **C1 request/response validation (local first)** — Plan C1 is implemented (`VoiceBootstrap.cs:33–35, 270–401`) but has never been observed end-to-end because every 2026-06-15 attempt failed in Axis A before reaching C1. Plan: after Axis A hardening lands, run the Editor-as-peer harness to observe G4–G7 (`relay_code_request_sent` / `_received` / `_response_sent` / `_response_received`). If those four events fire cleanly across two runs, C1 is verified and we ship as-is. If they don't, add joiner-side retries (2–3 sends spaced 1.5 s) inside `WaitForRelayCodeEventAsync` and move `AddCallbackTarget` from `Update()` to `Start()`. Design review in `docs/networking-stabilization-plan.md` §"C1 design review". | **High** | Before next two-headset test | Decision deferred until after local harness run — we don't tune what we haven't measured. |
| **Two-headset retest preconditions** — Don't ask Henrik for another install until: (1) G1–G9 pass locally twice in a row; (2) Axis A hardening landed and G10 passes; (3) backlog row 4.101 (LAN `127.0.0.1` trap) closed by flipping scene default to `Relay`; (4) backlog row 4.102 (mode-toggle-doesn't-stop-session) closed; (5) commit hash + APK filename + expected per-step state documented. Source: `docs/networking-stabilization-plan.md` §"Acceptance gates for next remote test". | **High** | Before next two-headset test | Gates every other "Before next two-headset test" item — if we skip these, the next Henrik session produces ambiguous logs again. |
| **Product simplification: remove LAN ("Same Wi-Fi") mode as a user-facing path** — CampfireVR's primary use case is two people in different physical locations (remote fika). LAN mode exists as developer convenience but consistently traps testers: backlog row 4.101 (`serverAddress: 127.0.0.1` trap — Henrik+Johan 2026-06-15 wasted 8 min on this) plus the same-LAN hairpin-NAT row below (Unity Relay also fails when both peers share a router). The two known LAN-mode failure modes together cover essentially every realistic dev-LAN deployment. Recommendation: ship Internet-only and either delete `Mode.Lan` entirely or gate it behind `#if UNITY_EDITOR` / `Development Build` as a debug path. **Explicit anti-pattern**: do **NOT** attempt to fix same-LAN connectivity with complex production code (custom LAN discovery, NAT hole-punching, fallback paths, mDNS, etc.). The acceptable development workarounds already exist and are documented: Quest on phone hotspot for L2 (Editor + 1 Quest, verified working 2026-06-15), two real headsets on separate networks for L3 (production-equivalent by definition). Scope when picked up: remove the `Mode` enum's `Lan` value, the `serverAddress` / `port` fields on `NetworkBootstrap`, the `M`-key / Y-tap mode-toggle UX, and the LAN paths in `StartHost`/`StartClient`. Update tutorial copy and `docs/networking-stabilization-plan.md` §"Out of scope" (which already notes "we're not investing in LAN — it's a developer convenience"). Cross-refs: row 4.101, same-LAN hairpin row below. | Medium | Before App Lab | Strategic simplification, not a blocker. Closes the "wrong-mode confusion" UX issue (backlog §3 "Room / mode explanation" bump) by removing the choice entirely. ~1 evening when picked up. |
| **A3-style failure: suspected Photon Voice region mismatch** — verified 2026-06-16 08:10 (force-stop A3, hotspot) and 07:57 morning run: Quest pressed B and joined "room A" but `relay_code_request_received from_actor=1` fired on Quest itself — meaning Quest was the master client of its own "room A" instance, separate from Editor's. Editor in parallel showed zero `OnPlayerEnteredRoom` callbacks. The two peers ended up in different physical Photon Voice rooms despite both calling `OpJoinOrCreateRoom("A")`. Most likely cause: Photon's `BestRegionSummary` picked different regions for Editor (Mac on Wi-Fi) and Quest (on hotspot) — and "room A" is region-scoped. Intermittent: A1+A2 worked same-room on the same setup, A3 didn't. **Diagnostic added 2026-06-16 (commit `24ca6d2`)**: `voice_region_selected` log event fires once per process at `ConnectedToMasterServer`, includes `region` (Photon `CloudRegion`), `app_version`, `platform`. Compare both peers' logs next time the failure happens — if `region` differs, root cause is confirmed. **FixedRegion fix held as the next step only after confirmation**: setting `FixedRegion = "eu"` in `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` is a config change (no C#) that locks both peers to the same region. Tradeoff: increases voice latency for users far from the chosen region. | High | Before next two-headset test | Cross-ref: A3 failure logs at `quest-logs/20260616-081200/campfirevr-log-20260616-080715.jsonl` + Editor `campfirevr-log-20260616-080832.jsonl`. Distinct from 4.108 (hairpin NAT) — different symptom, different layer. |
| **Same-LAN hairpin-NAT trap for Editor↔Quest L2 testing** — when host (Editor on Mac) and joiner (Quest) are on the same Wi-Fi, Unity Relay's join-allocation handshake fails with `Transport failure! Relay allocation needs to be recreated, and NetworkManager restarted.` on the host and `relay_join_failed` on the joiner ~8 s after `relay_join_calling`. Verified 2026-06-15 with the same C1 APK (`ae40d6d`): G8 failed twice on same-LAN, passed twice in a row on 4G hotspot (Mac stayed on Wi-Fi, Quest on cellular). **Workaround for ongoing local L2 testing**: put the Quest on a phone hotspot while keeping Mac on Wi-Fi. **No effect on two-headset production tests** (Henrik on his own Wi-Fi, Johan on his) since the peers are on separate WANs by definition. **No code change required** — this is consumer-router hairpin-NAT behaviour, not a Unity Relay / NGO / C1 bug. C1's G1–G7 events fired green on both runs (`relay_code_request_sent` / `_received` / `_response_sent` / `_response_received`, `code_length=6`) — the failure was strictly downstream in `UnityTransport`. Logged so future-Johan / future-Claude doesn't re-debug from scratch. | Low | Document only | Evidence: LAN fail = `quest-logs/20260615-201927/campfirevr-log-20260615-201706.jsonl` (Quest, G8 fail × 2) + the matching Editor log with two `unity_error: "Transport failure! Relay allocation needs to be recreated"` events. Hotspot pass = `quest-logs/20260615-203050/campfirevr-log-20260615-202723.jsonl` (Quest, G8 pass × 2, zero `unity_error`). |
| **`rc` discovery via Photon events (Plan C1 — currently shipping)** — Photon Voice's `LoadBalancingClient` does not propagate **any** custom properties (room or player) to new joiners. Verified across three independent failure modes on 2026-06-15: (1) room property post-join (commit `702f7ca`, host verify succeeded but joiner saw `rc` missing — johan3/henrik3); (2) room property at creation + `CustomRoomPropertiesForLobby = ["rc"]` (Experiment A — johan5/henrik5, joiner still missing 90 s after host set it); (3) host LocalPlayer custom property read by joiner from `room.GetPlayer(MasterClientId).CustomProperties` (Plan B, commit `7b333b9` — johan6/henrik6, host's local `verify_succeeded` confirmed property locally set, joiner still got `relay_join_master_property_missing` after 8 s). Photon Voice's voice-only state sync excludes all custom metadata by design. **Plan C1 (shipping)**: drop properties as the discovery channel; use Photon **events** instead via `OpRaiseEvent`. Three event codes: (1) host broadcasts to newly joined player via `OnPlayerEnteredRoom` callback, (2) joiner sends a request to the master client on room entry as a backup, (3) host replies with a targeted response. First-event-wins on the joiner side; properties retained only as a host-side diagnostic (`relay_host_player_property_verify_succeeded` confirms the host can write its own player state — useful signal independent of the broadcast path). New events: `relay_host_event_broadcast`, `relay_code_request_sent` / `request_received` / `response_sent` / `response_received` / `broadcast_received` / `relay_join_code_event_timeout`. Photon events propagate by design (otherwise voice wouldn't work), so this should be the robust path. **If C1 also fails**, the next fallback is to drop Photon as the discovery channel entirely and pivot to Unity Multiplayer Service's session-metadata API. **Update 2026-06-15**: superseded as a standalone row by the three "Local harness", "Axis A hardening", and "C1 validation" rows above — kept here for evidence-chain history only. Go-forward owner is `docs/networking-stabilization-plan.md`. | **High** | Before next two-headset test | Evidence chain: `henrik2/johan2` (fire-and-forget bug), `henrik3/johan3` (host verify path), `henrik5/johan5` (Experiment A room-prop-with-lobby), `henrik6/johan6` (Plan B player-prop). C1 awaiting first headset test — now blocked behind local harness + Axis A. |

## 5. Visual / environment follow-ups

Scene polish — none of these are blockers but they round out the "feels finished" bar that a tester would notice.

| Item | Priority | When | Note |
|---|---|---|---|
| **Oak / forest wind comfort check** — verify the ALP wind settings (WindPulse 0.10, WindTurbulence 0.12, WindRandomness 0.15) still read as "subtle breeze" rather than "storm" in headset | Low | Later polish | `docs/tree-wind-slice.md` documents the chosen values; this is a re-verify after any future scene change. |
| **Grass / flower density check** — confirm GrassBreakup tufts and FlowerCluster_* placements still read as natural, not patterned | Low | Later polish | Eye-test in headset; from `docs/grass-breakup-slice.md` + `docs/oak-and-flowers-slice.md`. |
| **Dog placement / material check** — confirm DogCompanion still sits naturally beside StoneSeat_A with the warm-brown DogCoat material rendering correctly | Low | Later polish | From `docs/dog-companion-slice.md`. URP/BiRP material conversion has historically been fragile here. |
| **Stone grounding check** — confirm `Tools/Quest Setup/Ground Stones` still produces varied depths that read as "placed on dirt" not "extruded blocks" | Low | Later polish | From `docs/stone-seats-slice.md`. Re-runnable helper. |
| **Controller / mitten polish** — finger bulge size, cuff colour, thumb direction in headset | Low | Later polish | From `docs/cozy-mittens-slice.md`. Subjective. |
| **38 pine trees — vertex count measurement** | Medium | Before App Lab | Slice 5 risk-item; tree_01 from Mountain Terrain pack hasn't had its vertex count measured. Could be a major GPU cost depending on LOD setup. |
| **FireLight shadow cost** — already moved Soft → Hard in `docs/quest-validation-pass.md`; re-verify still cheap on Quest 3 | Low | Before App Lab | Pair with Slice 5 perf measurement. |
| **Grass card transparent overdraw** | Medium | Before App Lab | Cross-quad cards with Standard-Cutout shader — overdraw cost can spike if the camera goes near them. Worth a perf-overlay frame during a typical seated session. |

## 6. Store / App Lab later

Marketing-and-dashboard work that's explicitly out of scope for the technical compliance sprint. None of this is blocking the next two-headset test; all of it is blocking actual App Lab submission.

| Item | Priority | When | Note |
|---|---|---|---|
| **App icon** — 512x512 PNG (24-bit) | High | Before App Lab | First impression in the Store and the Quest's Unknown Sources list. |
| **Hero cover** — 3000x900 PNG | High | Before App Lab | Wide banner at the top of the Store page. |
| **Cover assets** — landscape 2560x1440, square 1440x1440, portrait 1008x1440 PNG | High | Before App Lab | Different aspect ratios for different Store surface treatments. |
| **5 screenshots** — 2560x1440 PNG, no duplicates | High | Before App Lab | Shot from the actual built app. Needs a stable two-player session for the not-solo scenes. |
| **Trailer** — MP4 H.264 / AAC, 1080p–2K, 30 s – 2 min + 2560x1440 trailer cover | High | Before App Lab | The hardest single deliverable; needs scripted recording + editing. |
| **Store description** — long-form + short-form + what's-new copy + tagline | High | Before App Lab | Dashboard text fields. Worth drafting alongside the trailer script since they share themes. |
| **Final privacy policy URL** — host the privacy policy at a stable HTTPS URL | High | Before App Lab | Cheapest path is GitHub Pages on `cola500/CampfireVR`. Privacy draft must have its 10 open questions resolved first. |
| **IARC rating questionnaire** | High | Before App Lab | Dashboard form. ~20 min including reading IARC's questions. |
| **Age self-certification** — 13+ (recommended per audit, stays out of COPPA) | High | Before App Lab | Dashboard form. Matches what's already promised in the privacy draft. |
| **Comfort rating** — Comfortable / Moderate / Intense | High | Before App Lab | Dashboard form. Seated experience with no locomotion → likely Comfortable. |
| **Supported devices declaration** — Quest 2 / 3 / 3S / Pro | High | Before App Lab | Coordinate with Slice 5's `TargetQuest*` flag decisions in `OculusSettings.asset`. |
| **App Lab / Horizon Store dashboard setup** — create app entry, fill metadata, upload first APK via MQDH or `ovr-platform-util` | High | Before App Lab | This is the "submission day" sprint; allow a full evening including dashboard discovery time. |
| **Logo asset** — optional transparent PNG up to 9000x1440 | Low | Later polish | Not required; improves Store visual presentation. |
| **`scripts/store-assets/` directory** — keep source files (Affinity / Krita / Blender) for icons + covers under version control so they can be re-rendered | Low | Later polish | From audit's optional list. Useful long-term once we have art assets. |
| **`docs/store-listing-copy.md`** — version-controlled copy of the public app description and what's-new text | Low | Later polish | Same rationale as `store-assets/` — keeps the dashboard answers diff-able. |
| **In-app "report a problem" UI** | Low | Later polish | Not required for 13+ self-cert with private rooms, but useful for tester feedback. |

## Where each item came from

For traceability, the major sources behind this backlog:

- `docs/app-lab-compliance-sprint.md` — slice-level deferred items (keystore generation, headset verification, NGO-during-focus-loss decision, scene perf risk list)
- `docs/meta-store-readiness-audit.md` — all `UNKNOWN` rows, the optional list, the "Things I deliberately did NOT verify" section
- `docs/privacy-policy-draft.md` — the 10 open questions block
- `docs/release-keystore.md` — the manual keytool + backup workflow
- `docs/cozy-mittens-slice.md`, `docs/dog-companion-slice.md`, `docs/oak-and-flowers-slice.md`, `docs/stone-seats-slice.md`, `docs/grass-breakup-slice.md`, `docs/tree-wind-slice.md` — visual polish items
- `docs/remote-avatar-sanity.md` — remote avatar readability check
- `CLAUDE.md` — `Forest` orphan GameObject note, Photon Voice 2 NRE
- The Slice 4 commit (`034fd14`) source-header comments — XRHeadTracker pose-freeze decision
- 2026-06-15 Henrik+Johan two-headset session — `quest-logs/henrik-20260615-144825.zip` + `quest-logs/johan-20260615-145014.zip`. Source for the LAN `127.0.0.1` trap, mode-toggle-doesn't-stop-session, and `relay_join_property_missing` race items in section 4, plus the UX onboarding priority bumps in section 3.
- `docs/networking-stabilization-plan.md` (2026-06-15) — verification-first replacement for the trial-and-error C1 iterations. Source for the four new section 4 rows: Axis A connection-state hardening, local Editor-as-peer harness, C1 validation gating, and two-headset retest preconditions.

When an item lands in a future slice, replace its row with a strikethrough + link to the landing commit + the slice doc that closed it. Don't remove rows — backlog history is useful retrospectively.
