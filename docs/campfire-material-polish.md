# Campfire material polish

> Small art pass: the wood pile now has actual bark texture + normalmap from the Mountain Terrain pack — same bark that the surrounding trees use, so the campfire reads as belonging in the forest. Fire-pit stones and stone seats untouched (already full PBR via `rock_set_mat`).

## The problem (before)

`CampfireWood.mat` was flat dark brown — no albedo texture, no normalmap, just `_Color = (0.18, 0.10, 0.06)` and Standard shader. Authored quickly when we swapped the capsule logs for Piloto's `SM_campfire_001` mesh, intentionally placeholder. After three rounds of forest/floor/mountain/seat polish around it, the campfire itself was the visually weakest object in the scene — a brown blob against a fully-textured forest.

## Inventory of usable wood textures

| Asset | Path | Notes | Picked? |
|---|---|---|---|
| `bark01.png` (Mountain Terrain) | `Assets/Mountain Terrain rocks and tree/Materials/Trees/tree_01/bark01.png` | 2.5 MB PNG, used today by every `tree_01` in the forest. Shares visual family with the trees so the campfire reads as part of the same world. | **Yes — albedo** |
| `bark01_normal.png` (Mountain Terrain) | same folder | 2.6 MB PNG, matching normal for the bark texture. Publisher shipped it with `textureType: Default` (color), not NormalMap — a bug. Fixed as a side effect of this slice. | **Yes — bump map** |
| `bark01.tga` (NatureStarterKit2) | `Assets/NatureStarterKit2/Textures/bark01.tga` | 680 KB, smaller. Could work but it's bound to Tree Creator legacy materials in that pack; the texture itself is portable. | No — Mountain Terrain bark already in use by trees |
| `bark02.tga` (NatureStarterKit2) | same folder | 592 KB, paler bark variant | No — visual unity > variety here |
| `branch01.tga` (NatureStarterKit2) | same folder | 7.5 MB, large + branch-shaped, wrong for log surface | No |
| `bark_01.mat` (Mountain Terrain) | `Assets/Mountain Terrain rocks and tree/Materials/Trees/tree_01/bark_01.mat` | Already in scene on the tree trunks. Has a publisher bug — the normal texture is in the `_MetallicGlossMap` slot, `_BumpMap` is null. Re-using as-is would inherit the bug. | No — better to author our own material with both textures in the right slots |

## What was changed

### `Assets/Materials/CampfireWood.mat`

Before:
```
Shader: Standard
_Color: (0.18, 0.10, 0.06)
_MainTex: null
_BumpMap: null
_Metallic: 0, _Glossiness: 0.15
```

After:
```
Shader: Standard
_Color: (0.60, 0.46, 0.32)   ← warmer brown, lets bark detail read
_MainTex: bark01.png
_BumpMap: bark01_normal.png  ← _NORMALMAP keyword enabled
_Metallic: 0, _Glossiness: 0.10  ← slightly more matte
```

Tiling left at default `(1, 1)` — the FBX has its own UV layout so the bark wraps the log shapes naturally without per-instance tweaks.

### Publisher-bug side fix

`bark01_normal.png` shipped with `textureType: Default` (Unity treats it as a color texture). The polish helper switches it to `NormalMap`, which is what it actually is. Unity re-packs the pixel data correctly on reimport.

Side-effect on `bark_01.mat` (used by all tree trunks): the texture is still referenced — in the wrong slot (`_MetallicGlossMap` instead of `_BumpMap`). Tree trunks render the same as before because Quest tolerates that misuse and shadows are off on every tree anyway. So no visible regression on the forest.

### What was NOT changed

- **Fire-pit stones** (`rock_set_01..04`, each at scale 0.4×): already use `rock_set_mat`, which is a full PBR Standard material (albedo + normalmap + metallic-smoothness + AO). They already look like stones. Touching them would be polish-for-polish-sake.
- **Stone seats** (`StoneSeat_A`, `StoneSeat_B`, and the 7 user-added duplicates): same `rock_set_mat`. Same reasoning.
- **The user's added 7 perimeter stones**: same `rock_set_mat`.
- **`SM_campfire_001`'s mesh itself**: unchanged. Same Piloto wood pile geometry that we swapped in at commit `ec9565a`.

## Editor helper

`Tools/Quest Setup/Apply Campfire Material Polish` (in `Assets/Editor/CampfireMaterialPolish.cs`):

