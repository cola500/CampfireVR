---
title: Soak test checklist
description: Repeatable 30-minute two-headset validation procedure for CampfireVR. Catches regressions in connection lifecycle, voice, recovery, and frame-time before they reach a friend or an App Lab review.
category: meta
status: stable
last_updated: 2026-05-19
sections:
  - Goals
  - Pre-test setup
  - 5-minute smoke test
  - 30-minute soak test
  - Performance observations
  - Failure conditions
  - Pass criteria
  - Log collection
  - Post-test notes
  - Regression checklist
  - Optional helper script
---

# CampfireVR soak test checklist

Repeatable operational validation for CampfireVR. Designed for two real Quest 3 headsets + two testers. Catches connection-lifecycle, voice, recovery, and frame-time regressions before they reach a friend or the Meta review queue.

This doc is a checklist, not a tutorial. It assumes both testers are comfortable putting on the headset and following spoken cues. The companion debug-log-aware version of a social session is `docs/remote-fika-test-debug-checklist.md` — use that for casual testing; use this when the goal is **validation** and you're going to file a pass/fail report afterwards.

## Goals

- **Confirm App Lab readiness** — voice, network, focus handling, recovery, and frame-time all behave as the spec promises.
- **Catch regressions early** — every Slice that lands in this sprint touched something. The checklist re-validates the surface area before the next shared build.
- **Produce a paper trail** — pass/warn/fail + log archive per session, so future-Johan can answer "when did this last work" forensically.
- **Stay realistic about time** — 30 minutes connected + ~15 minutes setup + ~10 minutes teardown = a 60-minute wall-clock window. Plan accordingly.

The checklist explicitly does NOT cover:

- Subjective UX feel — that's a separate "feels right" pass that doesn't need a structured protocol.
- Two-headset network condition simulation (low Wi-Fi, lossy ISP). The default home-Wi-Fi behaviour is what we're validating; degraded-network resilience is a future slice.
- Multi-room multi-pair stress testing. Single-room A is the canonical default; rooms B–Z are tested incidentally only.

## Pre-test setup

Before either tester puts on a headset.

### Both sides

- [ ] **Same APK version** on both Quests.
  ```sh
  adb=/Applications/Unity/Hub/Editor/6000.4.7f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
  $adb shell dumpsys package com.unitymcplab.campfireroom | grep -E "versionName|versionCode"
  ```
  Both should print the same `versionName=1.0` and the same `versionCode=<N>` (where N is the `git rev-list --count HEAD` value baked in via Slice 3). If they differ, install the latest APK on the older side first.

- [ ] **Snapshot BUILD-INFO** for the test report. Run `./scripts/create-soak-test-report.sh` from one tester's machine — it captures `UnityProject/Assets/Resources/build-info.json` for the build under test and creates an empty `NOTES.md` to fill in. See [Optional helper script](#optional-helper-script) below.

- [ ] **Both devices charged** — soak tests are real load. Start at >70 % battery on both, plan for ~20–30 % drain across the 60-minute window. If either Quest is plugged in for power, mention it in the notes (corded play affects ergonomics + thermals differently).

- [ ] **Stable Wi-Fi on both sides.** Walk past the router with the phone and confirm the speed test is at least 25 Mbps down / 5 Mbps up; less than that and Photon Voice can struggle. If both testers are in different houses, both confirm independently.

- [ ] **Headphones on both Quests** before putting them on. Quest speakers leak voice into the other Quest's microphone — voice gets weird without headphones.

- [ ] **Verify debug logging is active** on each device by checking the log directory exists:
  ```sh
  $adb shell ls /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/
  ```
  If empty, launch the app once first; `DebugLogger` creates the directory on initial boot.

- [ ] **Optionally clear old logs** for a clean session:
  ```sh
  $adb shell rm /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/*.jsonl
  ```
  (The logger auto-rotates after 10 files anyway. Pre-cleanup is just for clarity when triaging afterwards.)

- [ ] **Confirm Room A** is the target room. Default at boot. If a previous session cycled the letter and you want to start clean, restart the app on both sides; default `A` returns.

- [ ] **Confirm `mode · Internet`** at the panel bottom on both sides. Same Wi-Fi mode is dev-only — won't work between houses.

