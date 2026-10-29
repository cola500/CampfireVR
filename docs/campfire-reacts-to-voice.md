---
title: Campfire reacts to voice
description: The campfire subtly brightens and flickers a little more while the local player is talking, then settles back over 1-2 seconds of silence. Pure wiring of two existing systems — no new assets, no new networking.
category: slice
status: awaiting-editor-validation
last_updated: 2026-08-21
sections:
  - Why this slice
  - Implementation
  - Parameters
  - Reasoning behind the values
  - What was intentionally not done
  - Scene application path
  - Debug output
  - Validation
  - Future expansion ideas
---

# Campfire reacts to voice

> Tiny presence pass: the fire listens. When the local player is talking, `FireLight` runs slightly brighter and flickers slightly more; it settles back to its normal rhythm within 1-2 seconds of silence. Two small script changes, zero new assets, zero networking changes, zero particle-system edits.

## Why this slice

From `docs/campfire-experience-spike.md`'s ranking: this was the highest value-per-line idea in the whole discovery catalogue, because both halves already existed in code and had simply never been connected — `VoiceBootstrap.IsTransmitting` (a public bool, local mic only) and `FireLightFlicker` (a `Light` intensity/color driver with no external hook). The goal isn't a gameplay signal, it's the environment quietly agreeing that a conversation is happening — "I don't know why, but the campfire somehow feels alive."

## Implementation

Two files changed, one file added, one Editor helper added:

| File | Change |
|---|---|
| `Assets/Scripts/Environment/FireLightFlicker.cs` | Added two public properties, `IntensityMultiplier` and `FlickerMultiplier` (default `1f`), multiplied into the existing `baseIntensity` / `flickerAmount` terms in `Update()`. At the default value the computed light intensity is bit-for-bit identical to before this slice — the multiplier is a pure hook, not a behaviour change on its own. |
| `Assets/Scripts/Environment/FireVoicePresence.cs` (new) | Reads `VoiceBootstrap.IsTransmitting` every frame, ramps a `0→1` presence value linearly over `rampSeconds`, and drives both `FireLightFlicker` multipliers toward a "talking" target via `Mathf.Lerp(1, target, presence)`. Logs a debug event only on the talking/not-talking edge. |
| `Assets/Editor/Environment/FireVoicePresenceSetup.cs` (new) | `Tools/Quest Setup/Apply Fire Voice Presence` — idempotent menu item that attaches `FireVoicePresence` to the `FireLight` GameObject. Mirrors the existing `TreeWindSetup.cs` pattern. A `Remove Fire Voice Presence` companion menu undoes it. |

Both new/changed runtime scripts live in `Scripts/Environment/`, next to `PresenceBreath.cs` and `FaceTarget.cs` whose style they follow: auto-find the collaborator in `OnEnable` (`FindFirstObjectByType<T>()`), no coroutines, no per-frame allocations.

### Data flow

```
VoiceBootstrap.IsTransmitting (local mic, already public)
        │  read every Update()
        ▼
FireVoicePresence._presence   (0..1, Mathf.MoveTowards ramp, rampSeconds each way)
        │  Mathf.Lerp(1, talkingMultiplier, presence)
        ▼
FireLightFlicker.IntensityMultiplier / FlickerMultiplier
        │  multiplied into the existing Perlin-noise flicker computation
        ▼
Light.intensity / Light.color   (unchanged FireLightFlicker.Update() otherwise)
```

No new GameObjects, no new components on anything except `FireLight`, no scene structural changes beyond that one component add.

## Parameters

All on `FireVoicePresence`, exposed in the Inspector:

| Field | Default | Notes |
|---|---|---|
| `talkingIntensityMultiplier` | `1.08` (108%) | Within the spec's suggested 105-110% range. Applied to `FireLightFlicker.IntensityMultiplier`. |
| `talkingFlickerMultiplier` | `1.08` (108%) | Same range, applied to `FireLightFlicker.FlickerMultiplier` — the flame's flicker swing gets very slightly wider, not just brighter. |
| `rampSeconds` | `1.5` | Linear ramp time each direction (silence→talking and talking→silence use the same rate). Within the spec's "smooth over 1-2 seconds" window. |
| `logStateChanges` | `true` | Gate on the debug log call. Set false to silence even the edge-triggered logging. |
| `fireLightFlicker` / `voiceBootstrap` | auto-found | `FindFirstObjectByType<T>()` in `OnEnable`; only needed as manual overrides if the scene ever has more than one of either component. |

## Reasoning behind the values

