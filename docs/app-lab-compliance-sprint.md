---
title: App Lab / Horizon Store technical compliance sprint
description: Sprint plan covering the seven technical slices needed to make CampfireVR submittable on the Horizon Store Early Access track. Marketing assets explicitly out of scope.
category: meta
status: planning
last_updated: 2026-05-19 (Slices 1 + 2 + 3 + 4 + 7 landed; keystore generation + headset validation pending Johan-side)
sections:
  - Context and scope
  - Slice status at a glance
  - Slice 1 — Android target SDK
  - Slice 2 — Release signing keystore
  - Slice 3 — versionCode + versionName automation
  - Slice 4 — Focus / pause / resume handling
  - Slice 5 — Performance readiness
  - Slice 6 — Soak test checklist
  - Slice 7 — Privacy / data handling draft
  - Recommended order and rationale
  - First slice to execute
  - Status-update conventions
---

# App Lab / Horizon Store compliance sprint

Living planning doc for the technical work needed to make CampfireVR submittable on Meta's Horizon Store **Early Access** track (post-2024 successor to App Lab). Each section is a slice-sized unit of work and tracks its own status here.

This is the *plan*, not the implementation. Nothing in this doc changes code, scene, or settings; subsequent slices do that, and each one updates the corresponding status line below when it lands.

## Context and scope

Grounded in [`docs/meta-store-readiness-audit.md`](meta-store-readiness-audit.md) (committed 2026-05-19, `8c46069`). That doc lists 9 submission blockers + 5 recommended gaps + a "what I didn't verify" section. This sprint covers the **technical** blockers and recommendations — the ones we can solve with code, build-pipeline tweaks, and developer documentation.

**In scope:**
- Build configuration (target SDK, signing, versionCode, headset target flags)
- Runtime stability (focus / pause / resume handling)
- Performance lever flips + measurement workflow
- Two-headset soak test workflow
- Privacy policy draft (text only — hosting is a future slice)

