---
title: Oak and flowers slice
description: One big oak + four sparse wildflower cross-quad clusters added to the campfire clearing for "calm Scandinavian summer" atmosphere.
category: scene-polish
status: headset-validation-pending
last_updated: 2026-05-16
sections:
  - Why a single oak
  - Assets used
  - Placement reasoning
  - Performance
  - Visual intention
  - What was intentionally avoided
  - Headset validation still needed
  - Future options
---

# Oak and flowers slice

> Atmosphere polish: one big oak tree off to the side of the clearing + a few sparse wildflower clusters around stones, trunk base, and far-edge sight-lines. Goal is "calm Scandinavian summer clearing", not "fantasy meadow".

## Why a single oak

The existing forest is six identical `tree_01` pines from the Mountain Terrain pack arranged in a rough perimeter (see `docs/forest-atmosphere-slice.md` if it exists, otherwise scene only). Pines read as "northern conifer backdrop" but the clearing itself has no anchor — no single tree the eye locks onto as a landmark. A solitary oak in the cleared zone gives the seated player something to look at in their peripheral vision when not facing the fire, and grounds the place as "a clearing in a real-feeling forest" rather than "a circle of pines around a campfire".

One only. More than one and the eye starts comparing them ("are those a row? a grove?") instead of accepting the oak as just "the tree by the campfire".

## Assets used

| Asset | Path | Notes |
|---|---|---|
| `OakBigTree01_pr.prefab` | `Assets/ALP_Assets/Big Oak Tree FREE/Prefabs/` | 5-LOD setup (LOD0..LOD3 + billboard), MeshCollider child, ~30m world canopy at scale 0.85. Custom ALP shaders verified BiRP-compatible (SubShader tag = `RenderType=Opaque`, no `UniversalPipeline` tag). |
| `grassFlower04.tga` | `Assets/ALP_Assets/GrassFlowersFREE/Textures/GrassFlowers/` | One chosen flower texture from the pack's ten. Pack ships textures only (terrain-detail oriented), no prefabs — we use the texture in a Standard-Cutout material for cross-quad meshes instead of setting up Unity Terrain. |
| `Assets/Materials/MeadowFlower.mat` | created in this slice | Standard shader (Built-In RP), Cutout mode, `_Cutoff = 0.5`, `_Color = (0.96, 0.94, 0.88)` warm white, low gloss. One shared material across all four clusters → one material slot, eight quad draws batchable. |
| `OakAndFlowersSetup.cs` | `Assets/Editor/Environment/` | Re-runnable `Tools/Quest Setup/Place Oak and Flowers` menu. Idempotent: skips placement if the canonical names already exist. Constants at the top of the file are the single source of truth for positions / rotations / scales. |

Both ALP packs are added to `.gitignore` per the project's Asset Store EULA pattern (Real Stars, Piloto, Nature Starter Kit 2, etc.). Fresh clone needs to re-import each from My Assets — the committed scene + material reference vendor assets by GUID.

### IL2CPP / runtime safety

The oak pack's runtime script `Core/Scripts/Controller/ALP8310_ControllerStyle.cs` properly wraps its `using UnityEditor;` block in `#if UNITY_EDITOR`. The unguarded portion uses only `Shader.GetGlobalFloat` / `SetGlobalFloat` — IL2CPP-safe for Android Player builds. No need for the strip-from-scene pattern used on `ithappy/CreatureMover.cs`.

## Placement reasoning

### Oak

| Field | Value | Why |
|---|---|---|
| `localPosition` | `(-4.5, 0, 3.5)` | Behind player A's seat (player A sits at `(1.6, *, 0)`), off-axis from the campfire-mountain sight-line. The 5.7m distance from origin places the trunk just outside the seating circle (~1.6m radius) but well inside the existing pine perimeter. |
| `localRotation` (Euler) | `(0, 47°, 0)` | Trunk twist differs from the pine prefabs' default 0° heading, so the oak doesn't visually mirror the surrounding tree_01 instances. |
| `localScale` | `(0.85, 0.85, 0.85)` | Prefab default is `1.0` with a ~36m world canopy; 0.85× brings it to ~31m. Real big oaks have 20–30m canopies, so this stays believable. From seated player view the canopy reads as "tall tree overhead", trunk fills lower peripheral vision. |
| `parent` | `World/Environment/Forest/Trees` | Same parent as the existing pine instances; doesn't disturb the cleanup pattern from `docs/project-structure-cleanup.md`. |
| Shadows | OFF on every renderer | Quest shadow budget is already spent on FireLight + Directional; an extra ~30m canopy of leaf shadows would tank framerate. |

