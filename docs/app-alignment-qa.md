# CampfireVR — alignment QA

> A genomlysning of what the app actually does (in code + scene) vs. what the docs claim it does. No functionality changed — this is an audit only. Verification stamps: **[code]** = read from source; **[scene]** = inspected via mcp-unity / Scene YAML; **[doc]** = quoted from a markdown file in `docs/` or the README; **[needs headset]** = cannot confirm from this seat.

## Executive summary

CampfireVR works. The networking, voice, presence, and UX systems are coherent, render cleanly in Built-in Render Pipeline on Quest, and the world-space tutorial walks a calm flow through `IDLE → HOSTING / JOINING → CONNECTING → CONNECTED`. The recent VFX_Fire integration is in place and the scene compiles with **0 errors, 0 warnings**.

The audit found one real bug, a handful of UX rough edges that the doc set already partly admits to, and substantial **drift between README and reality** that would mislead anyone reading the project front page. Nothing here blocks the existing remote-fika test. Most fixes are 5-minute copy edits; one (`VoiceBootstrap.OnGUI`) is a one-line guard.

The single most user-impactful question — **"is `Relay` the right user-facing word?"** — answers itself in code: it is not. Recommend renaming the visible label to **"Internet"** (or collapsing the toggle entirely, since LAN mode is effectively developer-only in this build).

## Current intended experience

Quoting `docs/vision.md`:

> Two people put on Quest 3 headsets, find themselves sitting around a small campfire at night, and talk. That is the whole thing. There is no game, no goal, no progression.

And `README.md`:

> Two people meet in VR, sit by a low-poly campfire, and talk.

Intended user journey (per README + roadmap + remote-fika-test):

1. Two users each install the same APK on a Quest 3.
2. One presses a button to **host a campfire**, the other presses a button to **join**.
3. They share a short code out of band, the joiner enters it, both end up in the same scene.
4. Voice flows spatially across the fire. Heads and hands track. No menus.
5. Either leaves; the other sees the seat empty again.

Two transport modes are documented:

| Mode | Intended use | Docs say |
|---|---|---|
| **LAN** | Same Wi-Fi / two Quests in one room | "direct IP — set `serverAddress` in the scene before building" |
| **Relay** | Two Quests on different internet connections | "Unity Relay free tier; short join code shared out of band" |

`docs/roadmap.md` calls the user-facing slice "**Cozy Quest join UX**" — confirming the design intent is *cozy*, not *technical*.

## Actual implementation

Verified by reading `Assets/Scripts/NetworkBootstrap.cs`, `TutorialOverlay.cs`, `VoiceBootstrap.cs`, `Services/ServicesBootstrap.cs`, the scene YAML, and a clean recompile.

### Modes

- `NetworkBootstrap.Mode` enum: `{ Lan, Relay }` **[code]**
- Default in the serialized scene: `Mode.Lan` **[code]**, line 19
- Toggled by left controller **Y** in idle (`OnLeftSecondary`) **[code]**, or editor **M**

### Phases (drives the tutorial overlay)

`NetworkBootstrap.Phase`: `{ Idle, Hosting, Joining, Connecting, Connected }` **[code]**, derived from a mix of `_inputState == EditingCode`, `NetworkManager.IsHost/IsClient/IsConnectedClient`, `_busy`, and `_joinCodeInput`.

### Controller bindings (Quest)

| Button | In **idle** | In **editing code** |
|---|---|---|
| **Right A** (right primary) | Recenter view | Cycle current letter +1 |
| **Right B** (right secondary) | LAN: start client. Relay: enter code editor. | Advance slot; on last slot, join. |
| **Left X** (left primary) | Start host | Cycle current letter −1 |
| **Left Y** (left secondary) | Toggle mode LAN ⇄ Relay | Previous slot; on slot 0, cancel back to idle |
| **Right thumbstick** | (no effect) | Cycle letters with deadzone 0.5, repeat 0.35 s / 0.12 s |