**Out of scope for this sprint:**
- App icon, hero cover, landscape / square / portrait covers, screenshots, trailer, trailer cover
- Store description / what's-new copy / tagline
- IARC questionnaire / age self-cert / comfort rating (dashboard work, separate slice)
- GitHub Pages hosting of the privacy policy (the *draft* is in scope; *publishing* it isn't)
- Photon Voice 2 EULA verification vs Meta distribution
- Unity Relay free-tier ToS verification vs Store distribution

The marketing-and-dashboard side is a separate sprint that can run in parallel and depends on at most one item from here (the privacy policy needs to be hosted before the dashboard's "Privacy Policy URL" field gets filled in).

## Slice status at a glance

| # | Slice | Complexity | Status | Notes |
|---|---|---|---|---|
| 1 | Android target SDK | S (~1 h) | DONE | Pinned to API 34; verified via `aapt2 dump badging`. |
| 2 | Release signing keystore | S (~2 h) | DONE (code+docs) | `ReleaseSigningGuard` + env-var wiring + `docs/release-keystore.md` shipped. Actual `keytool -genkey` + env var export is a manual follow-up on Johan's machine. |
| 3 | versionCode + versionName automation | S (~2 h) | DONE | `VersionCodeGuard` applies `git rev-list --count HEAD` per build; restores baseline post-build so disk stays clean. Verified versionCode=103 in APK manifest, ProjectSettings.asset unchanged. |
| 4 | Focus / pause / resume handling | M (~3 h) | DONE (code) | `AppLifecycle` + `VoiceBootstrap.SetTransmitEnabled`. Voice mic mute on focus loss, restore on regain. Headset verification of Meta-menu open/close pending. |
| 5 | Performance readiness | M (~3 h) | TODO | Foveated rendering + Quest 3 target flag + checklist. |
| 6 | Soak test checklist | S (~2 h) | TODO | Documentation-only; depends on slices 4 + 5 to land first. |
| 7 | Privacy / data handling draft | S (~1.5 h) | DONE (draft) | `docs/privacy-policy-draft.md` shipped. 10 open verification questions tracked at the bottom; needs each resolved before becoming a hosted policy. |

Complexity scale: **XS** ≤ 1 h, **S** 1–3 h, **M** half-day, **L** full-day. All estimates assume Editor + Quest are healthy.

---

## Slice 1 — Android target SDK

**Status:** DONE (2026-05-19)
**Complexity:** S (~1 h including build verification)
**Depends on:** nothing
**Blocks:** any future submission once Meta's 2026-03-01 deadline applies.

**Landed:** `QuestBuildSetup.cs` `targetSdkVersion` pinned to `AndroidApiLevel34`; `ProjectSettings.asset:178` `AndroidTargetSdkVersion: 0 → 34`. Verified via `aapt2 dump badging` on a fresh `./scripts/build-quest.sh` output: `targetSdkVersion: '34'`, `compileSdkVersion: '34'`, `platformBuildVersionName: '14'`.

**Goal:** Pin `targetSdkVersion` to API 34 explicitly so we don't depend on whichever Android SDK platform Unity happens to find installed.

**Current state:**
- `ProjectSettings.asset` line 178: `AndroidTargetSdkVersion: 0` (= `AndroidSdkVersions.AndroidApiLevelAuto`).
- `ProjectSettings.asset` line 177: `AndroidMinSdkVersion: 29` (correct, Quest minimum).
- `QuestBuildSetup.cs` line 25: `PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;` — re-asserted by the Editor menu, so the drift is sticky.

**Tasks:**
1. Change `QuestBuildSetup.cs` to set `AndroidSdkVersions.AndroidApiLevel34` (or the highest constant Unity 6.4 exposes, with a comment explaining the floor).
2. Re-run `Tools/Quest Setup/Configure Project for Quest 3` so `ProjectSettings.asset` reflects the change.
3. Build via `./scripts/build-quest.sh` and inspect the produced APK's `AndroidManifest.xml` (`aapt2 dump xmltree Builds/...apk AndroidManifest.xml` or `apkanalyzer manifest target-sdk`) to confirm the manifest declares `android:targetSdkVersion="34"`.
4. Update `docs/release-process.md` to mention the target SDK in the "Build metadata baked into every APK" section.

**Risk:** API 34 has stricter foreground-service and exact-alarm rules. Our app doesn't use either, so the risk is low. Worth a quick `aapt2 dump permissions` check on the APK to confirm no surprise permissions slipped in via package updates.

**Validation:** APK manifest shows `targetSdkVersion=34`; `./scripts/build-quest.sh` exits clean; install + launch on Quest works.

---

## Slice 2 — Release signing keystore

**Status:** DONE — code + docs landed 2026-05-19. Keystore generation (`keytool -genkey`) + env var export pending Johan's manual one-time setup; see `docs/release-keystore.md`.
**Complexity:** S (~2 h including the documentation)
**Depends on:** nothing
**Blocks:** all submissions — Meta refuses debug-signed APKs.

**Landed:** New `Assets/Editor/Build/ReleaseSigningGuard.cs` (`IPreprocessBuildWithReport`, callbackOrder=100) reads four env vars (`CAMPFIREVR_KEYSTORE_PATH/PASS`, `CAMPFIREVR_KEY_ALIAS/PASS`) and applies them to `PlayerSettings.Android` in memory before each Android build. Falls back with a clear warning if env vars aren't set; ProjectSettings.asset on disk never sees keystore values. `.gitignore` adds `*.keystore`, `*.jks`, `CampfireVR-release.*` as defensive guards. `docs/release-keystore.md` (new) covers keytool generation, backup strategy (two copies minimum, never to cloud-without-encryption), env var wiring, and `apksigner verify --print-certs` verification. Verified by running `./scripts/build-quest.sh` with env vars unset — log shows expected `[ReleaseSigningGuard] No CAMPFIREVR_KEYSTORE_PATH set` warning, build still succeeds with debug keystore (sideload only).

**Goal:** Replace the implicit Unity debug keystore with a real release keystore that's documented, backed up, and never committed to git.

**Current state:**
- `ProjectSettings.asset` line 285: `androidUseCustomKeystore: 0`.
- `ProjectSettings.asset` line 272: `AndroidKeystoreName:` empty.
- Build pipeline signs every APK with `~/.android/debug.keystore` (Unity default) — fine for sideload, refused by Meta review.

**Tasks:**
1. Generate a release keystore with `keytool -genkey -v -keystore CampfireVR-release.keystore -alias campfirevr -keyalg RSA -keysize 4096 -validity 10000` (25-year validity — Meta requires the same key for every future upgrade).
2. Store the keystore outside the repo at a stable path (suggestion: `~/.keystores/CampfireVR-release.keystore`). Document the path in a new `docs/release-keystore.md` slice doc.
3. Add `*.keystore`, `*.jks`, and `CampfireVR-release.*` to `.gitignore` as defensive guards — even though the keystore lives outside the tree, future drag-and-drop accidents have happened.
4. Configure Unity to use it via `Edit → Project Settings → Player → Publishing Settings`. Either set the path / passwords explicitly per-machine, or extend `QuestBuildSetup.cs` to read them from env vars (`CAMPFIREVR_KEYSTORE_PATH`, `CAMPFIREVR_KEYSTORE_PASS`, `CAMPFIREVR_KEY_ALIAS`, `CAMPFIREVR_KEY_PASS`) so the project settings stay committable without leaking secrets. Env-var approach matches the existing CI/CD philosophy.
5. Document backup strategy in `docs/release-keystore.md`:
   - **Two copies minimum** — losing the keystore means the app can never be upgraded on existing installs, permanently.
   - Suggested: encrypted external SSD + a copy in a password manager (1Password attachments work well).
   - **Never** upload to GitHub, GitHub Gists, Google Drive without encryption — Meta + Google both treat a leaked keystore as a takeover risk.
6. Verify a release-signed build via `apksigner verify --print-certs Builds/...apk` — should show the new cert subject, not "Android Debug".

**Risk:** Losing the keystore is irreversible — every existing installed APK becomes un-upgradable. Document the backup procedure thoroughly before generating; treat the keystore as project-critical secret material.

**Validation:** `apksigner verify --print-certs` on a fresh build shows the release cert (not debug); `git status` after a build is clean (no keystore tracked); env-var-driven build works end-to-end via `./scripts/build-quest.sh`.

---

## Slice 3 — versionCode + versionName automation

**Status:** DONE (2026-05-19)
**Complexity:** S (~2 h)
**Depends on:** Slice 2 ideally lands first (both edit the build pipeline; merging them avoids back-to-back rebuilds).
**Blocks:** repeat Store uploads — Meta requires strictly-increasing versionCode.

**Landed:** New `Assets/Editor/Build/VersionCodeGuard.cs` implements both `IPreprocessBuildWithReport` and `IPostprocessBuildWithReport` (callbackOrder=200). Pre-build: reads `CAMPFIREVR_VERSION_CODE` env var (computed by `scripts/build-quest.sh` as `git rev-list --count HEAD`), applies to `PlayerSettings.Android.bundleVersionCode`, remembers the previous value. Post-build: restores the previous value. PreloadedAssetsGuard's post-build `AssetDatabase.SaveAssets()` runs at `int.MaxValue` (i.e. after VersionCodeGuard's restore), so the on-disk asset only ever sees the baseline value. `scripts/build-quest.sh` exports the env var and bakes `versionCode` into `build-info.json`. `DebugLogger.cs` reads `info.versionCode` and stamps `build_version_code` on the `app_started` event. `scripts/package-friend-test.sh` surfaces it in README + RELEASE-NOTES. `docs/release-process.md` gets a new "Version-identity chain" subsection mapping all five places the value lives (CHANGELOG → APK filename → bundleVersion → bundleVersionCode → build-info → debug log). Verified end-to-end: APK manifest shows `versionCode='103'`, ProjectSettings.asset on disk stays at `AndroidBundleVersionCode: 1`, `git status` clean.

**Goal:** Make every release build's `bundleVersionCode` strictly increase and stay aligned with the version tag in `build-info.json`, the APK filename, and the CHANGELOG.

**Current state:**
- `ProjectSettings.asset` line 176: `AndroidBundleVersionCode: 1`. Never bumped.
- `ProjectSettings.asset` line 146: `bundleVersion: 1.0`. Hand-edited.
- `scripts/build-quest.sh` writes `build-info.json` with `version` from CHANGELOG (e.g. `v0.1.2-session-fix`) — string only, no numeric versionCode anywhere downstream.
- APK filename `CampfireVR-<version>-<YYYYMMDD-HHMM>.apk` uses the CHANGELOG version + timestamp.

**Tasks:**
1. Extend `scripts/build-quest.sh` (or a new `scripts/lib/versioning.sh` helper) to compute a monotonic versionCode. Cheapest deterministic option: `git rev-list --count HEAD` — strictly increases with every commit on `main`. Alternative: Unix epoch divided by 60 — guaranteed monotonic but less semantic.
2. Before invoking Unity, write the versionCode into `ProjectSettings.asset` via a small Editor `[InitializeOnLoad]` helper — or simpler, expose it through a `-executeMethod` Unity arg that reads from an env var. Avoid editing `ProjectSettings.asset` from bash (YAML structural edits are fragile).
3. Pipe the same versionCode into `build-info.json` as a new `versionCode` field.
4. Have `DebugLogger.cs` read `versionCode` from `build-info.json` and stamp it onto the `app_started` event (already does the string `build_version`; add the numeric `build_version_code`).
5. Update `scripts/package-friend-test.sh` to surface `versionCode` in `RELEASE-NOTES.md` and the README header.
6. Document the version-identity chain in `docs/release-process.md`: CHANGELOG heading → APK filename → bundleVersion string → bundleVersionCode integer → build-info.json → debug-logs → friend zip name.

**Risk:** YAML edits to `ProjectSettings.asset` from a shell script are notoriously fragile (YAML's whitespace sensitivity plus Unity's serialization quirks). Mitigation: do all asset mutation from C# Editor code, never from bash. The bash side only computes the integer and passes it in via env var.

**Validation:** Two back-to-back builds produce two different `versionCode` values; the values match `build-info.json` and the `app_started` log event; `apksigner` or `aapt2 dump badging` reports the same versionCode in the manifest.

---

## Slice 4 — Focus / pause / resume handling

**Status:** DONE (code, 2026-05-19) — headset verification of Meta-menu open/close pending Johan-side.
**Complexity:** M (~3 h, including a two-headset verification pass)
**Depends on:** nothing
**Blocks:** VRC.Quest.Functional — Meta tests "system menu opens mid-session, app doesn't misbehave."

**Landed:** New `Assets/Scripts/Lifecycle/AppLifecycle.cs` (~85 lines, `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` boot pattern mirroring DebugLogger). Implements `OnApplicationFocus(bool)` and `OnApplicationPause(bool)` separately so both transitions get their own log entries. Logs four events: `app_lifecycle_ready`, `app_focus_lost` / `app_focus_gained`, `app_paused` / `app_resumed`. Each focus event includes a `voice_transmit_muted` / `voice_transmit_restored` field so the log shows whether the voice mute fired together with the focus change. New `VoiceBootstrap.SetTransmitEnabled(bool)` toggles `Recorder.TransmitEnabled` on Photon Voice 2's PrimaryRecorder — mutes outgoing transmission while keeping the mic stream initialised, so voice resumes instantly on focus regain (no sub-second gap from mic re-initialisation that `RecordingEnabled` would cause). `_mutedByUs` flag ensures we only unmute on focus regain if our focus-loss handler is what muted in the first place. Deliberately does NOT touch NGO heartbeats, Relay session, or XRHeadTracker pose writes — system-menu open should not tear down the connection, and head-pose freeze on focus loss is left for a future slice if headset testing flags issues. `docs/debug-logging.md` event schema gains the four new event rows. Verified by `./scripts/build-quest.sh` (compiles, fixed one Unity deprecation warning along the way: `FindFirstObjectByType` → `FindAnyObjectByType`); full headset verification (open Meta menu mid-session, confirm via post-session log pull that focus event sequence is correct + voice mute confirmed) deferred to Johan's next two-headset session.

**Goal:** When the user opens the Meta system menu (or removes the headset, or app loses input focus for any reason), CampfireVR pauses input reads + voice + network ticks and logs the event. When focus returns, everything resumes cleanly.

**Current state:**
- `DebugLogger.OnApplicationQuit` (line 65) is the only Unity-lifecycle hook in the project.
- `XRHeadTracker` reads `InputDevices.GetDeviceAtXRNode(...)` every `LateUpdate` unconditionally — would keep "moving" the local head while the user is interacting with the system menu.
- `NetworkBootstrap` has no pause handling; NGO transport keeps sending heartbeats.
- `VoiceBootstrap` Photon connection keeps mic open and uploads audio (potential privacy issue: Meta menu audio captured).

**Tasks:**
1. Create a new `Assets/Scripts/Lifecycle/AppLifecycle.cs` MonoBehaviour, attached to NetworkBootstrap's GameObject (or a new persistent root). Implement:
   - `OnApplicationFocus(bool hasFocus)` — log JSON event `{"event":"app_focus","focused":<bool>}` via DebugLogger.
   - `OnApplicationPause(bool isPaused)` — log similar.
2. Wire a "system focus lost → mute Photon Voice mic" action: `VoiceBootstrap.SetMicMuted(true)` on focus loss, `false` on focus regained. Prevents accidental capture of the Meta menu / room.
3. Wire a "system focus lost → freeze XRHeadTracker pose writes" — simple toggleable bool inside XRHeadTracker that the lifecycle script flips.
4. Decide what to do about NGO / Relay during focus loss. Options: (a) leave the connection up (heartbeats are tiny, fastest resume); (b) gracefully disconnect after N seconds of focus loss. Recommendation: leave up, document the choice. If we later see Meta flagging idle networking, revisit.
5. Add the new events to `docs/debug-logging.md`'s event schema table.
6. Test with two physical headsets: open Meta menu mid-session, confirm via `adb logcat` (or post-session log pull) that `app_focus` events fire at the right times and that the remote player doesn't see the local player's head jitter while the menu is open.

**Risk:** XRHeadTracker freezing means the local user's head pose stops updating to remote players — they'll see a "frozen player" while the local user is in the system menu. That's the *correct* behaviour (privacy + correct UX), but it differs from the current state and is worth documenting in the soak test (Slice 6) so testers know what to expect.

**Validation:** Manual two-headset session where one player opens the Meta menu and the other watches; debug log shows the focus event sequence; voice mic actually mutes (verify via test phrase spoken inside the menu — should not appear in remote audio).

---

## Slice 5 — Performance readiness

**Status:** TODO
**Complexity:** M (~3 h)
**Depends on:** nothing critical, but pairs nicely with Slice 1 (both touch build config).
**Blocks:** VRC.Quest.Performance — apps must hit declared refresh rate without drops.

**Goal:** Flip the obvious perf levers; declare the right headset targets; produce a manual frame-time measurement workflow we can re-run before every shared build.

**Current state (from `Assets/XR/Settings/OculusSettings.asset`):**
- `FoveatedRenderingMethod: 0` — foveated rendering **off**.
- `SubsampledLayout: 0` — subsampled layout off (works with FFR; off is safe default).
- `TargetQuest2: 1`, `TargetQuest3: 0`, `TargetQuestPro: 0`, `TargetQuest3S: 0` — **declared support for Quest 2 only**, even though README says Quest 3 is the target. Significant drift worth fixing.
- `LowOverheadMode: 0` — recommended off for Vulkan, correct.
- `PhaseSync: 1`, `OptimizeBufferDiscards: 1` — already on, good.
- `SpaceWarp: 0` — off; can stay off for a 90 Hz seated scene, document as a future lever.

**Tasks:**
1. Fix the headset-target drift: set `TargetQuest3: 1`, leave `TargetQuest2: 1` if we still want Quest 2 compatibility (TBD — Quest 2 is older but the rendering budget might still pass on this low-poly scene), set `TargetQuest3S: 1` for the Quest 3S since it's a Quest 3 variant. Decide via a quick check on the scene's GPU cost on the older device.
2. Enable foveated rendering: set `FoveatedRenderingMethod: 1` (Fixed Foveated Rendering) — runtime configurable to dynamic later. Document the choice.
3. Decide whether to also enable `SubsampledLayout: 1` — improves FFR quality but slightly increases bandwidth; check Unity 6.4 + Quest 3 compatibility before flipping.
4. Write `docs/performance-checklist.md`:
   - How to enable the Quest's perf overlay (`adb shell setprop debug.oculus.metricsOverlay 1` or via OVRMetricsTool).
   - What metrics to watch (App GPU time, App CPU time, missed frames, GPU level, CPU level).
   - Pass criteria: <11.1 ms App GPU time at 90 Hz with 5-min headroom; zero missed frames during a 5-minute connected session.
   - Common red flags (vertex-heavy scene additions, shadow casters multiplied, transparent overdraw).
5. Identify high-risk scene items already in CampfireVR:
   - **38 pine trees** — vertex count not yet measured.
   - **Oak tree** with ALP shader globals — wind animation cost.
   - **Dog companion** SkinnedMesh.
   - **FireLight** Hard shadows.
   - **Grass cards** transparent cutout — overdraw risk if mass-multiplied.
6. Optional follow-up: a `Tools/Quest Setup/Measure Vertex Counts` Editor menu that prints total scene vertex count + per-renderer breakdown. Skip for this slice unless we hit perf surprises.

**Risk:** Foveated rendering is mature on Quest 3 but has historically caused subtle text rendering artefacts at the eye periphery — would affect our world-space TextMesh tutorial panel. Validate the panel readability after flipping FFR on.

**Validation:** Quest 3 perf overlay shows zero missed frames during a 5-min two-player session; world-space tutorial panel still readable with FFR enabled; APK builds + runs identically on Quest 2 if we keep that target.

---

## Slice 6 — Soak test checklist

**Status:** TODO
**Complexity:** S (~2 h, all documentation)
**Depends on:** Slices 4 + 5 should land first so the checklist reflects post-fix behaviour.
**Blocks:** confidence in submission — Meta's review team runs through every flow they can find; we should run through them first.

**Goal:** A repeatable two-headset, 30-minute checklist that exercises every user-visible feature and produces a clean debug-log archive for retrospective analysis.

**Tasks:**
1. Create `docs/soak-test-checklist.md` with sections:
   - **Setup** — two charged Quest 3 headsets, two Wi-Fi networks (or one if testing LAN), one tester per headset.
   - **Pre-flight** — confirm both headsets on the same APK (read the `app_started` event from logs or check Settings → Apps).
   - **The 30-minute script**:
     - 0–5 min: solo boot, recenter, look at tutorial panel, switch room letter (right thumbstick), toggle mode (left Y).
     - 5–10 min: Player A hosts on Room A (Internet mode); Player B joins; both confirm they see each other; both confirm voice flows both ways.
     - 10–15 min: open the Meta system menu on one side; confirm the *other* side sees the local player freeze (post-Slice-4) and voice mute; close the menu; resume confirmed.
     - 15–20 min: long-press left Y on one side to trigger Stop; confirm the other side sees the disconnect cleanly; re-host or re-join; confirm reconnection works.
     - 20–25 min: cycle through every controller button (X, A, B, Y short, Y long, right thumbstick); confirm none crash or stick.
     - 25–30 min: open `adb shell dumpsys meminfo com.unitymcplab.campfireroom` on a connected computer at minute 0 and minute 30 — confirm RSS hasn't grown more than 50 MB.
2. **Pass/fail criteria** section:
   - PASS: zero crashes, zero stuck input, voice clear, no visible frame stutter during normal action, memory steady, `debug_log` complete.
   - WARN: any "stop_step_failed" events, any "voice_state_change" loops, any frame-time blips visible in the perf overlay.
   - FAIL: any crash, any audio dropout >1 s, any net-stuck state requiring app quit.
3. **Log pull workflow** — single command using `scripts/pull-quest-logs.sh --zip` on each Quest, naming the zips by player ID + date. Document where the zips go.
4. **Reporting template** — short markdown stanza for capturing pass/fail + observations + which APK was tested (use the BUILD-INFO event from log).

**Risk:** "30 minutes" is a budget, not a duration — real soak tests often run over because of setup / re-pairing / battery. Plan for a 60-min wall-clock window per test.

**Validation:** Run the checklist end-to-end at least once with Johan + one friend; iterate on the doc based on what was unclear or missing. Then run it again on a clean install.

---

## Slice 7 — Privacy / data handling draft

**Status:** DONE (draft, 2026-05-19) — hosting + legal review are future steps tracked in the draft's own "Open questions" section.
**Complexity:** S (~1.5 h)
**Depends on:** nothing
**Blocks:** dashboard's Privacy Policy URL field, once hosted (out of scope for this sprint).

**Landed:** New `docs/privacy-policy-draft.md` (~200 lines) describing every data path the app actually has — voice (Photon Voice 2), session brokering (Unity Auth + Unity Relay), Photon room property `rc` for Relay-code handoff, NGO position sync, local debug logs. Explicit "what we deliberately do NOT collect" list verified by grep (no analytics, no PlayerPrefs, no advertising IDs, no location, no camera/passthrough, no biometric, no friends list). Notes the subtle nuance that Unity's anonymous auth still creates a persistent PlayerId per install — flagged in the draft rather than hidden. Closes with 10 open questions that must be answered before this becomes a hosted policy (Photon/Unity retention specifics, Android manifest permission audit, hosting URL, legal review). Targets 13+ age-group self-certification to stay out of COPPA scope.

**Goal:** A plain-language privacy policy draft that names every data type CampfireVR touches and where it goes. Stored under version control so future revisions are diffable.

**Tasks:**
1. Create `docs/privacy-policy-draft.md` covering:
   - **Who runs the app** — Johan Lindengård, hobby project, contact email.
   - **What data we process:**
     - **Voice audio** — captured by Quest microphone, streamed to **Photon Voice 2** cloud servers (link Photon's privacy policy + ToS), routed to other players in the room, **not recorded** by us. Mic mutes when the Meta system menu opens (post-Slice-4).
     - **Network session metadata** — Unity Relay allocation join codes via Photon room property, Relay relay-IP routing data, NGO heartbeat traffic. Held only for the duration of the session.
     - **Local debug logs** — JSONL files under `/sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/`, never leave the device unless the tester runs `adb pull` and sends them to us manually. Contents: session timestamps, host/join state changes, voice connection state, frame markers — no audio, no identifiers beyond what Meta already exposes via OpenXR.
   - **What we do NOT collect** — no telemetry, no analytics SDKs, no advertising IDs, no Meta Platform Services accountId queries.
   - **Third parties** — link Photon's privacy doc, link Unity's privacy doc.
   - **Retention** — voice not retained; session metadata gone when the session ends; debug logs retained only on the tester's device until they choose to share or delete.
   - **Deletion request path** — email contact for "please confirm you've deleted any logs I sent you."
   - **Children** — app is 13+; we don't collect data from younger users.
   - **Changes** — date this draft, note future versions.
2. Cross-reference from `README.md` ("Privacy" section pointing to the draft).
3. Note in `docs/release-process.md` that the draft must be hosted at a public HTTPS URL before submission and provide a suggested hosting recipe (GitHub Pages on the `cola500/CampfireVR` repo).
4. Add a `## Privacy` section to `docs/meta-store-readiness-audit.md` blocker 9 referencing this draft so the audit accurately reflects current state when this slice lands.

**Risk:** Privacy policies are legally consequential — this is a *draft*, not legal advice. Recommend a quick review (15 min, free) from a privacy-aware friend before hosting. Track the gap: this slice produces text we believe is accurate, not text reviewed by a lawyer.

**Validation:** The draft accurately names every data path the app actually uses (cross-check against `NetworkBootstrap`, `VoiceBootstrap`, `DebugLogger`); reading it as a tester answers "what does this app do with my voice" in under a minute; no third-party services mentioned that the app doesn't actually call.

---

## Recommended order and rationale

Two principles guide the order:

1. **Cheapest blockers first.** Slices that unblock submission with minimal code change should land before slices that need design decisions or headset testing.
2. **Pair related changes.** Build-pipeline slices (1, 2, 3) share files and re-test infrastructure; landing them in adjacent commits is easier than threading them through other work.

Suggested sequence:

| Order | Slice(s) | Why |
|---|---|---|
| 1 | **Slice 1 (target SDK)** | Cheapest, pure config, build-pipeline already covers verification. ~1 hour. |
| 2 | **Slice 2 (signing) + Slice 3 (versionCode)** as one branch | Both edit the build pipeline; merging them avoids two consecutive Library invalidations. ~4 hours combined. |
| 3 | **Slice 7 (privacy draft)** | Independent; can land any time; writing it surfaces every data path the app touches, which informs Slice 4. ~1.5 hours. |
| 4 | **Slice 4 (focus handling)** | Runtime code; needs headset verification. Informed by Slice 7 (the voice-mute decision is a privacy decision). ~3 hours. |
| 5 | **Slice 5 (performance)** | Configuration + measurement workflow; pairs well with the headset session needed for Slice 4 verification. ~3 hours. |
| 6 | **Slice 6 (soak test)** | Documentation that captures post-fix behaviour; depends on 4 + 5 to land first so the checklist reflects reality. ~2 hours. |

Total wall-clock estimate: **~15 hours** of focused work, fitting into 6–8 evening sessions over 2–3 weeks.

## First slice to execute

**Recommendation: Slice 1 — Android target SDK.**

Reasons:
- Smallest scope (1-line `QuestBuildSetup.cs` change + a build verify).
- Zero design decisions — the value is fixed at 34 by Meta's deadline.
- Validation is already in our build pipeline (`./scripts/build-quest.sh` → APK manifest inspection).
- No headset hardware needed.
- Unblocks the platform deadline without depending on any other slice.
- Lands a fast "we're moving on this sprint" signal in the changelog.

The natural follow-up is Slice 2 + Slice 3 as a paired build-pipeline branch. Slice 7 can run in parallel any time and gives us a useful artefact (the privacy draft) regardless of what else happens.

## Status-update conventions

When a slice starts:
- Flip the status column above from `TODO` → `IN PROGRESS`.
- Update `last_updated:` in the frontmatter.

When a slice lands (commit on `main`):
- Flip status to `DONE`.
- Add a "Landed in:" line under the slice's tasks pointing at the commit SHA and CHANGELOG entry.
- Move the corresponding bullets from the audit's blocker / recommended list into a "fixed" state (strikethrough + `[updated: app-lab-compliance-sprint]` annotation, per `docs/app-alignment-qa.md` convention).

When a slice gets blocked:
- Flip status to `BLOCKED`.
- Add a "Blocked by:" line naming the dependency or external decision.
- Re-evaluate the recommended order if the block lasts more than a few days.

When the sprint completes:
- Update `status:` in the frontmatter from `planning` → `stable`.
- Cross-reference all landed slices from `docs/roadmap.md`'s done section.
- Submission readiness becomes a separate sprint (dashboard work, screenshots, trailer).