- [ ] **Both testers note local wall-clock time** before launching. ("Starting at 19:35.") Phone clocks are usually NTP-synced; if you're paranoid about skew, both check `time.gov` or equivalent once. Even 5 s of drift makes log diffing painful.

### Designated host side

- [ ] Confirms first who hosts. Hosting tester opens the app first. Joining tester waits ~10 s before launching (so the host's `relay_alloc_succeeded` lands before the join attempt).

## 5-minute smoke test

Run this **first**, every session. If anything fails here, abort the 30-minute soak — you'll lose the time on something that's already broken.

### 0:00 — Solo launch on both sides

- [ ] App boots within ~10 s, no black screen
- [ ] World-space text panel reads `Room: A` and `mode · Internet`
- [ ] Tutorial panel readable, controls labels make sense
- [ ] Hand mittens visible, tracking matches controller pose

### 1:00 — Host

- [ ] Host presses **left X**, says "hosting"
- [ ] Panel changes to `Hosting Room A`
- [ ] No errors visible / no missing-prefab markers

### 2:00 — Join

- [ ] Joiner presses **right B**
- [ ] Both panels read `Connected` within ~3 s
- [ ] Remote avatar (head + hands) visible across the fire on both sides
- [ ] Voice both directions — host says "one two three", joiner echoes

### 3:00 — Meta menu

- [ ] One tester (let's say host) presses **Meta button** to open the system menu
- [ ] Other tester confirms: voice goes silent on their side, remote avatar's head freezes at its last position (NOT drifting — this is the current designed behaviour)
- [ ] Host closes menu (Meta button again)
- [ ] Voice resumes within ~1 s, no need to re-host or re-join

### 4:00 — Stop / rejoin

- [ ] Joiner long-presses **left Y** for 1.5 s
- [ ] Panel reads `Stopped session`
- [ ] Host's panel reads `Friend left`
- [ ] Joiner immediately presses **right B** to re-join
- [ ] Reconnection succeeds within ~5 s, voice + avatar return

If all five 5-minute checks pass: proceed to the 30-minute soak.
If any fail: stop, capture logs immediately (see [Log collection](#log-collection)), file the failure in `NOTES.md`.

## 30-minute soak test

The structured part. Both testers stay in headset. Times are approximate — exact-minute precision isn't important, but completing each block before moving on is.

### 0:00–5:00 · Idle conversation + sync observation

The "feel" baseline. No deliberate stress.

- [ ] Both testers sit relaxed by the fire, have a normal conversation for ~5 minutes
- [ ] Move hands naturally — gestures, point at things, mime drinking — observe remote sync feels live (sub-100ms perceptible)
- [ ] Move head naturally — look up at the sky, down at the fire, side-to-side
- [ ] No visible head/hand stutter or rubber-banding
- [ ] No voice artefacts (echoing, robot-voice, dropouts)

### 5:00–10:00 · Focus loss stress

One side opens the Meta menu repeatedly.

- [ ] Host opens Meta menu, waits ~5 s, closes. Repeat **3 times**.
- [ ] Joiner stays in-app, observes:
  - Voice mutes during each menu-open, restores on menu-close
  - Remote head freezes during menu-open, resumes when menu closes
  - No re-host or re-join needed; session stays alive
- [ ] **Headset briefly removed and re-worn** by host (slip off for ~10 s, then put back on). Joiner confirms session survives.
- [ ] After all of the above, **swap roles**: joiner opens Meta menu 3 times + briefly removes the headset. Host observes.

### 10:00–15:00 · Stop / rejoin cycle

Recovery path stress.

- [ ] Joiner long-presses **left Y** → confirms `Stopped session`
- [ ] Joiner waits ~10 s
- [ ] Joiner presses **right B** to re-join the same room
- [ ] Both confirm reconnection works, voice + avatar return
- [ ] **Optional**: swap host. Original host long-presses **left Y** to stop. Joiner (now alone) becomes the new host by pressing **left X**. Original host re-joins by pressing **right B**.

### 15:00–20:00 · Extended idle

Watch for slow regressions that don't show up at minute 5.

- [ ] Both stay seated, talk slowly or just enjoy the fire
- [ ] One tester glances at the perf overlay every ~30 s (if enabled — see `docs/performance-checklist.md`)
- [ ] Watch for: any frame stutter, any voice glitch, any visible memory pressure (overlay GPU level climbing to 3+)

### 20:00–25:00 · Stress test

The deliberately hostile block.

- [ ] **Repeated stop/join**: joiner long-presses Y, immediately re-joins. Repeat **5 times** with ~10 s between each cycle.
- [ ] **Room reconnect**: both testers cycle to room B (right thumbstick → next room) by pressing it once. Long-press Y on both sides to fully stop. Host on B, join on B. Then cycle back to A and repeat.
- [ ] **Simultaneous actions**: both testers press their controller buttons at the same time — host hits X while joiner hits B, host hits A (recenter) while joiner long-presses Y. Watch for any "stuck" state.

### 25:00–30:00 · Calm idle + observations

Cool down and capture observations while still in-headset.

- [ ] Both stay seated, conversation natural
- [ ] Final voice stability check — both say "one two three four"
- [ ] **Note battery level** on each Quest before quitting (visible in system menu)
- [ ] **Note any thermal warning** — Quest will show a temperature warning at the top of the system menu if it's hot
- [ ] Both testers note any subjective discomfort (headset weight, eye strain, voice fatigue)

After 30 minutes connected: end session (long-press Y on both, or quit via Meta menu).

## Performance observations

If using the perf overlay (recommended for the first soak test on a new build), reference `docs/performance-checklist.md` for what each number means. Capture in `NOTES.md`:

- **App GPU time** (median + p95) — should stay under 8 ms at 90 Hz
- **App CPU time** (median + p95) — should stay under 6 ms
- **Missed frames** total — ideally 0 across the 30 minutes
- **GPU level** trend — should stabilise at 2 or below
- **Memory growth** (RSS at minute 0 vs minute 30 via `adb shell dumpsys meminfo`) — < 50 MB delta is healthy
- **Thermal level** — should stay NORMAL or NOMINAL; WARNING means we have a problem
- **Subjective comfort** — does the headset feel hot? Are the testers visibly tired?

## Failure conditions

Treat any of these as **FAIL**, abort the test, capture logs:

- App crash on either side (drops to Quest home or shows the Unity error scene)
- Voice silenced permanently on one side after a Meta menu open (didn't restore on menu close)
- Reconnect impossible after long-press Y stop (re-host or re-join returns to error state)
- "Ghost session" — one side thinks it's connected, the other side thinks the room is empty, for more than 30 s
- Remote avatar frozen permanently (not just during a menu-open — meaning head + hands stop syncing for the rest of the session)
- Major frame stutter (visible judder during normal seated movement, sustained for more than a few seconds)
- Thermal level reaches WARNING and stays there
- App fails to resume after focus loss (system menu close → black screen, or the panel state is unrecoverable)

Treat these as **WARN**, finish the soak, note in `NOTES.md`:

- Single transient voice dropout (<1 s) that recovers on its own
- Single missed frame during a network transition (host → join, room change)
- Memory growth 50–150 MB across 30 minutes
- GPU level peaks at 3 during a stress block but returns to 2 afterwards
- Subjective "felt warm" without a thermal warning event

## Pass criteria

Indie-realistic, not Meta-internal:

- [ ] **Zero crashes** on either side across the 60-minute window
- [ ] **Voice survives normal usage** — at least 25 of 30 connected minutes had clean voice both directions
- [ ] **Reconnect works** — both stop/rejoin cycles and one Meta-menu open recovered cleanly
- [ ] **No progressive degradation** — minute 30 feels the same as minute 5 (no compounding lag, no creeping voice quality loss)
- [ ] **Logs retrievable from both headsets** — `./scripts/pull-quest-logs.sh --zip` produces non-empty archives on both sides
- [ ] **NOTES.md filled in** — pass/warn/fail per criterion, perf numbers if measured, observations per block

A session that passes all six bullets is shippable for friend test or App Lab submission, assuming no other known blockers remain.

## Log collection

Each tester pulls logs from their own Quest after the session. From the repo root on the tester's machine:

```sh
./scripts/pull-quest-logs.sh --zip
```

Produces `quest-logs/YYYYMMDD-HHMMSS/` + a matching zip. **Rename** each so the recipient can tell whose log is whose:

```sh
# Johan's side
mv quest-logs/20260520-2130 quest-logs/johan-20260520-2130
mv quest-logs/campfirevr-logs-20260520-2130.zip quest-logs/johan-20260520-2130.zip

# Friend's side
mv quest-logs/20260520-2131 quest-logs/friend-20260520-2131
mv quest-logs/campfirevr-logs-20260520-2131.zip quest-logs/friend-20260520-2131.zip
```

Then attach **both zips** + the **BUILD-INFO.json** snapshot from `create-soak-test-report.sh` (or `UnityProject/Assets/Resources/build-info.json` if no report folder exists) to the session's NOTES.md.

For triage commands (`jq` interleave, time-window grep), see [`docs/remote-fika-test-debug-checklist.md`](remote-fika-test-debug-checklist.md#quick-triage-on-receiving-both-files).

## Post-test notes

Use `NOTES.md` (auto-created by `create-soak-test-report.sh`) to capture:

- Session date + time + duration (planned 30 min connected, actual may differ)
- Both testers' names
- APK identity: filename + versionCode + git commit (copy from BUILD-INFO.json)
- Pass/Warn/Fail per pass-criterion bullet
- Per-block observations (idle, focus loss, stop/rejoin, extended idle, stress, calm)
- Perf numbers if measured
- Failure descriptions with wall-clock timestamps (cross-references log JSONLs)
- Subjective comfort observations

The format is intentionally loose — focus on capturing what you'd want a future-you to know if asked "did this build pass soak six months ago?".

## Regression checklist

Short reusable checklist after any future slice that touches the surface area. Run between slices that ship; full soak test before each shared friend build.

- [ ] **Build** clean: `./scripts/build-quest.sh` exits 0, no `error CS`, all guards (Release / Version / Preloaded) fire
- [ ] **Install** clean: `./scripts/build-quest.sh --install-only --launch` reaches the launched-app state without errors
- [ ] **Solo boot OK**: app launches on the headset, world-space panel reads `Room: A · mode · Internet`
- [ ] **Host/join**: two-Quest minimal — connect, see remote avatar, voice both directions for 60 s
- [ ] **Voice**: no dropouts during 60 s of normal conversation
- [ ] **Stop/recovery**: long-press Y stops cleanly, immediate re-join reconnects
- [ ] **Focus/pause**: open + close Meta menu, voice mutes + restores, no re-host needed
- [ ] **Logs**: `./scripts/pull-quest-logs.sh` returns at least one non-empty JSONL file with this session's `app_started` event in it
- [ ] **Perf sanity**: perf overlay enabled, no missed frames during the 60 s seated-talking baseline

This is the floor — anything below this and a build isn't ready for friend testing.

## Optional helper script

`scripts/create-soak-test-report.sh` scaffolds a per-session report directory. Run it once at the start of each soak test:

```sh
./scripts/create-soak-test-report.sh
```

Produces:

- `quest-logs/soak-tests/YYYYMMDD-HHMM/` — the session folder
- `quest-logs/soak-tests/YYYYMMDD-HHMM/BUILD-INFO.json` — snapshot of `UnityProject/Assets/Resources/build-info.json` for this build
- `quest-logs/soak-tests/YYYYMMDD-HHMM/NOTES.md` — empty template pre-filled with the pass-criteria checklist + the per-block observation prompts

After the session, drop the renamed log zips (johan-…zip, friend-…zip) into the same folder so everything for that session lives in one place. `quest-logs/` is gitignored — these archives stay local.

The script doesn't run adb. Log pulling is a separate step per `scripts/pull-quest-logs.sh` because each tester runs it on their own machine with their own Quest plugged in.

## See also

- [`docs/performance-checklist.md`](performance-checklist.md) — how to enable + read the Quest perf overlay
- [`docs/debug-logging.md`](debug-logging.md) — JSONL schema, triage recipes
- [`docs/remote-fika-test-debug-checklist.md`](remote-fika-test-debug-checklist.md) — companion doc for casual Remote Fika sessions (less structured, more social)
- [`docs/release-process.md`](release-process.md) — how the APK version + build-info propagate
- [`scripts/pull-quest-logs.sh`](../scripts/pull-quest-logs.sh) — log retrieval
- [`docs/post-sprint-backlog.md`](post-sprint-backlog.md) — items deferred from this sprint that pair naturally with a soak test session
