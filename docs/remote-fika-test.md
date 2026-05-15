# Remote Fika — validation test

A 15–20 minute structured hangout session for two people on different internet connections, each wearing a Meta Quest 3. The point is not to test networking; it is to find out whether the campfire actually feels like a place where two people can spend time together.

## What we are validating

- **Presence**: does the other person feel *there*, not like a video call?
- **Comfort**: is it pleasant to be in for 15+ minutes, or fatiguing?
- **Conversation flow**: do natural pauses feel cozy or empty?
- **Tech reliability**: does anything break mid-session?

If any of those answers are "no", we know what to slice next. If they are "yes", we know the vision is reachable and can start adding texture.

## Build for this test

The test build is `CampfireVR-remote-fika-test-v0.1.apk`, produced from `main` at the time of this doc.

To rebuild from a fresh checkout:

1. Open `UnityProject/` in Unity 6 with Android Build Support installed.
2. Run **Tools → Quest Setup → Configure Project for Quest 3** once (idempotent — sets IL2CPP/ARM64/Vulkan/Oculus loader).
3. Run **Tools → Quest Setup → Build Remote Fika APK**. The output lands at:
   ```
   UnityProject/Builds/CampfireVR-remote-fika-test-v0.1.apk
   ```
   `Builds/` is gitignored. The APK isn't checked in — each tester rebuilds from `main`.

`Cmd+B` still works for ad-hoc builds; the menu just guarantees the right name and output path for this campaign.

## Before the session

### One-time install per device

Each Quest needs `CampfireVR-remote-fika-test-v0.1.apk` installed.

1. Plug in Quest via USB-C, accept the trust dialog inside the headset.
2. `adb install -r UnityProject/Builds/CampfireVR-remote-fika-test-v0.1.apk` from the repo root.
3. Repeat for the second Quest.

The package id is `com.unitymcplab.campfireroom`. Earlier installs (when the product was named `CampfireRoom`) share the package id, so `adb install -r` upgrades cleanly. The on-headset app icon will still read **CampfireRoom** until we rename it — see "known rough edges" below.

### One-time on each Quest

1. Developer mode on, USB debugging allowed (already done for our two devices).
2. First launch: accept the **microphone permission** dialog when it appears. Without it, voice never goes anywhere — Photon Voice will silently produce no audio rather than retrying.

### Per-session setup (~5 min)

The overlay drives one of five states (idle, hosting, joining, connecting, connected). Look at the world-space `🔥` panel in front of the campfire — it always tells you what to do.

1. **Pick a host.** Doesn't matter which; the experience is symmetric.
2. **Host:** opens the app. Panel reads `🔥 CAMPFIRE` with the universal legend.
   - If `mode · LAN` is shown at the bottom, press **left Y** to flip to Relay (the line should change to `mode · Relay`).
   - Press **left X** to host. Panel switches to `🔥 YOUR FIRE`, then `Creating fire ...` (with cycling dots), then `Sharing code` and finally:
     ```
     🔥  YOUR FIRE
     share code
     A B C
     waiting for friend ...
     ```
3. **Host reads the 3-letter code out loud** over a phone call / Discord / SMS. The alphabet is only A/B/C, so 27 possible codes — repeat once if you suspect a misheard letter.
4. **Guest:** opens the app, makes sure the bottom line reads `mode · Relay` (press **left Y** to toggle if not), then **right B** to enter the code editor. Panel switches to `🔥 JOIN FIRE` with three slots, the first one bracketed: `[A] B C`.
5. **Guest enters the code.** No keyboard — it's all controller input:
   - **Right thumbstick** to change the current letter (right or up = next, left or down = prev). A short flick changes one letter; holding the stick auto-cycles after ~0.35s.
   - **Right B** advances to the next slot (the brackets jump to the next position) and, on the third slot, becomes "join".
   - **Left Y** goes back a slot, or cancels back to idle from slot 1.
   - **A / X buttons** are silent fallbacks if a stick isn't responding — A = next letter, X = prev.
6. **Guest presses B on the last slot to join.** Panel switches to `🔥 CAMPFIRE` with `Joining fire ...` (cycling dots). Within a few seconds:
   - Guest sees a brief `🔥 Connected` notification, then the panel goes blank — the campfire is the focus.
   - Host sees `🔥 Friend joined` for ~5 seconds, then blank.
7. **Voice handshake.** Photon Voice usually catches up within ~3 seconds of the NGO connection. Say *"can you hear me?"* — confirm voice both ways before continuing.

If voice doesn't come through within ~5 seconds: there's no in-app disconnect button yet — quit to the system menu and relaunch on both sides. If it still doesn't, fall back to a phone call for this session and capture the failure in the retro.

## Physical setup recommendations

| | Recommendation | Why |
|---|---|---|
| Seat | A real chair you can sit in comfortably for 20 min, arms supported | Quest 3 + headphones get heavy; arm support keeps hands relaxed for `LeftHandAnchor` / `RightHandAnchor` tracking |
| Room | Quiet, normal living room acoustics | Photon Voice noise suppression handles background but not heavy reverb |
| Audio | **Use headphones**, in-ear or over-ear | Quest's built-in speakers leak into the mic of the *other* Quest if you're at all close to each other; headphones eliminate that |
| Volume | Quest system volume around **40–50%** | Voice and crackle are mixed below 1.0; full system volume can sting |
| Lighting | Soft / dim physical room is fine | Quest 3 inside-out tracking is robust; no need to optimise |
| Position | Sit in your real room facing forward, no need to spin | The seat is fixed; you don't have to physically turn to look around — head tracking does it |
| Distance to walls | At least 50 cm from anything you might gesture into | Quest will draw a real-world Guardian outline if you get close |