- Switches `bark01_normal.png` to `NormalMap` import type if needed (idempotent — no-op if already correct).
- Loads `bark01.png` + `bark01_normal.png` from the Mountain Terrain pack.
- Sets `_MainTex`, `_BumpMap`, `_Color`, `_Glossiness`, `_Metallic` on `CampfireWood.mat`.
- Enables the `_NORMALMAP` shader keyword.
- Marks the material dirty and saves the asset.

Idempotent. Re-runnable. Safe to run any time.

## Quest performance

- **No new objects.** Material change only.
- **Two textures added to the build path that were already there** (used by trees). Zero APK size impact.
- **No shadows change.** Wood pile still has `castShadows = On` (it's the campfire centre and looks better with a shadow under it from FireLight). Mountain Terrain rocks/seats remain `Off` (set by `ForestSetup`).
- **No new shader.** Standard shader is what the material had before — just with two more texture slots populated.
- **`_NORMALMAP` keyword adds one shader variant** that the build's already compiling because trees use the same albedo texture path. No fresh variant compilation.

## Reversibility

Three independent ways to undo:

1. **In the Inspector**: open `Assets/Materials/CampfireWood.mat`, clear `_MainTex` and `_BumpMap`, set `_Color` back to `(0.18, 0.10, 0.06)`. Material returns to placeholder dark brown.
2. **Git revert** this commit (when made).
3. **Delete `CampfireMaterialPolish.cs`** and re-create the material via `ForestFloorSetup` or by hand. The bark01_normal.png import-type change is also reversible via the Inspector (Texture Type → Default) — but there's no reason to revert it, since NormalMap is the correct type for that file.

## Visual direction — what's still placeholder

The spec asked about three nice-to-haves we didn't do because they'd grow the slice:

- **Slightly lighter / drier edges on logs.** Would need a second material per log + UV-aware vertex paint, or a custom shader. Out of scope; current uniform tint reads as "log wood that hasn't been burnt yet", which is fine for an unlit-pile-of-wood reading.
- **Darker charred side near the fire.** Same problem — needs per-vertex blending or a second material on the burned half of the mesh. Future slice if it ever feels missing.
- **Mossy stones.** Could overlay a green secondary albedo on the fire-pit stones via the Standard shader's `_DetailAlbedoMap` slot. Trivial 1-texture addition if a CC0 moss texture is imported. Out of scope here.

None of these is needed for the campfire to feel cosy. The dominant visual signals in headset are the flame + warm flicker light + crackling audio — the wood is supporting cast.

## Next tiny polish step if needed

In rough priority order:

1. **Detail map on `rock_set_mat`** — a subtle moss / lichen secondary texture in the `_DetailAlbedoMap` slot. Would let every rock in the scene (fire-pit + seats + perimeter) read with more variety without needing different materials. ~1 hour with a CC0 moss texture.
2. **Slight bake of fire-pit darkening** — a small dark patch decal under the campfire mesh (separate plane + transparent texture or vertex-tinted area on the Ground). Sells "scorched earth" beyond what the current ForestFloor material can do.
3. **Secondary log mesh on top of `SM_campfire_001`** — one or two `rock_set` props styled as fallen branches near the fire base. Would let the campfire silhouette have some asymmetry without changing the main mesh.

All optional. Current state is the smallest fix that addresses the "brown blob" feedback.

## Validation

- `recompile_scripts` after adding the helper: **0 errors, 0 warnings**.
- `Apply Campfire Material Polish` ran cleanly:
  - `[CampfireMaterialPolish] Switched … bark01_normal.png import type to NormalMap.`
  - `[CampfireMaterialPolish] Updated CampfireWood: bark albedo + normal applied, tint RGBA(0.600, 0.460, 0.320, 1.000), smoothness 0.1.`
- `get_material_info Assets/Materials/CampfireWood.mat` post-run: `_MainTex` = bark01.png, `_BumpMap` = bark01_normal.png, `_Color` = (0.6, 0.46, 0.32), `_Glossiness` = 0.1. ✓
- Console after polish run: **empty**, no errors.

## Untouched

Networking, voice, menu, XR rig, LAN/Internet logic, tutorial panel, ForestFloor material on Ground, mountain backdrops, the campfire's Flame / VFX_Fire / Embers / FireLight / crackle audio, starfield, trees, tree wind, fire-pit kerbstones' `rock_set_mat`, seat stones' material.
