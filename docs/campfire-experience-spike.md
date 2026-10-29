---
title: Campfire experience spike
description: Discovery spike — idea catalogue across fire, audio, wildlife, nature, weather, interaction, companion behaviour, and social presence, ranked by player/social/cozy value against implementation complexity and Quest cost. No implementation in this slice.
category: spike
status: draft
last_updated: 2026-08-21
sections:
  - Why this spike
  - Baseline — what's already shipped
  - How to read the tables
  - 1. Fire
  - 2. Audio
  - 3. Wildlife
  - 4. Nature
  - 5. Weather
  - 6. Interaction
  - 7. Companion behaviour
  - 8. Social presence
  - 9. Performance summary
  - Recommendation — the three experiments to build first
  - Explicitly not now
---

# Campfire experience spike

> Discovery spike. Nothing in the scene or scripts was changed. This is a catalogue and a recommendation, not a plan — see `docs/roadmap.md` for what's actually queued next.

## Why this spike

The campfire is the emotional center of CampfireVR: two people, seated, talking, with a fire between them. The risk with a spike like this is generating fifty feature ideas and forgetting that the vision is explicitly about *restraint* — "occasionally discover small delightful details, but never feel overwhelmed" (see `docs/vision.md`). Every idea below is judged against that bar, not just against feasibility.

The scene already has more built than the request's framing assumes. A good chunk of this catalogue turns out to already be shipped, partially shipped, or one wiring pass away from shipped — because three prior polish slices (dog idle behaviour, tree wind, VFX fire) already explored this exact design space in miniature. That prior work is the strongest signal available for what "cozy" actually looks like in this project, so the ranking below leans on it heavily rather than re-deriving taste from scratch.

## Baseline — what's already shipped

Worth stating plainly before cataloguing new ideas, because several "ideas" in the brief turn out to be baseline already:

| Area | Already in the scene |
|---|---|
| Fire | `VFX_Fire` particle flame (WALLCOEUR pack, 20 particles, BiRP-compatible) + `Embers` (24 particles, additive drift) + `FireLight` with Perlin-noise intensity/color flicker (`FireLightFlicker.cs`) + looping crackle (`FireCrackleAudio`). Smoke deliberately excluded (see `vfx-fire-package-evaluation.md`, `piloto-campfire-evaluation.md`). |
| Wildlife | One dog (`DogCompanion`, ithappy `Dog_001`) with procedural breathing, occasional weight-shift, and an occasional **time-based** attention glance toward the local camera (`DogIdleBehaviour.cs`). |
| Nature | 43 pines + one oak + sparse wildflower clusters + 6 grass tufts. 5 of the closest trees already have subtle wind sway (`SubtleTreeWind.cs`); a menu item (`Apply Tree Wind (ALL trees)`) extends this to all 43 with zero new code. |
| Weather | Night is static and authored, not simulated: flat dark ambient (`NightAtmosphere.cs`) + a real starfield skybox (`Real Stars Skybox`, already wired via `RenderSettings.skybox` in the current scene — `roadmap.md`'s "skybox cleared" note is stale, see `docs/app-alignment-qa.md`-style drift). No moon, no fog, no dynamic time of day. |
| Interaction | None. Explicitly deferred in `roadmap.md` ("a stick to poke the fire, a stone you can pick up. Nothing more."). No grab/hold system exists at all — XRI's runtime is intentionally not used (see CLAUDE.md "Custom XR" section) — so any interaction idea inherits a bigger prerequisite than it looks like it should. |
| Companion behaviour | Time-based only. `VoiceBootstrap.IsTransmitting` (a public bool, already implemented) is not wired to anything visual yet. |
| Social presence | None. The fire doesn't know two people are talking. |

Quest performance headroom to work with: the last recorded soak test (`performance-checklist.md`) shows **6.2 ms median / 8.1 ms p95 App GPU time** against an **11.1 ms budget** at 90 Hz — roughly 3 ms of comfortable headroom, more at the median. Every prior polish addition (dog idle, tree wind, VFX fire swap) has landed at sub-millisecond or sub-microsecond cost, so the practical ceiling here is "many more of these," not "room for one more."

## How to read the tables

Each idea gets:

- **Value** — Player / Social / Cozy, each rated Low/Med/High (rolled into one column to keep tables scannable; called out separately only where they diverge sharply)
- **Complexity** — Trivial (wiring existing pieces) / Low (small new script, no new asset) / Medium (new asset or new subsystem) / High (new subsystem + asset + tuning risk)
- **Quest cost** — rough CPU/GPU/memory/draw-call/network impact, calibrated against the sub-ms costs already measured for similar existing effects
- **Class** — NOW / NEXT / LATER / MAYBE NEVER

**NOW** = could be scoped as the very next slice with today's assets and today's code. **NEXT** = clearly worth doing soon, but needs a small new asset or a small new subsystem first. **LATER** = valuable but depends on something not yet built (a grab system, a weather system, a second companion asset). **MAYBE NEVER** = either fights the vision's restraint principle, contradicts an existing explicit decision, or the cost/value ratio doesn't clear the bar without a larger context change.

---

## 1. Fire

| Idea | Value | Complexity | Quest cost | Class |
|---|---|---|---|---|
| Fire brightens/quiets with conversation | Social High, Cozy High | Trivial — wires `VoiceBootstrap.IsTransmitting` into `FireLightFlicker`'s existing intensity knob | ~0 (one bool read + one lerp/frame) | **NOW** *(cross-listed, see §8)* |
| Occasional distinct spark pop (brighter, rarer than embers) | Cozy Med | Low — second small particle system, same proven shader (`Mobile/Particles/Additive`) as `Embers` | <0.5 ms, matches `Embers`/`VFX_Fire` measured cost | **NEXT** |
| Flame lean with ambient "wind" (shared param with tree sway) | Cozy Med, ties fire to forest | Low — reuse `SubtleTreeWind`'s sin-based rotation pattern on the `VFX_Fire` transform | Negligible (same math already measured at ~1 µs/instance) | **NEXT** |
| Logs slowly burning down over a session (material darkens / log mesh shrinks) | Player Med, Cozy Med | Medium — needs a session-duration timer driving a material/scale lerp; no existing pattern to reuse | Negligible GPU; needs a `[SerializeField]` timer script | **LATER** |
| Dynamic GI — flicker affects bounced light on nearby objects | Cozy Low-Med | High — Built-in RP baked lighting doesn't cheaply support runtime-reactive GI; would need light probes reweighted at runtime or a fake local-light hack | Unclear, likely more than trivial GPU cost | **MAYBE NEVER** — marginal payoff, real risk |
| Smoke | — | — | Alpha-blend overdraw; explicitly evaluated and excluded twice already | **MAYBE NEVER** — decided against in `vfx-fire-package-evaluation.md` and `piloto-campfire-evaluation.md`; revisit only if a very sparse, very cheap wisp effect is found |
| Composite fire (ground-fire ring / torch-style, from the already-imported VFX pack) | Player Low | Trivial to wire, but wrong tone | n/a | **MAYBE NEVER** — reads as cinematic/gamey, fights "quiet campfire" vision; pack docs already flagged these as overkill |

## 2. Audio

Current state: one looping crackle clip, nothing else. This is the area furthest from "done" relative to the brief — everything below needs at least one new audio asset (CC0/licensed, evaluated the same way `asset-inventory.md` evaluated visual packs).

| Idea | Value | Complexity | Quest cost | Class |
|---|---|---|---|---|
| Ambience reacts to player count (near-silent solo, gentle life at 2p) | Social Med | Low — read NGO connected-client count, crossfade an ambience bed volume | Negligible | **NEXT** |
| Sparse night ambience bed (crickets/insects loop, low volume) | Cozy High | Low-Med — needs one new CC0/licensed loop; mixing against voice + crackle needs care | 1 extra `AudioSource`, negligible CPU | **NEXT** |
| Occasional owl/distant-bird one-shot (sparse, random interval — reuse `DogIdleBehaviour`'s interval-random pattern) | Cozy High | Low-Med — needs 1-2 new clips + a ~40-line script structurally identical to the dog's weight-shift timer | Negligible | **NEXT** |
| Distant river | Cozy Low-Med | Medium — needs an asset + a placement/plausibility check (is there a river in this clearing's fiction?) | Negligible | **LATER** |
| Rain / thunder (audio only, no visual) | Cozy Med (mood-dependent) | Low — one loop + one one-shot, gated by a manual "weather mood" toggle rather than a live system | Negligible | **LATER** — pair with Weather §5, don't build audio for a visual that doesn't exist yet |
| Audio reacts to distance from fire | — | — | n/a | **MAYBE NEVER** — locomotion is an explicit non-goal (CLAUDE.md); players are always the same distance from the fire, so this axis doesn't exist in this app |
| Audio reacts to weather / time of day | Cozy Med | Depends entirely on Weather §5 existing first | n/a | **LATER**, gated on §5 |

## 3. Wildlife

| Idea | Value | Complexity | Quest cost | Class |
|---|---|---|---|---|
| Dog's attention glance triggers on **local** mic going live (swap the existing random timer for an edge-trigger on `VoiceBootstrap.IsTransmitting`, which is already public) | Social High, Cozy High | Trivial-Low — the target (`Camera.main`) is already what the dog looks at; only the trigger source changes | ~0 — one bool poll per frame, already-existing glance envelope | **NOW** *(cross-listed, see §7)* |
| Fireflies (small drifting additive particle cluster at the clearing edge, no lights) | Cozy High | Low — third instance of the same particle pattern already proven twice (`Embers`, `VFX_Fire`), no new asset needed | <0.5 ms, matches measured `Embers` cost | **NOW** |
| Second companion — reuse `Deer` from the already-imported `ithappy` pack, extract `DogIdleBehaviour` into a shared `CompanionIdleBehaviour` first | Cozy Med-High | Medium — needs the refactor (small, mechanical) + a placement pass identical in shape to `dog-companion-slice.md` | Same order as the dog: 1 skinned mesh + 1 Animator tick, "rounding error" per that slice's own measurement | **NEXT** |
| Fox / owl / rabbit / hedgehog / squirrel as a companion | Cozy Med-High | Medium-High — **no such asset exists in the project or the cached Asset Store library today**; needs sourcing + a BiRP-shader compatibility check (the exact process `piloto-campfire-evaluation.md` and `vfx-fire-package-evaluation.md` already ran for other packs, which is not free — one of those two evaluations came back "unusable") | Unknown until an asset is chosen | **LATER** — real idea, but gated on an asset search that might dead-end like Piloto did |
| Dog reacts to fire flare (small ear-perk/flinch when `FireLightFlicker`'s intensity spikes) | Cozy Med | Low — needs one small public accessor on `FireLightFlicker`, then reuses the existing weight-shift envelope | ~0 | **NEXT** |

## 4. Nature

| Idea | Value | Complexity | Quest cost | Class |
|---|---|---|---|---|
| Extend `SubtleTreeWind` to all 43 trees (menu item already exists: `Apply Tree Wind (ALL trees)`) | Cozy Med | **Trivial — literally already built**, just needs invoking + a headset pass | ~40 µs total, already measured | **NOW** — closer to "flip a switch" than "build a feature" |
| Grass sway (same sin-rotation pattern applied to the 6 `GrassBreakup` cards) | Cozy Low-Med | Low — reuse `SubtleTreeWind` pattern verbatim on a smaller object | Negligible (6 objects) | **NEXT** |
| Ground fog / mist (Built-in RP `RenderSettings.fog`, distance-based) | Cozy High | Trivial-Low — Unity's built-in linear/exponential fog needs no new asset, one settings pass + tuning | Effectively free on Quest — it's a per-pixel shader term already in the standard pipeline | **NOW** |
| Drifting pollen / dust motes catching firelight | Cozy High | Low — small additive particle system near the fire, same proven shader family | <0.5 ms | **NEXT** |
| Falling leaves (sparse, slow drift) | Cozy Med-High | Low-Med — needs a leaf sprite (available in the already-imported `NatureStarterKit2`) + a small particle system | Low, comparable to `Embers` | **NEXT** |
| Butterflies | Cozy Med | Medium — no butterfly asset currently in the project; needs sourcing + simple flight-path scripting; daytime-coded content in a night-only scene | Low once built | **LATER** |
| Mushrooms (static decorative mesh) | Player Low | Medium — needs sourcing, one-time discovery value only | Negligible | **LATER / MAYBE NEVER** — low ongoing payoff for a "look at it once" prop |
| Snow | Cozy Low here | Medium | Low | **MAYBE NEVER** — the existing `oak-and-flowers-slice.md` deliberately targeted "calm Scandinavian **summer**"; snow contradicts the authored season unless a seasonal-toggle direction is explicitly chosen (see Weather §5) |

## 5. Weather

CampfireVR's night is **authored, not simulated** — a fixed, tuned mood rather than a day/night engine. That's a real prior decision (`NightAtmosphere.cs` is a one-shot `Apply()`, not a cycle), and it matches the vision's restraint principle. The ideas below mostly extend the authored-mood approach rather than proposing a simulation.

| Idea | Value | Complexity | Quest cost | Class |
|---|---|---|---|---|
| Moonlight (a moon disc + a light-color pass to complement the already-shipped starfield) | Cozy High | Low-Med — a sprite/quad in the sky + retuning the existing `Directional Light` | Negligible | **NEXT** |
| Ground fog / mist | *(cross-listed, see §4)* | Trivial-Low | Effectively free | **NOW** |
| One-time slow sunset→night intro (not a cycle — a single authored transition on session start) | Cozy Med | Medium — needs a short scripted lerp across ambient color, directional light, skybox blend; scoped tightly it avoids full day/night complexity | Negligible once built | **LATER** — nice but not free; a fixed night is already working |
| Light rain (audio only) | *(cross-listed, see §2)* | Low | Negligible | **LATER** |
| Light rain (visual — droplet particles / ripple-on-ground) | Cozy Med (risk: reads as "bad weather," not cozy, if overdone) | Medium | Low-Med, alpha-blended particles cost more than additive | **LATER** |
| Snow | *(cross-listed, see §4)* | Medium | Low | **MAYBE NEVER** — contradicts current authored season |
| Full weather **system** (state machine cycling conditions) | — | High | Unclear, and adds cross-network sync complexity (two players must see the same weather) | **MAYBE NEVER** — this is the one idea in the whole catalogue that risks becoming a "game system," which `roadmap.md`'s explicit non-goals warn against. An authored mood beats a simulated one for this app. |

## 6. Interaction

**Load-bearing note:** every idea in this section shares one prerequisite that doesn't exist yet — a minimal grab/hold interaction. The project deliberately does not run XRI's runtime (see CLAUDE.md, "Custom XR (no XRI/Input System runtime)"), so "add interaction" isn't a per-idea cost, it's one new subsystem that every idea below then rides on top of. That reframes the real first decision: not *which* interaction, but *whether* to build a minimal custom grab system at all right now.

| Idea | Value | Complexity | Quest cost | Class |
|---|---|---|---|---|
| Poke the fire with a stick (grabbable prop, tip glows near flame, maybe triggers a spark burst) | Player High, Cozy High | Medium — rides on the grab-system prerequisite above; the poke logic itself is small once grab exists | Low once built | **LATER** |
| Add another log (squeeze near the woodpile → fire brightens briefly) | Player Med, Cozy Med | Medium — same grab prerequisite, simpler payload (a state flag, not physics) | Low | **LATER** |
| Throw pinecones | Player Med | High — grab **and** throw (velocity transfer from controller tracking), the most novel physics subsystem on this list | Low-Med (Rigidbody + collision) | **LATER** |
| Roast marshmallows | Player High, Cozy High | High — stacks grab + proximity-to-fire heat detection + material color-lerp + a "done" state; the most systems-heavy idea in the whole catalogue | Low once built, but the most tuning risk (heat curve, timing) | **MAYBE NEVER** *(not this cycle)* — genuinely delightful, but do it only after a grab system exists and has shipped once on something simpler |
| Move sticks / place mugs (idle rearrangement) | Player Low | Medium (grab prerequisite) | Low | **LATER** |
| Toast together (two players clink mugs) | Social High, Cozy High | High — grab + two-player proximity/gesture detection + a networked "clink" event | Low compute, real network-sync design work | **MAYBE NEVER** *(not this cycle)* — best possible payoff in the whole interaction list, but it's the most systems on top of the least-proven subsystem |

## 7. Companion behaviour

| Idea | Value | Complexity | Quest cost | Class |
|---|---|---|---|---|
| Voice-driven local attention (swap dog's random-timer glance for an `IsTransmitting` edge trigger) | Social High, Cozy High | Trivial-Low — the API is already public; the glance envelope already exists | ~0 | **NOW** |
| Voice-driven **remote**-speaker attention (dog looks at whichever player is talking, not just local camera) | Social High, Cozy High | Medium — needs `VoiceBootstrap` to expose the remote speaker's transform (`VoiceSpeakerPlacer` already parents it under `RemoteRig`, so the pointer exists, just isn't surfaced) | ~0 | **NEXT** — natural follow-up once the local half ships |
| Bone-level head-turn instead of full-body glance | Cozy Med (calmer read) | Low-Med — already scoped in `dog-idle-behavior-slice.md` as "cheapest follow-up that adds the most life"; risk is rig-name brittleness across vendor pack updates | ~0 | **NEXT** |
| Sleeping → stretching → sniffing state variety | Cozy Med | Low-Med — extends the existing envelope-timer pattern | ~0 | **NEXT** — but budget carefully; adding states risks "busier," which the original slice explicitly guarded against |
| Reacting to fire flare | *(cross-listed, see §3)* | Low | ~0 | **NEXT** |
| Shared `CompanionIdleBehaviour` for multiple companions | *(cross-listed, see §3)* | Medium (refactor) | ~0 | **NEXT** |

## 8. Social presence

The two ingredients this section needs — a live "is someone talking" signal and a controllable fire-brightness knob — **already both exist in code**, just not wired to each other. This is the highest value-per-line idea in the entire spike.

| Idea | Value | Complexity | Quest cost | Class |
|---|---|---|---|---|
| Fire brighter when people talk, quieter in silence (local-only reaction: each headset reacts to its own mic) | Social High, Cozy High | Trivial — ~30-line script reading `VoiceBootstrap.IsTransmitting`, lerping `FireLightFlicker.baseIntensity` | ~0 (one bool read + one lerp per frame) | **NOW** |
| Same effect, but **synced** so both players see the identical fire state (not just their own mic) | Social High, Cozy High | Low-Med — needs one `NetworkVariable<bool>` (or reuse the existing hand-pose sync pattern) to broadcast "someone is transmitting" | Small, well inside NGO's already-proven sync budget | **NEXT** — natural v2 once the local-only version is validated in headset |
| Gentle warmth post-processing pulse (bloom/vignette tied to conversation) | Cozy Med | Medium — the project has no post-processing Volume stack today in the active BiRP path; this is a small new subsystem, not just a wiring pass | Depends on effect chosen; bloom is typically cheap, but it's a new cost category | **LATER** — do only if the light-only version proves the concept needs more punch |
| Crackle volume swells with conversation (same trigger, second output) | Cozy Med | Trivial — same trigger as brightness, one more `AudioSource.volume` lerp | ~0 | **NOW** — bundle into the same slice as fire brightness rather than shipping separately |

## 9. Performance summary

Rolling up every **NOW** and **NEXT** item above against the measured 3 ms of GPU headroom (`performance-checklist.md`):

| Category | Typical per-item cost | Running total if every NOW+NEXT item ships |
|---|---|---|
| New/extra particle systems (sparks, fireflies, pollen, leaves) | <0.5 ms each, additive shader, ≤24 particles (matches `Embers`/`VFX_Fire` measured cost) | ~2 ms for all four combined — still comfortably inside budget |
| New small `MonoBehaviour` timers (owl calls, weight-shift-style states, fire-reactive scripts) | Sub-microsecond each (matches `DogIdleBehaviour`, `SubtleTreeWind` measurements) | Rounding error even at a dozen instances |
| New `AudioSource`s (ambience bed, owl one-shots) | CPU-negligible; real cost is mixing/balancing against Photon Voice, not hardware budget | No GPU/CPU concern; a design/tuning concern instead |
| Built-in fog (`RenderSettings.fog`) | Effectively free — a shader term already compiled into the standard pipeline | Free |
| Second companion (Deer) | ~Same as the dog: 1 skinned mesh, 1 Animator tick, "rounding error" per `dog-companion-slice.md`'s own measurement | Negligible |
| `NetworkVariable<bool>` for synced fire-reacts-to-voice | Tiny — same class of sync NGO already does for hand poses | Negligible |
| Grab/hold interaction subsystem (prerequisite for all of §6) | **Unmeasured** — this is the one category with a real unknown, because nothing like it exists in the project yet | Needs its own spike before committing, not estimated here |

**Bottom line:** almost everything in this catalogue is Quest-cheap by a wide margin — the existing headroom absorbs the entire NOW+NEXT list several times over. The one real performance unknown is the interaction/grab subsystem (§6), because it's the one category where nothing analogous has been built and measured yet. Everything else risks *design* budget (does it feel calm, not "screensaver") long before it risks *frame* budget.

## Recommendation — the three experiments to build first

Ranked by value-per-line-of-code, using what's already proven to work in this exact codebase:

### 1. Fire responds to conversation (§8)

The cheapest idea in the whole catalogue and the most on-vision — "the campfire... becomes the natural focal point for conversation" is close to a literal spec for this feature. Both halves already exist in code and have never been connected.

**Smallest vertical slice:** one new script (`FireSocialPresence.cs` or similar), local-only reaction first (no networking): poll `VoiceBootstrap.IsTransmitting` on an interval, lerp `FireLightFlicker`'s `baseIntensity` up ~10–15% while true, decay back over ~2 s when it goes false. Bundle the crackle-volume swell into the same script since it's the same trigger. No new assets, no scene changes beyond one component add, no networking changes. Headset test: talk, watch the fire lift; go quiet, watch it settle.

### 2. Voice-driven dog attention, local half only (§7)

Reuses ~90% of an already-shipped, already-tuned system. `dog-companion-slice.md`'s idle-behaviour slice already documented this exact extension as a "future expansion" and estimated it at "~30 lines of glue" — the estimate is already sitting in the repo.

**Smallest vertical slice:** in `DogIdleBehaviour.cs`, replace (or gate) the glance timer's random trigger with an edge-detect on `VoiceBootstrap.IsTransmitting` going true, for the local player only. Keep every existing envelope, angle, and duration constant unchanged. Defer the remote-speaker half (needs a new `VoiceBootstrap` accessor) to a follow-up slice.

### 3. Fireflies (§3/§4)

The one genuinely new piece of content in the top three — the other two are wiring passes. It's still cheap because it's the third instance of a particle pattern this project has now proven twice (`Embers`, `VFX_Fire`): additive shader, small particle count, no light components, no new textures required. It also directly answers the brief's "occasionally discover small delightful details" line in a way none of the wiring ideas can.

**Smallest vertical slice:** one small additive `ParticleSystem` (8–12 max particles) placed at the clearing's edge — far enough from the seats to read as "the forest has fireflies too," not "particles near my face." Slow upward drift, soft flicker via the same Perlin-noise pattern `FireLightFlicker` already uses. No new script needed if particle-system curves alone produce a convincing drift/flicker; a tiny script only if curves prove insufficient in headset.

**Why not the other strong candidates:** extending tree wind to all 43 trees is *already built* (one menu click) — it's a quick win worth doing, just not really an "experiment." Ground fog is nearly as cheap as these three and is a strong 4th pick if the first three land well. The interaction system (§6) is the highest-ceiling idea in the catalogue but is gated behind an unbuilt, unmeasured subsystem — the right next step there is a dedicated spike on the grab/hold mechanic itself, not a feature built on top of a guess.

## Explicitly not now

Carried over from the classifications above, stated once so it's easy to point back to:

- **Smoke** — decided against twice already (`vfx-fire-package-evaluation.md`, `piloto-campfire-evaluation.md`).
- **Snow** — contradicts the currently-authored "calm Scandinavian summer" season (`oak-and-flowers-slice.md`).
- **A simulated weather/day-night system** — reads as a game system, which `roadmap.md`'s non-goals explicitly warn against; an authored mood is the right shape for this app.
- **Marshmallow roasting, mug-toasting** — the two most delightful interaction ideas, deliberately deferred until a basic grab/hold subsystem exists and has shipped once on something simpler (poke-the-fire or add-a-log).
- **Locomotion-dependent ideas** (audio/behavior reacting to distance from the fire) — locomotion itself is an explicit non-goal; players are always the same distance from the fire.