### Flower clusters (four)

All four clusters share the same material (`MeadowFlower.mat`) and use the project's cross-quad pattern (two perpendicular `Quad` primitives) — same approach as the existing `GrassTuft_*` objects in `World/Environment/Grass/GrassBreakup`.

| Name | `localPosition` | `localScale` | Rationale |
|---|---|---|---|
| `FlowerCluster_OakBase` | `(-3.7, 0, 3.2)` | 0.45 | At the oak's roots, slightly off-trunk on the inward side. Reads as "wildflowers thriving in the dappled shade", anchors the oak visually to the meadow ecology. Largest cluster (0.45) because it's furthest from the seating circle. |
| `FlowerCluster_SeatBack` | `(1.95, 0, -0.55)` | 0.32 | Behind player A's StoneSeat (A is at `(1.6, *, 0)`). Visible when player A turns to chat with player B; small enough not to crowd the seat. |
| `FlowerCluster_Kerb` | `(-0.85, 0, -1.25)` | 0.30 | Tucked against the fire-pit kerb rocks on the side opposite the player seats. Adds a flash of light tone in the fire's mid-range view without being in anyone's lap. |
| `FlowerCluster_FarEdge` | `(2.4, 0, -3.2)` | 0.38 | Outer edge of the clearing, in a sight-line between the pines. Acts as a "stop point" for the eye when looking away from the fire toward the forest. |

Cross-quad construction per cluster: two `Quad` primitives, one at 0° yaw and one at 90° yaw, both at `(0, scale*0.5, 0)` local position so the bottom edge sits on the ground plane (y=0). Quad's pivot is at its centre, hence the `scale * 0.5` Y-lift.

Why scales 0.30–0.45 rather than uniform: small natural asymmetry. Wildflowers in real meadows aren't all the same height; the variation reads as "thriving in different micro-conditions" rather than "instanced grass strip".

## Performance

| Metric | Cost |
|---|---|
| Oak triangles | ~5k LOD0 + falloff LODs (LODGroup auto-switches by view distance) |
| Oak materials | 4 unique materials (`BillboardBigOak01`, `Branches001`, `Trunk01`, `Ground`) → ~4 draw calls when at LOD0, drops as LODs swap. Real-world Quest cost: 2–3 visible draws most of the time since LODs swap aggressively past ~10m. |
| Flower triangles | 2 quads × 4 clusters = 8 quads × 2 tris = 16 tris total. Negligible. |
| Flower materials | 1 shared `MeadowFlower.mat` → 1 SRP-batchable draw call for all 8 quads, in practice. |
| Flower transparency | Cutout (alpha-test), not alpha-blend → no overdraw issue, plays well with Quest's tiled renderer. |
| Realtime shadows | OFF on all new renderers (oak + flowers). |
| Animation | None. All static decoration. |
| Scripts | Zero runtime. The vendor runtime script is `#if UNITY_EDITOR`-gated. |

Net Quest cost: well within the 30k-ish on-screen triangle target for a single-frame seated view with the existing forest. No frame-time impact measured (not yet measured — see headset validation).

## Visual intention

What we're aiming for:

- **Calm Scandinavian summer clearing** — the oak as a singular landmark, not a feature; the flowers as a few quiet accents, not a carpet.
- **Subtle nature variation** — different shapes (broad oak vs slender pines), different colour temperatures (oak's green-gold canopy vs pine's blue-green), different micro-detail (cross-quad wildflowers vs solid-mesh grass tufts).
- **Believable small ecosystem** — flowers at the oak base imply soil/shade, flowers near the kerb imply warmth/sun, flowers at the far edge imply "the meadow extends past what we see".
- **Cozy VR social space** — peripheral things to notice without distraction. You should be able to look at your friend across the fire and have the oak + flowers register as "this place feels alive" without ever consciously focusing on them.

