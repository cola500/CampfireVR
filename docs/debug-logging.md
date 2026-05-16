# Debug logging

CampfireVR writes timestamped, structured events to a local file on every Quest run. The goal is to reconstruct a two-headset test session after the fact by diffing log files from both devices.

## What gets logged

| Event | When | Useful for |
|---|---|---|
| `app_started` | First frame after launch | Per-session header — product name, version, platform, device model, install mode, plus build identity (build version, build time, git commit + branch, dirty flag, APK filename) |
| `network_bootstrap_ready` | NetworkBootstrap.Start() | Initial mode, room letter, active scene |
| `editor_key` | Editor key press (H/C/X/M/L) | Repro paths when iterating in flat-screen Editor |
| `mode_changed` | Y press / M key | LAN ↔ Relay toggle |
| `room_changed` | Right thumbstick cycles letter | Off-default room selection |
| `recenter` | A press | View recenter request |
| `host_pressed` / `join_pressed` | X / B press | User intent capture |
| `lan_host_attempt` / `lan_host_ready` / `lan_host_failed` | LAN host flow | Same-Wi-Fi diagnostics |
| `lan_join_attempt` / `lan_join_started` / `lan_join_failed` | LAN client flow | Same-Wi-Fi diagnostics |
| `relay_host_attempt` / `relay_alloc_succeeded` / `relay_alloc_failed` | Unity Relay allocation | Internet-mode allocation failures |
| `relay_host_ready` / `relay_host_voice_failed` | Host pipeline complete | Voice room property set after Relay alloc |
| `relay_join_attempt` / `relay_join_voice_timeout` / `relay_join_property_missing` / `relay_join_calling` / `relay_join_succeeded` / `relay_join_failed` | Internet client flow | Specific failure points along Relay join |
| `voice_connect_attempt` | VoiceBootstrap.Start() | Photon Voice cloud connection start |
| `voice_state` | Photon `ClientState` transitions | Connection state debugging |
| `voice_joined` / `voice_left_room` / `voice_disconnected_while_in_room` / `voice_room_join_attempt` / `voice_room_leave_requested` | Voice room lifecycle | Voice room name + timing |
| `client_connected` / `client_disconnected` | NGO callbacks | NetworkManager-level peer presence |
| `stop_pressed` / `stopped` | Editor X key | Graceful shutdown trace |
| `unity_error` | Any Unity `LogError` / `LogException` / `Assert` | Crash and error capture |
| `app_quit` | OnApplicationQuit | End-of-session marker |
| `MANUAL_MARKER` | Editor `L` key (or `DebugLogger.Marker()` from any script) | "We said now" — synchronise both logs |

Each event is one JSON line:

```json
{"ts":"2026-05-16T14:07:32.118","mono":12.473,"event":"relay_join_attempt","room":"A"}
{"ts":"2026-05-16T14:07:32.345","mono":12.700,"event":"voice_room_join_attempt","room":"A"}
{"ts":"2026-05-16T14:07:33.892","mono":14.247,"event":"voice_joined","room":"A"}
{"ts":"2026-05-16T14:07:34.011","mono":14.366,"event":"relay_join_calling"}
{"ts":"2026-05-16T14:07:35.482","mono":15.837,"event":"client_connected","id":0,"role":"client"}
{"ts":"2026-05-16T14:07:35.483","mono":15.838,"event":"relay_join_succeeded"}
```

Fields:

- `ts` — local wall-clock time, `yyyy-MM-ddTHH:mm:ss.fff`
- `mono` — `Time.realtimeSinceStartup`, seconds with millisecond precision (monotonic, immune to wall-clock drift)
- `event` — short snake_case name from the table above
- `msg` — optional free-text description
- everything else — per-event key/value details (`room`, `mode`, `id`, etc.)

### Build identity in `app_started`

Every session's first event includes build metadata read from `Assets/Resources/build-info.json` (regenerated per build by `scripts/build-quest.sh` — see [release-process.md](release-process.md)):