There is **no in-headset binding that calls `Stop()`**. The `Stop()` method exists in `NetworkBootstrap` (line 459) but is only triggered by the **editor** `KeyCode.X` (line 137). On a Quest build, users must use the Meta system menu to quit the app to leave a session. This is documented in `docs/remote-fika-test.md` as a known rough edge.

### Join code

- Alphabet: `"ABC"` **[code]**, line 56
- Length: `3` slots **[code]**, line 57
- Total possible codes: **27**
- Code entry: world-space ABC slot picker via thumbstick (primary) or A/X buttons (fallback). No `TouchScreenKeyboard`.
- Host's "alias" generated with `Random.Range` from the same ABC × 3 alphabet.
- Voice room name and Relay code property both use the alias as the lookup key (`VoiceBootstrap.JoinRoom(alias)` + `SetRoomProperty("rc", realCode)`).

### State strings (cozy copy)

Reviewed all `_state = "..."` assignments in `NetworkBootstrap.cs`. Currently friendly and concrete: *"Creating fire", "Sharing code", "Waiting for friend", "Looking for fire", "No fire found", "Friend joined", "Friend left", "Joining fire", "Couldn't reach fire", "Host's code missing", "Couldn't start fire"*. Two are slightly technical-leaning but acceptable: `"Voice room failed"`, `"Signing in"`.

### Screen-space overlays (`OnGUI`)

| Script | OnGUI present? | Editor-gated? | Visible in Quest build? |
|---|---|---|---|
| `NetworkBootstrap` | Yes (lines 506–542) | **Yes** (`if (!Application.isEditor) return;`) | No |
| `ServicesBootstrap` | Yes (lines 80–87) | **Yes** | No |
| `VoiceBootstrap` | Yes (lines 146–149) | **No** | **Yes — bug** |

### Scene

- Active build scene: `Assets/Scenes/CampfireRoom.unity` (only scene in `EditorBuildSettings.scenes`). **[scene]**
- `Assets/Scenes/Main.unity` exists (429 lines) but is **not in build settings**. Contains only a Directional Light, an `AI_Cube`, and a Main Camera — leftover from project bootstrap. Dead. **[scene]**
- `MicrophoneTest.cs` is still referenced in `CampfireRoom.unity` (originally the Voice A microphone probe — see `docs/roadmap.md`). Likely dead unless still wired to a GameObject we use; worth checking on next polish pass. **[scene]**

### Compile and Console

Verified during this audit: `recompile_scripts → 0 errors, 0 warnings`. No magenta materials, no missing references, no leftover Piloto shader errors (Piloto shader folder was cleaned up in commit `93679e5`). VFX_Fire integration (`d218b93`) intact: capsule renderer disabled, new flame PS active with `playOnAwake=true, maxParticles=20`, smoke double-muted (`maxParticles=0` + renderer disabled).

## LAN mode findings

**Status: works, but effectively developer-only.**

What works (verified in code):
- `NetworkBootstrap.Mode = Lan` is the serialized default.
- Pressing **left X** in idle on LAN calls `NetworkManager.Singleton.StartHost()` via `ConfigureLanTransport()` which sets `UnityTransport.SetConnectionData(serverAddress, port, "0.0.0.0")`.
- Pressing **right B** in idle on LAN calls `StartClient()`.
- Both also call `_voiceBootstrap.JoinRoom("lan-campfire")` so voice tracks the session.

What's broken/missing for a normal user:
- **`serverAddress` is hard-coded in the scene** (default `127.0.0.1`, line 17). To LAN-host between two real Quests, the user must edit this field in Unity before building. No runtime IP entry, no LAN discovery.
- **The host's IP isn't visible on Quest.** `NetworkBootstrap.OnGUI` shows `"Local IPs: ..."` but is gated behind `Application.isEditor`. In a Quest build, there's no way to read the host's IP from inside the headset.
- **No UI tells the user what "LAN" means.** The world-space tutorial in idle shows `mode · LAN` at the bottom but offers no explanation that LAN requires same Wi-Fi + a baked-in IP.