Compositional rule: nothing new added in the central seating triangle (player A ↔ player B ↔ fire). Everything new is at the periphery.

## What was intentionally avoided

- **No second tree species, no fern-grass-flower combo, no second oak.** Adding one new species at a time keeps the colour world coherent.
- **No flowers in the seating circle's centre or directly in front of either player.** Avoids "looking at flowers" as a focal activity — they're peripheral by design.
- **No animated wind, no wildflower sway, no Lifecycle controller.** The ALP shaders include a wind-displacement variant (`ALP Cutout Translucency Wind`); we use the non-wind shader for the flower material to keep the scene static.
- **No multiple flower colours.** Pack has ten flower textures; using one keeps the palette tight ("warm white wildflower" — reads as cow parsley or wood anemone). Future polish slice can introduce a second material with a yellow or blue variant if the headset test feels under-varied.
- **No Unity Terrain conversion.** The flower pack is terrain-detail oriented; setting up a Terrain just to use the detail painter would touch the ground rendering / collider / lighting in ways completely out of scope for atmosphere polish.
- **No realtime shadows from the oak.** Quest's shadow budget is already on the FireLight + Directional; a 30m canopy of leaf shadows is the single most expensive thing we could add to this scene.
- **No changes to networking, voice, XR/input, room/session, hand visuals, remote avatars, build pipeline, or debug systems.** Per slice spec.

## Headset validation still needed