## Recommended test scenarios (in order)

Each is 3–5 minutes. The session is over when you've done all six or 20 minutes have passed, whichever comes first.

### 1. Warm-up — the first 60 seconds
Just sit. Look at the fire. Look at your hands. Look across at your friend. Don't try to fill the silence. **Notice**: does it feel like you are in a place, or like you are in a 3D viewer?

### 2. Greeting + small talk
"Hej, hur är läget?" through normal opening pleasantries. **Notice**: does the voice direction feel right? Does it match where the head sphere is?

### 3. Storytelling
One of you tells the other a 2-minute story about something that happened this week. The listener does not interrupt. **Notice**: as the listener, does the speaker's head movement give you presence cues? As the speaker, is it strange to talk to a sphere?

### 4. Joint silence
Stop talking for 60 seconds on purpose. Both of you. Just listen to the fire. **Notice**: is the silence cozy or awkward? Does the fire crackle fill the space enough?

### 5. Gesture talk
Both wave with controllers. Point at each other. Point at the fire. **Notice**: do the hand cubes feel like *your* hands or like floating debris? Do they line up with where you think your real hands are?

### 6. Goodbye
Decide to leave. Wave goodbye. There's no in-app disconnect button in this build — both of you press the **Meta button** to return to the Quest home, then close the app from the dock. **Notice**: does the goodbye feel like leaving a place, or like ending a call? Note: the lack of a graceful in-VR disconnect is on the rough-edges list.

## Recommended headset settings

- **System volume**: 40–50%. If it feels too quiet after that, the build is wrong, not the headset.
- **Eye relief / IPD**: take a moment to adjust before starting. Eye fatigue ruins immersion.
- **Top strap tightness**: a touch tighter than usual; you'll be still for 20 min and a slipping headset breaks presence.

## After the session — questions to discuss

Talk these through together, ideally still in VR for the first one or two:

1. **What surprised you?** (positive or negative — don't filter)
2. **Was there a moment it felt real?** When?
3. **Was there a moment it stopped feeling real?** What broke it?
4. **Did silence feel okay?** Or did you want to fill it?
5. **Did you forget the headset was on?** Even briefly?
6. **What's the first thing you'd add?**
7. **Would you do this again next week?**

## Retrospective template — fill in together right after

Copy-paste this section into a session note and fill it in while it's fresh.

```
## Remote Fika session — YYYY-MM-DD

Duration: __ min
Network: A on __ , B on __  (WiFi / 5G / wired)

What felt good
- 

What broke immersion
- 

What felt cozy
- 

What was awkward
- 

Technical issues
- (timestamp, what happened, did it recover)

One-line vibe
"..."

Next slice we want
- 
```

## Known rough edges

Things we already know about. Not blockers for this test — just heads-ups so you can shrug them off and stay in the experience.

- **App icon still says "CampfireRoom".** The Unity `productName` hasn't been updated; the on-headset name predates the rebrand to CampfireVR. Functional only — same package id, same APK, just a stale label.
- **`bundleVersion` is `1.0`, the APK file is `v0.1`.** The two are independent on Android. The file name is the test campaign tag; the on-device "version" you'd see in Quest's app info reads 1.0. Nothing to do during the session.
- **No in-app version display.** You can't see which build you're running from inside VR. Confirm via `adb shell dumpsys package com.unitymcplab.campfireroom | grep versionName` if there's any doubt.
- **No in-VR disconnect.** When you're done, press the Meta button and close from the dock. There's no "leave fire" button on the controller in this build.
- **Y also toggles mode while connected.** If you accidentally press left Y mid-session it'll flip the mode label between Relay and LAN. It does *not* drop the existing connection — a follow-up slice will gate Y to idle-only.
- **B/X also do something while hosting.** Pressing them while you're already hosting will try to start another action (B → enter code editor, X → re-attempt host). Mostly harmless but can produce confusing notifications. Just don't press them after the code is shared.
- **27 possible codes.** ABC × 3 slots = only 27 combinations. Two simultaneous sessions could in theory collide on the same alias. For one paired session this is fine; we'll widen the alphabet when we have more concurrent users.
- **Emoji in the panel may render as `?` on Quest.** TextMesh uses the legacy Arial font, which sometimes lacks `🔥`. If the panels show boxes/question marks where fires should be, the rest of the UX still works — note it in the retro.
- **Voice cuts out for 2–5 sec** — Photon Relay region hop or WiFi blip. Should recover automatically. Note the timestamp.
- **Hand cubes float to weird positions** — happens when a controller lost tracking. Bring the controller back into view.
- **Other person's head sphere drifts away from the seat** — should be fixed now that head/hands are sent in seat-relative coordinates. If it recurs, we need to know.
- **Fire crackle too loud / too quiet** — currently `volume = 0.3`. Note your preference; we can dial it.
- **You hear your own voice as an echo** — means the other Quest's speakers are feeding back into its mic. Use headphones.
- **Quest system menu pops up mid-session** — the Meta button accidentally pressed pauses the app. Photon and NGO usually survive a brief pause; if not, both quit and relaunch.
- **One side says voice/network has dropped** mid-session — Photon Cloud connection dropped. Currently no auto-reconnect; both quit, relaunch, redo the host/join flow with a fresh code.

## What this test will *not* validate

- Lip sync (we don't have any)
- Facial expression (we don't have any)
- Body language beyond head + hands (we don't have any)
- Long-form (1h+) comfort
- More than 2 people
- Cross-platform (Quest only for now)

If those things turn out to be missing-in-a-way-that-hurts, that becomes the next slice. If the 20 minutes felt good without them, we know low-fidelity presence is enough and can keep slicing in that direction.