**Practical impact:** a vanilla user who downloads the APK and chooses LAN mode will get `Joining fire ...` → silent timeout. There is no path through LAN that succeeds without developer-side scene editing.

## Internet / Relay mode findings

**Status: works as designed, but the word "Relay" is wrong for a user.**

What works (verified in code):
- Pressing **left X** in idle on Relay → `StartHost()` → `_services.HostRelayAsync()` (Unity Multiplayer Service with `WithRelayNetwork()`) → returns a Relay code → 3-char ABC alias generated → voice room joined under that alias → Relay code published as a Photon room property `rc`.
- Pressing **right B** in idle on Relay → enters the world-space ABC code editor (`EnterCodeEditor()`).
- After the joiner enters 3 letters and presses B on the last slot → `StartClient()` → voice room joined by alias → polls Photon for `rc` property → `JoinRelayAsync(realCode)` connects NGO via Relay.
- State strings walk a readable arc: `Creating fire → Sharing code → Waiting for friend → Friend joined` (host side), `Enter code → Looking for fire → Joining fire → Connected` (client side).

What's confusing for a user:
- The UI uses the word **"Relay"** (the `Mode` enum name leaks into `mode · Relay` via `ToString()` in `TutorialOverlay.cs:123`). "Relay" is Unity dashboard jargon — most users have no model for what it means. They want to know "do my friend and I have to be on the same Wi-Fi?" The correct user-visible word is **"Internet"** (or "Online").
- **27 possible codes.** ABC × 3 = 27. Two concurrent sessions on the public service have a real (though small) chance of colliding on the same alias. The remote-fika doc already calls this out as a known rough edge.

## UX / copy findings

**Idle overlay (world-space):**

```
🔥  CAMPFIRE

X  host
B  join / next / confirm
Y  back / mode
A  change / recenter

mode · LAN              ← (or Relay)
```

- The "universal legend" presents X/B/Y/A with multi-role labels (covering Idle + Joining contexts). This works in Idle but is a half-truth in Hosting/Connecting/Connected:
  - **`Y back / mode`** in Connected state actually means "toggle the mode enum *for the next session*" — it does not leave the current session. Pressing Y mid-session would silently flip `mode`. The Y label could mislead a user into expecting "back" to mean "back out of the session."
  - **`A change / recenter`** in Connected state means recenter (since `_inputState != EditingCode`). The `change` half is dead context.
- "mode · LAN" gives no indication that LAN is functionally broken for a non-developer.

**Hosting overlay:**

```
🔥  YOUR FIRE

share code
A B C

waiting for friend ...

X  host
B  join / next / confirm
Y  back / mode
A  change / recenter
```

- "share code" is good copy.
- The universal legend is shown but most of these buttons are not safe to press while hosting (B → "enter code" would try to join your own session; Y → toggles mode mid-host; X → no-op via `_busy` guard).

**Joining overlay (code editor):**

```
🔥  JOIN FIRE

[A] B C

stick  change letter
B      next slot      (or "join" on the last slot)
Y      back
```

- This is the strongest legend in the app. Three buttons, all relevant, all correct. The B label flips intelligently to "join" on the last slot.

**Connected overlay:** blank after 5-second `🔥 Friend joined` notification. Calm — correct.

**Notifications:** every state-string change shows for 5 s with a cycling-dot spinner during `_busy`. The wording is uniformly cozy and short.

## Bugs / mismatches

### Real bugs

1. **`VoiceBootstrap.OnGUI` leaks into Quest builds.** `NetworkBootstrap.OnGUI` and `ServicesBootstrap.OnGUI` are both gated behind `if (!Application.isEditor) return;`. `VoiceBootstrap.OnGUI` (lines 146–149) is **not**. Result: in headset, a small status line like `"Voice: connecting…"` / `"Voice connected (ABC)"` / `"Voice: left room"` renders as a screen-space overlay at position (20, 380), in front of the cozy VR scene. **[code]**, easy fix: one-line guard.