```json
{"ts":"2026-05-16T20:27:02.118","mono":0.412,"event":"app_started",
 "msg":"DebugLogger initialised",
 "product_name":"CampfireVR","version":"1.0",
 "platform":"Android","device_model":"Oculus Quest 3",
 "device_name":"Johan's Quest","install_mode":"Editor",
 "log_file":"campfirevr-log-20260516-202702.jsonl",
 "build_version":"v0.1.2-session-fix",
 "build_time":"2026-05-16T20:27:02+0200",
 "git_commit":"7a56b9d","git_branch":"main","git_dirty":true,
 "apk_name":"CampfireVR-v0.1.2-session-fix-20260516-2027.apk"}
```

| Field | Source | What it tells you |
|---|---|---|
| `version` | `Application.version` (= `bundleVersion`) | Binary version Android reports |
| `build_version` | CHANGELOG `## [v…]` heading | The human-friendly tag the APK was packaged under |
| `build_time` | Build clock at script invocation | When this APK was actually compiled, with TZ offset |
| `git_commit` | `git rev-parse --short HEAD` at build time | Exact commit, no guessing |
| `git_branch` | `git rev-parse --abbrev-ref HEAD` | `main` for shipping builds; feature branch for experiments |
| `git_dirty` | `true` if working tree had uncommitted changes when script ran | Trust signal — `true` means "this APK is from someone's WIP" |
| `apk_name` | The versioned filename the script produced | Cross-reference with what the tester downloaded |

If `build-info.json` is missing (e.g. the scene is being run in Editor without having built first), the build fields fall back to `"unknown"` / `false`. Builds produced by `scripts/build-quest.sh` always include it.

## Where logs are stored

On the headset:

```
/sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/
```

In the Editor (Mac):

```
~/Library/Application Support/unity-mcp-lab/CampfireVR/debug-logs/
```

Filename format: `campfirevr-log-YYYYMMDD-HHMMSS.jsonl` (one file per app launch, timestamp = first start). Each file rotates at **5 MB**; the **10 most-recent** files are kept and older ones are deleted automatically — no manual cleanup needed.

## Pulling logs from a Quest

Plug the Quest into your computer via USB-C, accept the Allow USB debugging popup if needed (see [install-on-quest.md](install-on-quest.md) Part A).

### Quickest path — `scripts/pull-quest-logs.sh`

```sh
./scripts/pull-quest-logs.sh           # → quest-logs/YYYYMMDD-HHMMSS/
./scripts/pull-quest-logs.sh --zip     # also produce quest-logs/campfirevr-logs-YYYYMMDD-HHMMSS.zip
```

The helper:

- Locates adb (env `ADB`, then `PATH`, then Unity Hub's bundled adb).
- Verifies exactly one Quest is connected + authorized, with friendly errors for the obvious failure modes (zero connected, multiple connected, popup not accepted, app never launched).
- Pulls every log file from the device into a timestamped folder.
- Writes a `README.txt` recording pull time, device serial, app version (from `dumpsys package`), source path, and the exact command used.
- Optional `--zip` packages the folder for sending out of band.

Doesn't delete anything on the headset, doesn't need Unity, doesn't need the project to be built. Send back either the unzipped folder or the zip.

### Manual adb (the long form)

### Pull all logs from one headset

```sh
adb=/Applications/Unity/Hub/Editor/6000.4.7f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$adb pull /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/ ./quest-logs/
```

(Use plain `adb` if you have Android Platform Tools on your `PATH`.)

This copies every log file into `./quest-logs/` on your computer. About 5–50 KB per session.

### Pull only the most recent session

```sh
$adb shell ls -t /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/ | head -1
# → campfirevr-log-20260516-140728.jsonl

$adb pull /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/campfirevr-log-20260516-140728.jsonl ./
```

### Pull from both headsets in one session

If both Quests are plugged into the same machine at once (rare — usually one cable, one headset):

```sh
$adb devices
# List of devices attached
# 2G0YC5ZG20031Y    device
# 1WMHHA4242C123     device

$adb -s 2G0YC5ZG20031Y pull /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/ ./johan-logs/
$adb -s 1WMHHA4242C123  pull /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/ ./friend-logs/
```

More usually, each tester pulls their own headset's logs and shares the file:

```sh
$adb pull /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/$(adb shell ls -t /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/ | head -1) ./mine.jsonl
# Send mine.jsonl over Discord / email / however you communicate.
```

### Naming convention for shared logs

When sharing across testers, **rename the file** before sending so the receiver can tell whose log it is:

```
campfirevr-log-20260516-140728.jsonl    →    johan-20260516-140728.jsonl
campfirevr-log-20260516-140730.jsonl    →    friend-20260516-140730.jsonl
```

Keep the timestamp portion — it makes timeline reconstruction trivial.

## Comparing two logs

Both files are JSONL. To eyeball them, just open in an editor side by side. To do something more programmatic:

```sh
# Pretty-print one log to terminal
cat johan-20260516-140728.jsonl | jq -c '. | {ts, event, msg}'

# Interleave two logs chronologically by ts
{ sed 's/^/[johan] /' johan-20260516-140728.jsonl;
  sed 's/^/[friend] /' friend-20260516-140730.jsonl; } |
  sort -k2 |
  head -40

# Find when a specific event happened on each side
grep relay_join_succeeded johan-20260516-140728.jsonl
grep relay_join_succeeded friend-20260516-140730.jsonl
```

The `mono` field is the monotonic seconds since each app's own start — useful for "how long after I pressed B did it actually connect?" The `ts` field is wall-clock — useful for matching up across two devices that may have different uptimes.

## Manual markers

When you and your friend hit a moment worth flagging ("voice cut out **now**"), pressing the `L` key in the Editor writes a `MANUAL_MARKER` event:

```json
{"ts":"2026-05-16T14:08:12.992","mono":53.347,"event":"MANUAL_MARKER","msg":"editor_L"}
```

In headset there's currently no button bound to `Marker()` — the in-VR controls (X / B / Y / A / stick) are all in use by host/join/mode/recenter/room. A future polish slice can add a long-press combo for an in-headset marker if the test workflow needs it.

To insert a marker programmatically from anywhere in code:

```csharp
DebugLogger.Marker("voice-cut-out");
```

## What to send back for debugging

When something fails during a test session, share these along with the report:

1. **Both log files** (renamed per the convention above), covering the failing session.
2. **The wall-clock time** the failure happened (so we know which log entries to focus on).
3. **One sentence** describing what each side saw on screen at that moment.
4. **`adb logcat -d > friend-logcat.txt`** output from the headset that failed (catches native crashes the in-app logger never sees).

That's enough information to reconstruct most multiplayer failures without needing a re-test.

## Privacy and storage notes

- **No PII.** The logger captures device model and OS-level device name (e.g. "Quest 3", "Johan's Quest") plus event timing. No voice audio, no chat content, no IP addresses, no Photon room properties beyond names like "A".
- **No cloud upload.** Files stay on the headset until you pull them via adb. There's no analytics service or external reporting endpoint.
- **No frame-rate impact.** The logger writes one JSON line per discrete event (~50–200 events per session). It's not hooked into `Update()` — only state-change detection and explicit `DebugLogger.Log()` calls.
- **File rotation.** 5 MB per file, 10 files kept, older ones auto-deleted. A heavy week of testing won't fill the Quest's storage.

## Disabling

The logger is always on for the current sideload builds — there's no "production build" yet. If you ever want to turn it off in a specific build:

1. Add a `DEBUG_LOGGING` scripting define symbol in Player Settings → Other Settings → Scripting Define Symbols.
2. Wrap the body of `DebugLogger.WriteInternal` in `#if DEBUG_LOGGING ... #endif`, default off.
3. Strip it from release builds only — keep dev/sideload builds with it on.

Not implemented yet because we're never doing release builds in the current MVP. Worth doing if the project ever ships to the Meta Quest Store.
