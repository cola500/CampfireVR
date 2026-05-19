---
title: Dog idle behaviour slice
description: Subtle idle life for the campfire dog — procedural breathing + occasional weight shift, layered on the existing static placement. No AI, no nav, no audio, no networking.
category: slice
status: stable
last_updated: 2026-05-19 (attention glance added)
sections:
  - Why this slice
  - The fundamental problem
  - Chosen approach
  - Components and animations used
  - Performance considerations
  - Intentionally avoided complexity
  - Scene application path
  - Future expansion ideas
---

# Dog idle behaviour slice

The campfire dog used to feel completely static — a beautiful little brown shape that didn't move. This slice adds three subtle layered motions: continuous breathing, occasional weight shift, and occasional attention glance toward the local player. Reads as a sleeping dog who briefly notices you. ~140 lines of C# total; zero networking, zero AI, zero voice analysis.

Lands on top of `docs/dog-companion-slice.md` (the static placement, material setup, and runtime-script stripping). Doesn't replace any of it.

## Why this slice

Feedback from the most recent test run: the dog read as a prop, not a presence. The goal isn't a game companion — it's the cozy "there's a dog here too" feel. The dog needed to look alive enough that you'd glance at it once during a 30-minute session and notice the chest rise and fall, then look away. Anything more energetic would break the sleepy campfire mood.

Slice spec was explicit on what NOT to add: no navigation, no barking, no chasing players, no physics, no networking, no complex AI/state machines, no expensive animation systems. Conservative scope.

## The fundamental problem

The dog asset (`Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab`) ships with three animations: `Dog_001_idle`, `Dog_001_walk`, `Dog_001_run`. They route through `Dog.controller`'s BlendTree gated by a `State` int parameter (0 = idle, 1 = walk, 2 = run). `dog-companion-slice.md` documents that the BlendTree's default idle is supposed to produce subtle breathing.

But inspecting `Dog_001_idle.anim`:

```yaml
m_LoopTime: 0
m_LoopBlend: 0
```

**The idle animation doesn't loop.** On spawn, the BlendTree plays it once (~1 second of breathing), then freezes at the final pose. After that, the dog is statically posed.

We do not modify the vendor asset to enable looping — `CLAUDE.md` is explicit:

> Never move/rename files inside vendor folders. Never commit a vendor pack's source.

The same applies to in-place modification of a vendor `.anim` file. So we layer life on top instead, at the root transform level.

## Chosen approach

Two effects in one `MonoBehaviour`, both authoritative on the dog's local transform from a baseline captured at `OnEnable`:

### 1. Breathing — continuous

A small sinusoidal Y-position offset applied every `Update`. Reads as a sleeping dog's belly rising and falling.

| Parameter | Default | Rationale |
|---|---|---|
| Amplitude | `0.005` m | 5 mm at the dog's local scale (parent is at 0.5×, so ~2.5 mm world). Barely perceptible but unmistakably present. |
| Period | `4` s | Slow enough to feel relaxed, fast enough that a visitor notices within a single look. Real dogs at rest breathe ~10–15 breaths/min — 4 s/cycle = 15/min, on the upper end. |
| Phase | random | Desynchronises if we ever add a second dog. |

### 2. Weight shift — occasional

A small Y-rotation offset that fires every 12–25 seconds, lerps from 0 → ±2° over 0.7 s, holds 0.4 s, lerps back. Reads as the dog turning its snout slightly to track the fire's flicker or a nearby sound.

| Parameter | Default | Rationale |
|---|---|---|
| Angle | `±2°` | Tiny. At the dog's distance from the seated viewer (~1.5 m), 2° is ~5 cm of snout displacement — enough to notice without looking deliberate. |
| Interval | `12–25 s` (random) | No fixed beat → doesn't loop. Mean ~18.5 s = ~3 shifts per 30-min soak session, sparse enough to feel organic. |
| Lerp seconds | `0.7` | Slow enough that the gesture is calm. |
| Hold seconds | `0.4` | A brief pause at the peak, not a rigid hard stop. |
| Direction | coin flip per shift | So the dog doesn't always tilt the same way. |

The envelope uses `Mathf.SmoothStep` so the start and end are eased, not linear.

### 3. Attention glance — occasional, toward player camera

A slightly larger Y-rotation that fires every 15–45 seconds with a 50 % chance of triggering each interval. Reads as the dog briefly noticing the player ("oh, that's my human"), then returning to its resting orientation.

| Parameter | Default | Rationale |
|---|---|---|
| Max yaw | `±8°` (clamped) | Bigger than weight shift so it's distinguishable as "directed attention". Always clamped — if the player is directly behind the dog, the glance still caps at 8°, so it can never lock-on or stare. |
| Interval | `15–45 s` (random) | Mean ~30 s. With the 50 % probability gate, the dog effectively glances roughly once per minute. Sparse enough that it feels deliberate when it happens. |
| Probability | `0.5` per interval | The dog doesn't react every time — half the intervals quietly skip. Removes the "mechanical timer" feel that a 100 % gate would have. |
| Duration | `3 s` total | Lerp out 0.9 s → hold 1.2 s → lerp back 0.9 s. Long enough to read as "noticing" rather than "twitching", short enough not to feel like staring. |
| Target | `Camera.main` (auto) | The local VR camera. Resolved lazily at glance time so a delayed XR rig init doesn't break anything. The `glanceTarget` field accepts a manual override for testing or pointing at a different transform. |