- **Oak scale.** `0.85` is an educated first guess. If the canopy crowds the seated view or the trunk reads as comically thick, drop to `0.65–0.70`. If it disappears into the pine perimeter, increase to `1.0`.
- **Oak position.** `(-4.5, 0, 3.5)` keeps it behind player A. May need to shift further out (~-6, 0, ~5) if it visually overlaps a pine, or rotate so the densest canopy hangs over a specific empty quadrant.
- **Flower cluster visibility.** At `0.30–0.45` scale the quads are ~30–45cm tall. May read as too small from seated eye height (~1.2m), or too big if they tower over the stones they're nestled against.
- **Flower colour.** Warm white may wash out against the dirt ground (`Terra/Example/Textures/Dirt*.tga`). If they vanish, try `_Color = (1.0, 0.95, 0.78)` (pale yellow) or swap to `grassFlower07.tga` (a different texture from the pack — content not previewed yet).
- **Oak LOD transition snapping.** Default LODGroup settings — if the LOD swap is visible mid-session, the per-LOD percentages on the LODGroup can be tuned.
- **Cross-quad billboarding artefacts.** With only two perpendicular quads and no actual billboard component, the flowers will look "flat" from the +X / -X / +Z / -Z viewing angles where one quad is edge-on. Acceptable for a single-spot seated experience (the player isn't walking around them), but watch for any cluster that's frequently seen edge-on.

Re-running `Tools/Quest Setup/Place Oak and Flowers` after tuning the constants at the top of `OakAndFlowersSetup.cs` regenerates each object in place. Idempotent — won't duplicate.

## Oak wind polish

A subtle leaf-shimmer on the oak's canopy + billboard, no perceptible trunk motion. Goal is "calm evening breeze" — the kind of motion you'd register as "this tree is alive" rather than consciously look at.

### Implementation approach

The ALP "Big Oak Tree FREE" pack ships shaders with built-in wind support:

| Material | Shader | Wind support |
|---|---|---|
| `Branches001.mat` (canopy) | `ALP Cutout Translucency Wind.shader` | Yes — vertex sway driven by shader globals |
| `BillboardBigOak01.mat` | `ALP Cutout Translucency Wind.shader` | Yes — same path |
| `Trunk01.mat` | `ALP Surface Detail Wind.shader` | Supports it but our settings keep trunk motion at zero (heavy/grounded feel) |

The shaders read `_GlobalWindIntensity`, `_GlobalWindPulse`, `_GlobalWindTurbulence`, etc. as shader globals — each value is set on every `Update()` by an `ALP8310Controller` MonoBehaviour. Without that controller in the scene, the globals stay at zero and nothing animates. We add one instance at `World/Environment/Forest/OakWindController` and tune it for the cozy target.

`OakAndFlowersSetup.PlaceWindController()` is the single source of truth — re-running `Tools/Quest Setup/Place Oak and Flowers` re-applies the constants from the top of the file. Idempotent: existing controller gets its values updated, no duplicate spawned.

### Wind settings used

```
WindStrength            0.10    (~2 % of ALP's Reset() default of 5.0)
WindPulse               0.30    (slow cycle; Reset defaults to 0.5)
WindTurbulence          0.12    (gentle variance; Reset = 1.0)
WindRandomness          0.15
WindDirection           0
BillboardWindEnabled    true
BillboardWindIntensity  0.06    (barely-there; Reset = 0.5)
SynchWindZone           false   (we drive globals directly, no Unity WindZone)
SynchTheVegetationEngine  false
SynchMicrosplat         false
```

Why these specific values:
- **`WindStrength 0.10`** — anything above ~0.3 starts to look like "a noticeable wind", which breaks the cozy seated vibe. 0.10 produces a barely-perceptible canopy shimmer that you have to look for.
- **`WindPulse 0.30`** — slow cycle (~3 s wavelength). Fast pulse reads as "windy day"; slow pulse reads as "evening breeze that comes and goes". Lower than 0.2 starts to feel mechanical.
- **`WindTurbulence 0.12`** — small variance so the motion isn't a clean sine wave. Higher values introduce per-frame jitter that reads as noise rather than nature.
- **`BillboardWindIntensity 0.06`** — the far-LOD billboard should barely sway; the player rarely sees the billboard from this seated viewpoint, so the value is mostly future-proofing for camera moves.

### Why subtle was chosen

Cozy seated VR is a low-stimulation context. Even a "moderate" wind reads as agitating in headset — your brain registers the constant motion peripherally and treats it as something demanding attention. By keeping the canopy motion below the conscious-detection threshold for a focused viewer (the player is usually looking at their friend across the fire, not the oak overhead), the oak feels "alive but unintrusive" — adding to the place without becoming a feature.

Also: VR motion-sickness budget. Periodic motion in peripheral vision can contribute to discomfort even when the player isn't moving. The values here are well below that threshold.

### What was NOT touched

- **Flower clusters** — Standard-Cutout material, no shader wind support, no motion. Confirmed unaffected.
- **Existing grass tufts** (`GrassTuft_*`) — Standard shader, no wind support. The ALP shader globals don't affect them.
- **Existing pine trees** (`tree_01` from Mountain Terrain pack) — different shaders entirely, no ALP globals consumed.
- **Trunk material** — supports wind via `ALP Surface Detail Wind` but our settings keep it visually static. The shader's wind sampling is vertex-cost negligible regardless.
- **`Trunk01.mat` shader fields** themselves — left at the pack's defaults; only the global controller values matter.

### Performance

| Cost | Note |
|---|---|
| CPU per frame | One `Update()` call on `ALP8310Controller` setting ~7 shader globals — sub-microsecond. |
| GPU per frame | Wind animation is computed in the existing vertex shader of leaves/billboard — no extra draw call, no extra pass. Folded into the canopy's normal cost. |
| Memory | Zero. No textures, no meshes, no allocations. |
| Draw calls | Unchanged from pre-wind state. |
| Frame time impact | Not measurable on Quest 3. |

The component is `[ExecuteInEditMode]` so wind also animates in the Editor view — useful for tuning without needing a build.

### Why wind is not extended to other trees

When the wind controller was first added, an obvious follow-up question was: should the same controller animate the 38 pine trees (`tree_01` from Mountain Terrain) in the surrounding forest too? The investigation answer was **no**, and the controller is intentionally named `OakWindController` rather than `ForestWindController` to reflect that.

**Empirical scope check** — `ALP8310Controller` sets shader globals like `_GlobalWindIntensity` and `_GlobalWindPulse` on every `Update()`. Those globals only affect materials whose **shaders sample them**. The three ALP wind shaders that do are used by exactly three materials in the project:

| Material | Shader | Belongs to |
|---|---|---|
| `Branches001.mat` | `ALP Cutout Translucency Wind` | Oak canopy |
| `BillboardBigOak01.mat` | `ALP Cutout Translucency Wind` | Oak distant billboard |
| `Trunk01.mat` | `ALP Surface Detail Wind` | Oak trunk |

The 38 pine instances use Unity's built-in `Standard` (bark) + `Nature/Tree Soft Occlusion Leaves` (leaves) shaders, which do not read `_Global*` wind values. So the wind controller is **already naturally scoped to the oak** by shader compatibility — no extra guarding needed, no "rename to forest-wide" required.

**Why we don't extend to the pines** even though it's technically possible:

1. **Vendor pack fragility.** Replacing `leaves_01.mat`'s shader with `ALP Cutout Translucency Wind` would modify a file inside the gitignored `Mountain Terrain rocks and tree/` directory — lost on every fresh clone + re-import. We'd need an Editor helper to re-apply it after import, matching the `MittenHandsSetup` pattern, which adds maintenance surface for marginal gain.
2. **Missing vertex data.** The ALP shader bows individual leaves using vertex-color channels written by the FBX exporter (SpeedTree convention). The Mountain Terrain pine FBX doesn't have those channels — without them, the shader falls back to whole-tree displacement, which looks like the pines are being shoved by gusts rather than rustling. Wrong behaviour, not just wrong magnitude.
3. **Visual budget.** Even at `WindStrength 0.10`, 38 simultaneous swaying trees in peripheral vision is no longer "subtle" — it's a forest that's actively moving. The point of the slice was "the oak is the alive landmark, the pines are the static backdrop". Saturating the periphery breaks that composition.
4. **Real-world physics agrees.** Pines (coniferous, stiff needles, thick trunks) genuinely sway less than oaks (deciduous broadleaf) in a calm evening breeze. The current asymmetry matches what a Scandinavian forest actually does.

**If this question comes up again** (e.g. someone adds a third tree species and wonders if the same controller drives it): the answer is "only if the new species' material uses one of the three ALP wind shaders above". Anything using Standard, NSK, or other vendor shaders is invisible to this controller and would need either (a) shader replacement (per the fragility caveat above) or (b) a separate per-tree animation system (rejected by slice spec — no `Update()` transform wobble).

### Headset validation needed

- **Magnitude.** `WindStrength 0.10` is a first guess. If the canopy looks dead-still in headset (motion too subtle to read), bump to `0.20`. If it feels "windy" or distracting, drop to `0.06`.
- **Pulse.** `0.30` should feel like slow swells. If the rhythm reads as "stiff" or "mechanical", try `0.20`. If it feels like nothing, try `0.45`.
- **VR comfort.** Watch for any peripheral-motion discomfort during a 5+ minute session. If anyone reports nausea or distraction, reduce `WindStrength` and `BillboardWindIntensity` together.

All adjustable via the constants at the top of `OakAndFlowersSetup.cs` + re-running the menu — idempotent re-tune in seconds.

## Future options

In rough order of polish-value-per-effort:

1. **Second flower colour** — duplicate `MeadowFlower.mat` as `MeadowFlowerYellow.mat`, assign to one cluster. Tiny palette variation without changing geometry.
2. **Tune oak shader wind** — switch the canopy materials to `ALP Cutout Translucency Wind` (already in the pack) for a barely-visible leaf sway. Adds a per-vertex shader cost but no per-frame CPU.
3. **Ground stones near oak base** — a single rock_set_03 nestled at the oak roots would tie it visually to the existing fire-pit kerb stones. Re-run `Tools/Quest Setup/Ground Stones` after placement so the embed depth matches the kerb pattern.
4. **Distant pine variation** — if the oak makes the pine perimeter feel uniform-by-comparison, scatter 2–3 of the existing `tree_01` at varied Y-rotations to break the symmetry.
5. **Subtle sound** — bird ambience routed through an `AudioSource` parented to the oak canopy. Low volume, looped, spatial. Same cost as the existing `FireCrackleAudio`.