- **1.08× on both intensity and flicker amplitude, not just intensity.** Intensity alone would read as "the light got brighter," which is closer to conscious than the brief wants. Nudging the flicker amplitude too means the fire's *rhythm* changes slightly as well as its brightness — closer to "more alive" than "more lit."
- **Linear ramp via `Mathf.MoveTowards`, not an eased curve.** The brief is explicit: never pulse rapidly, never flash. A linear ramp is the simplest way to guarantee a bounded, monotonic transition with no overshoot — matches the project's existing preference for the simplest math that does the job (`SubtleTreeWind`, `PresenceBreath` are the same shape of simple).
- **Multiplying into the existing flicker computation rather than replacing it.** `FireLightFlicker` still owns 100% of the moment-to-moment look (the Perlin-noise flicker). `FireVoicePresence` only ever nudges the ceiling and floor of that existing motion. This is why the "idle fire looks unchanged" acceptance criterion holds by construction — at `presence = 0`, both multipliers are exactly `1`, which is exactly today's behaviour.
- **Local mic only, no network sync.** `VoiceBootstrap.IsTransmitting` reflects the local player's own microphone. Per the brief, no new networking was introduced — each headset's fire reacts to that headset's own player talking, not to the remote player. (A synced "either player talking" version was flagged as a possible follow-up in the discovery spike, deliberately not built here.)

## What was intentionally not done

- **Ember activity.** The brief said "slightly increase ember activity **if already available**" but also listed particle systems under "do NOT modify." `Embers` has no existing runtime-controllable hook (no wrapper script, just a static `ParticleSystem`), so under the "if already available" reading, it isn't — and touching it would mean editing a particle system, which the constraints list rules out. Left for a future slice that would first need to add a small controllable emission-rate hook to `Embers`, mirroring what this slice just did for `FireLightFlicker`.
- **No changes to the flame particle system, campfire mesh, audio, networking, multiplayer, or shaders.** Confirmed by inspection: the only runtime types touched are `FireLightFlicker` (already on the allowed list) and the new `FireVoicePresence`.
- **No remote-player reaction, no `NetworkVariable`.** Explicitly out of scope per "do not introduce new networking."

## Scene application path

`FireVoicePresence` is **not yet attached to the scene** — this session had no live Unity Editor / `mcp-unity` connection to add it and no way to run `recompile_scripts` to confirm a clean compile. Next time the Editor is open:

```
Tools → Quest Setup → Apply Fire Voice Presence
```

Idempotent — re-running is a no-op if already applied. `Tools → Quest Setup → Remove Fire Voice Presence` undoes it.

## Debug output

One event, logged only on the talking/not-talking edge (never per-frame), via the existing `DebugLogger`:

```json
{"ts":"...","mono":123.456,"event":"fire_reacts_to_voice","talking":true}
{"ts":"...","mono":131.202,"event":"fire_reacts_to_voice","talking":false}
```

Matches the existing JSONL convention (`docs/debug-logging.md`) and the same "log on transition, not per-frame" discipline already used throughout `VoiceBootstrap.cs` and `DogIdleBehaviour.cs`.

## Validation

Not yet run — recorded here so the next Editor session knows exactly what's outstanding:

- [ ] `recompile_scripts` (or opening the Editor) — confirm 0 new compile errors/warnings from `FireLightFlicker.cs`, `FireVoicePresence.cs`, `FireVoicePresenceSetup.cs`.
- [ ] `Tools → Quest Setup → Apply Fire Voice Presence` — confirm it logs `Added FireVoicePresence to FireLight.` and the scene marks dirty.
- [ ] Idle fire looks unchanged (both multipliers sit at `1` when `IsTransmitting` is false).
- [ ] Talking locally gently lifts the fire; releasing the mic settles it back within ~1.5 s, no visible pop or flash.
- [ ] `fire_reacts_to_voice` appears in the JSONL log exactly on transmit-state edges, never per-frame.
- [ ] No measurable frame-rate impact — the added `Update()` cost is one bool read, one `MoveTowards`, two `Lerp`s, well under the sub-millisecond cost already measured for comparable scripts (`PresenceBreath`, `SubtleTreeWind`) in `docs/performance-checklist.md`.
- [ ] No networking-related log lines or `NetworkVariable` changes appear anywhere in the diff (there are none — confirmed by inspection, worth re-confirming after any future edit).

## Future expansion ideas

Carried over verbatim from the brief, not implemented in this slice:

- **Dog reacts to voice** — already scoped in `docs/dog-idle-behavior-slice.md`'s "Future expansion ideas" and in `docs/campfire-experience-spike.md`'s top-3 recommendation: swap `DogIdleBehaviour`'s random glance timer for an edge-trigger on the same `VoiceBootstrap.IsTransmitting` this slice reads.
- **Dinosaur reacts to voice** — same pattern as the dog, once a second companion exists (see the spike's "shared `CompanionIdleBehaviour`" note).
- **Companions react to fire brightness** — a companion script could read `FireLightFlicker.IntensityMultiplier` (now public, thanks to this slice) instead of `VoiceBootstrap` directly, so companions respond to "the fire is lively" rather than duplicating the voice check.
- **Subtle ember bursts** — the deferred half of this slice's own brief; needs a small controllable hook added to `Embers` first (an emission-rate multiplier, same shape as `FireLightFlicker`'s new properties).
- **Fireflies react to fire** — if the fireflies idea from the discovery spike ships, its drift/flicker could read the same `IntensityMultiplier` for a shared "the whole clearing is a little more alive" moment.