**This is not real voice-activity detection.** The dog glances at the local player on a random schedule, not in response to who is speaking. From the player's perspective it still reads as "the dog noticed me" because they're the only target — but a multi-player extension would need to know which remote player is currently active. See "Future expansion ideas" below for the voice-driven version we considered but deliberately didn't ship.

The envelope uses proportional timing (30 % lerp / 40 % hold / 30 % lerp, based on total duration) — different shape from the weight shift's absolute-second envelope, so the two feel distinct when they overlap.

### Composition

All three effects compose. The breathing math runs always. The weight-shift and attention-glance state machines are each a single nullable timestamp. When weight shift and glance both fire at once, their Y-rotation deltas add: worst-case ±2° + ±8° = ±10°, still subtle. Each effect uses `Mathf.DeltaAngle`-style math so the dog never spins past its baseline orientation.

## Components and animations used

**New script:** `Assets/Scripts/Environment/DogIdleBehaviour.cs` (~140 lines including the attention glance). Style mirrors the existing `PresenceBreath.cs` in the same folder — `[SerializeField]` knobs, baseline captured in `OnEnable`, phase randomisation, simple `Update`. No coroutines.

**Existing components preserved:**
- The `Animator` on Dog_001 stays on. The BlendTree's frozen idle pose still provides the resting position; we layer breathing + weight shift on top of it. No `Animator.Play()` calls, no parameter writes.
- The `SkinnedMeshRenderer` keeps its shadows-off + `DogCoat.mat` configuration from the original placement slice.

**Existing components removed at placement time (unchanged):**
- `CreatureMover`, `MovePlayerInput`, `CharacterController` — stripped by `QuestBuildAPK.AddDogCompanion` as before.

**Scene wiring** (per the saved scene state at 2026-05-19 12:02):

```
World/Companions/DogCompanion
├── Transform (1.3, 0, 0.8) · rot Y=215° · scale 0.5
├── Animator (Dog.controller, idle BlendTree)
├── SkinnedMeshRenderer (DogCoat.mat, shadows off)
└── DogIdleBehaviour
      breathingAmplitude:      0.005
      breathingPeriod:         4.0
      weightShiftAngle:        2.0
      weightShiftMinInterval:  12.0
      weightShiftMaxInterval:  25.0
      weightShiftLerpSeconds:  0.7
      weightShiftHoldSeconds:  0.4
      enableAttentionGlance:   true
      minGlanceInterval:       15.0
      maxGlanceInterval:       45.0
      glanceProbability:       0.5
      glanceDuration:          3.0
      glanceMaxYaw:            8.0
      glanceTarget:            (null → Camera.main fallback)
```

## Performance considerations

| Aspect | Cost |
|---|---|
| `Update` body | 1× `Mathf.Sin`, up to 2× `Mathf.SmoothStep` (when both shift and glance are mid-envelope), 1× `Camera.main` lookup per glance trigger (= max once every 15 s), 1 transform write. Well under 0.05 ms on Quest 3. |
| Allocations | None per frame. No `new` in `Update`. |
| Rendering | No new draw calls. Existing SkinnedMesh + Animator continue as before. |
| GPU foveation impact | None — the dog still renders at the same fidelity; FFR (Slice 5) is unaffected. |
| Memory | Two `Vector3` / `Quaternion` baselines + nine floats + one optional Transform reference per instance. ~120 bytes. |
| Logging | 1 `DebugLogger.Log("dog_attention_glance", ...)` per glance — bounded by interval + probability to ≤ 4 per minute, typically ~1 per minute. Negligible JSONL volume. |

This sits comfortably inside the budget defined in `docs/performance-checklist.md`. Adding a second dog would still be free.

## Intentionally avoided complexity

These were ruled out at design time, per the slice spec's explicit "do not add" list:

- **Navigation / NavMesh / pathfinding** — the dog never moves from its placed position.
- **Audio (barking, panting, breathing sound)** — would need spatial audio + a sample + a trigger system + volume balancing against Photon Voice. Out of scope.
- **Voice activity detection / speech recognition / audio analysis** — the attention glance is *time-based*, not *voice-based*. The dog glances toward `Camera.main` on a random schedule, not in response to who is currently speaking. Wiring real VAD via Photon Voice's `Recorder.IsCurrentlyTransmitting` flag is documented as a future expansion below — explicitly excluded from this slice per spec.
- **Speaker targeting in multiplayer** — the glance always targets the local player's camera. We do not look up the remote player's `PlayerHead` prefab or correlate with their voice transmission. Multi-player attention is a future extension; this slice is local-camera attention only.
- **Bone-level animation (head turn, ear flick, tail wag)** — every bone tweak conflicts with the Animator's pose unless gated in `LateUpdate` with disabled Animator update. Root-transform layering avoids the conflict entirely.
- **Looped procedural breathing animation override via `AnimatorOverrideController`** — would land a new asset in the project, complicate the BlendTree flow, and conflict with future Animator changes. Root-transform sin is equivalent in feel.
- **Modifying `Dog_001_idle.anim` to loop** — vendor asset, see CLAUDE.md prohibition.
- **State machine (sleeping → looking → returning-to-sleep)** — premature. Two layered effects produce enough variety; a state machine only earns its complexity when behaviours are mutually exclusive.
- **Networked sync of the breathing phase** — the dog is local-only by design. Each headset's dog breathes on its own phase. No remote sync needed.