2. **`Stop()` has no Quest binding.** The graceful disconnect path (`_voiceBootstrap.LeaveRoom() + _services.LeaveRelayAsync() + nm.Shutdown()`) only fires on editor `KeyCode.X`. README claims "press **left Y** on either side" stops the session — actually left Y toggles the mode enum, which is a different thing entirely. **[code]**

### README drift (real)

3. **README says the code is 6 characters.** Code is 3 characters from {A,B,C}. **[code: `CodeLength = 3`, `CodeAlphabet = "ABC"`]** The README description of "the 6 characters spaced apart" predates the `Simplify Quest join codes to ABC aliases` slice (`a14a291`).
4. **README says code entry uses Quest's TouchScreenKeyboard.** Actually entry is via world-space ABC slot cycling with thumbstick + A/X fallback. The keyboard is no longer used anywhere in the app. **[code]**
5. **README "Stop: press left Y on either side"** — left Y does not stop. **[code: `OnLeftSecondary → ToggleMode()`]**
6. **README "press right A to host"** — right A in idle calls `Recenter`, not host. Host is left X. **[code: `OnRightPrimary → Recenter()` when not editing]**
7. **README "Mode toggle: press left X"** in the multiplayer-testing table — left X starts the host, doesn't toggle mode. Mode toggle is left Y. **[code]**
8. **README "Next slices"** section lists Voice chat, ambient crackle, dimmer ambient, and remote PlayerHead placement as "next" — all of these are already in `Done` per `docs/roadmap.md` and in the codebase.

### Roadmap.md drift

9. **`docs/roadmap.md` lists Voice C/D/E twice:** once under `## Next — Voice slices` as `✓ Voice C – Minimal remote voice` / `✓ Voice D – Spatial campfire voice`, then **again** a few lines later as `**Voice C — Mono voice between two Quests.**` (no ✓) and `**Voice D — Spatial voice.**` (no ✓) — the same slices listed both as done and as next. The list got appended to without removing the older bullets when the slices shipped.

### Scene / code drift

10. **`Assets/Scenes/Main.unity` is dead.** Not in build settings, contains only a Directional Light, an `AI_Cube`, and a default Main Camera. Leftover. Safe to delete in a cleanup slice.
11. **`MicrophoneTest.cs` is still referenced in `CampfireRoom.unity`.** Originally the Voice A probe per roadmap; should have been removed after Voice C shipped. Confirm it's not on a live GameObject before removing.

### Minor / cosmetic

12. **Stop() sets `_state = "Disconnected"`** — that string will never be seen in Quest because Stop() never fires there.
13. **Universal legend `Y back / mode`** mixes two semantically very different actions. "Back" implies undo; "mode" implies a setting toggle. In editing they map to "back"; in idle/hosting/connecting/connected they all map to "mode toggle". Confusing label.
14. **`NetworkBootstrap.EnsureStyles()`** is only ever used by the editor-gated `OnGUI()` — fine, but worth noting that the styles allocate every editor session.

## Suggested small fixes (do not implement yet)

In rough priority order:

1. **Gate `VoiceBootstrap.OnGUI` behind `Application.isEditor`** — 1-line fix, removes a dev-debug overlay from headset.
2. **Update `README.md` "Multiplayer testing" and "Relay flow" sections** to reflect the current 3-char ABC code, thumbstick entry, no Stop button, and accurate button mappings. Drop the `TouchScreenKeyboard` mention.
3. **Update `README.md` "Next slices"** — remove items that are already done; either delete the section or repoint it at `docs/roadmap.md`.
4. **Deduplicate Voice C/D/E in `docs/roadmap.md`** — remove the second copy that's listed as not-done.
5. **Rename UI label `Relay → Internet`** in `TutorialOverlay.cs`. Keep `Mode.Relay` internally so we don't ripple through code. Example: `string ModeLabel(NetworkBootstrap.Mode m) => m == NetworkBootstrap.Mode.Relay ? "Internet" : "Same Wi-Fi";`.
6. **Hide the LAN option from the user** (or gate Y mode-toggle to editor only) until LAN gets runtime IP entry. Right now picking LAN on a vanilla Quest install is a dead end.
7. **Bind `Stop()` to a Quest button.** Suggestions in priority order:
   - Long-press left Y (>1.5 s) → leave session. Keeps the existing short-press Y for mode toggle in idle.
   - Or: gate Y differently per phase — in Idle, Y = mode; in Connected/Hosting/Connecting, Y = leave.
8. **Update the legend per phase** instead of "universal": Connected → no legend (already correct); Hosting/Connecting → just `A recenter / Y leave` once Stop is bound.
9. **Delete `Assets/Scenes/Main.unity`** after one Editor open confirms it has no references in any prefab/script.
10. **Audit `MicrophoneTest.cs`** — if not on any active GameObject, delete the script.

## Suggested terminology changes

| Current label | Where | Suggested user-facing | Rationale |
|---|---|---|---|
| `Mode: Relay` / `mode · Relay` | Tutorial overlay idle | `mode · Internet` | "Relay" is Unity dashboard jargon. Users want to know "Internet or same Wi-Fi". |
| `Mode: LAN` / `mode · LAN` | Tutorial overlay idle | `mode · Same Wi-Fi` (if kept) | Spells out the constraint. Alternative: hide LAN entirely. |
| `share code` | Hosting overlay | `share code with your friend` | Slightly more directive. Optional. |
| `JOIN FIRE` | Joining overlay header | `enter friend's code` | Reads as "what am I doing" instead of "where am I". Optional — current is fine if kept. |
| `Y  back / mode` | Universal legend | Per-phase: `Y back` in editing, `Y mode` in idle, `Y leave` in connected (after binding Stop). | One verb per button per phase reduces cognitive load. |
| Editor-only word "Relay" inside `NetworkBootstrap.cs` enum | Code | Keep as `Relay` | No reason to ripple terminology through code; internal name is fine. |

## Recommended next slice

**Title:** *Fix Quest disconnect + tighten copy*

One small commit, all changes are 5-minute edits and one trivial guard:

1. Add `if (!Application.isEditor) return;` to `VoiceBootstrap.OnGUI` (mirror the pattern in `NetworkBootstrap` and `ServicesBootstrap`).
2. Rewrite the README's "Multiplayer testing" and "Relay flow" sections to match the current ABC + thumbstick flow. Delete the stale TouchScreenKeyboard paragraph. Fix the button binding table.
3. Deduplicate the Voice C/D/E entries in `docs/roadmap.md`.
4. Trim README's "Next slices" section.
5. Add a `ModeLabel()` helper in `TutorialOverlay.cs` so the visible label is `Internet` / `Same Wi-Fi` instead of `Relay` / `LAN`. Keep `Mode.Relay` / `Mode.Lan` in the C# enum.

**Defer** (separate, slightly larger slice): bind `Stop()` to a Quest button (long-press Y is the lowest-friction option), per-phase legend, and a decision on whether to remove LAN from the visible UI entirely. Those touch input semantics that the user has tested in headset; safer to ship the copy + bug fix first and let the input change ride its own remote-fika round.

**Verification stamps after this slice:**
- `recompile_scripts` clean
- Editor Play Mode in flat-screen: confirm no `Voice:` overlay
- One quick Quest build: confirm `mode · Internet` reads cleanly, voice overlay is gone, host/join flow still walks the documented arc

That's it — a tight cleanup pass that removes the worst README drift, kills the one real bug, and decouples internal "Relay" jargon from what the user sees.
