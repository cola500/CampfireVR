# Remote Fika — validation test

A 15–20 minute structured hangout session for two people on different internet connections, each wearing a Meta Quest 3. The point is not to test networking; it is to find out whether the campfire actually feels like a place where two people can spend time together.

## What we are validating

- **Presence**: does the other person feel *there*, not like a video call?
- **Comfort**: is it pleasant to be in for 15+ minutes, or fatiguing?
- **Conversation flow**: do natural pauses feel cozy or empty?
- **Tech reliability**: does anything break mid-session?

If any of those answers are "no", we know what to slice next. If they are "yes", we know the vision is reachable and can start adding texture.

## Before the session

### One-time install per device

Each Quest needs the current build of `CampfireRoom.apk` installed.

1. On the build machine: `Cmd+B` in Unity → produces `.apk`.
2. Plug in Quest A via USB-C, `adb install -r path/to/CampfireRoom.apk`. Repeat for Quest B.

(Or `Build And Run` while the right Quest is plugged in.)

### One-time on each Quest

1. Developer mode on, USB debugging allowed (already done for our two devices).
2. First launch: accept the **microphone permission** dialog when it appears. Without it, voice never goes anywhere.

### Per-session setup (~5 min)

1. Pick a host. Doesn't matter which; the experience is symmetric.
2. Host opens the app first, presses **left Y** to switch to Relay, then **left X** to host. The big `CAMPFIRE CODE` appears. The state line should read `Waiting for friend…`.
3. Host reads the code out loud over a phone call / Discord / SMS.
4. Guest opens the app, presses **left Y** to switch to Relay, then **right B**. Quest's system keyboard pops up — type the 6 characters, press Done.
5. State should walk `Enter the campfire code… → Connecting to ABCDEF… → Connected` and on the host's side `Friend joined the fire`.
6. Within ~3 seconds, `Voice connected (ABCDEF)` should appear on both. Say *"can you hear me?"* — confirm voice both ways before continuing.

If voice doesn't come through within 5 seconds: left Y (Stop) on both sides, retry. If it still doesn't, fall back to a phone call for this session and capture the failure in the retro.

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
Decide to leave. Wave goodbye. One person presses **left Y** to disconnect, then the other. **Notice**: does the goodbye feel like leaving a place, or like ending a call?

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

## Likely weak spots to watch for

These are the parts of the prototype most likely to misbehave. If you notice any, write the symptom in the retro — the more specific, the better the next slice can target it.

- **Voice cuts out for 2–5 sec** — Photon Relay region hop or WiFi blip. Should recover automatically. Note the timestamp.
- **Hand cubes float to weird positions** — happens when a controller lost tracking. Bring the controller back into view.
- **Other person's head sphere drifts away from the seat** — has happened in earlier slices; should be fixed now that head/hands are sent in seat-relative coordinates. If it recurs, we need to know.
- **Fire crackle too loud / too quiet** — currently `volume = 0.3`. Note your preference; we can dial it.
- **You hear your own voice as an echo** — means the other Quest's speakers are feeding back into its mic. Use headphones.
- **Quest system menu pops up mid-session** — the Meta button accidentally pressed pauses the app. Photon and NGO usually survive a brief pause; if not, left Y to disconnect and rejoin.
- **One side says `Voice: Disconnected`** mid-session — Photon Cloud connection dropped. Currently no auto-reconnect; left Y on both sides and rejoin via the same code.

## What this test will *not* validate

- Lip sync (we don't have any)
- Facial expression (we don't have any)
- Body language beyond head + hands (we don't have any)
- Long-form (1h+) comfort
- More than 2 people
- Cross-platform (Quest only for now)

If those things turn out to be missing-in-a-way-that-hurts, that becomes the next slice. If the 20 minutes felt good without them, we know low-fidelity presence is enough and can keep slicing in that direction.