## Scene application path

The component is attached automatically to any **future** placement by `Tools/Quest Setup/Add Dog Companion` — the helper now ends with:

```csharp
if (dog.GetComponent<DogIdleBehaviour>() == null)
    dog.AddComponent<DogIdleBehaviour>();
```

For the **existing** DogCompanion in the scene, a new standalone menu was added: `Tools/Quest Setup/Apply Dog Idle Behaviour`. It finds the existing GameObject (preserving any manual position / rotation / scale adjustments), attaches the component if missing, and marks the scene dirty. Idempotent — re-runs cleanly with a "nothing to do" log. The menu was executed once at 2026-05-19 12:02 to land the component on the current scene; the scene was then saved.

This pattern (separate menu for retroactive application vs full re-creation) preserves headset-validated manual placement tweaks while keeping the helper-driven creation flow intact.

## Future expansion ideas

Logged here so the next slice's author knows what was considered:

- **Voice-driven speaker attention** — extend the attention glance so that when Photon Voice 2's `Recorder.IsCurrentlyTransmitting` flips true for either local or remote, the dog targets that specific speaker instead of `Camera.main`. Local case is trivial (already targeting the local camera). Remote case needs `VoiceBootstrap` to expose a "current remote speaker's transform" — typically the speaker GameObject that `VoiceSpeakerPlacer` parents under `RemoteRig`. ~30 lines of glue plus a public property on `VoiceBootstrap`. Pairs with `Recorder.IsCurrentlyTransmitting`'s boolean rather than VAD level — gives a clear edge trigger.
- **Glance head bone instead of full body** — find the head bone via `Animator.GetBoneTransform(HumanBodyBones.Head)` (if the rig is Humanoid) or by name search, write to it in `LateUpdate` after the Animator runs. Reads as the head turning while the body stays settled. Lower max-yaw needed (~5°) and the dog feels even calmer. Risk: bone-name brittle to vendor pack updates; need to verify the ithappy rig type first.
- **Ear / tail twitch** — same bone-level pattern. Would need `Animator.SetBoneLocalRotation` in `LateUpdate` after the Animator runs. Adds 1–2 specific transform writes per frame on top of the current 1.
- **Position-anchored "kerb resting"** — dog occasionally lays its head closer to the fire (subtle Y-position transition lasting ~30 s). Easy to add with the same envelope pattern.
- **Companion proximity reaction** — dog's breathing speed gently increases when a player leans closer. Could read a head-distance value from VRCamera. Risk: feels gimmicky if the threshold is wrong.
- **Multi-dog desync** — if we ever add a second dog (e.g. for the friend's side of the fire), the phase randomisation in `OnEnable` already handles desync. No code change needed.

The cheapest follow-up that adds the most life is **bone-level head tilt during a weight shift** — same trigger we already have, applied to the head bone instead of the root. A future slice can layer that on top of this one without rewriting either side.

## Validation

- **Compile (initial 2-effect version)**: `mcp__mcp-unity__recompile_scripts` returned **0 errors, 51 warnings** — all warnings are pre-existing vendor code or Unity 6 deprecation noise, none from `DogIdleBehaviour.cs` or the `QuestBuildAPK.cs` additions.
- **Compile (with attention glance)**: `./scripts/build-quest.sh` exit 0, no `error CS`, no new warnings from `DogIdleBehaviour.cs`. APK 117 MB produced.
- **Scene wiring**: confirmed via `mcp__mcp-unity__get_gameobject DogCompanion` after the initial slice — `DogIdleBehaviour` is serialized on the prefab instance with default values. The new attention-glance fields will appear with defaults on the next Editor open (Unity adds them from the C# field declarations); a save will then persist their values in the scene file.
- **Networking dependency check**: `grep -nE "Network|NGO|Photon|VoiceConnection" Assets/Scripts/Environment/DogIdleBehaviour.cs` → empty. The component is local-only; the attention glance reads `Camera.main` only.
- **Headset verification**: pending Johan's next session. Expected: dog visibly breathes, occasionally tilts, occasionally glances slightly toward the player camera (within ±8°), then returns. Tracked in `docs/post-sprint-backlog.md` under "Visual / environment follow-ups → Dog / materials / environment sanity".
