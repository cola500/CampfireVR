# Terra asset audit + forest-floor + mountain-backdrop prototype

> Investigation slice plus a tiny reversible prototype. Companion to `docs/forest-atmosphere-slice.md` (commit `647bcaf`) and `docs/campfire-asset-audit.md`.

## Headline

**Terra is a terrain-editor extension, not a content pack.** 138 .cs files, an xNode visual-scripting graph framework, and a CoherentNoise procedural-generation library — purpose-built to *generate* Unity Terrain objects from noise nodes. It's not a "drop these prefabs in" pack.

**But:** its `Example/` folder ships demo textures and demo props alongside the system. The **textures are usable** standalone — they're pipeline-agnostic TGAs / PNGs that work on any Standard-shader material. The **prefabs are not** — their meshes have broken refs in Unity 6 (same failure mode as NatureStarterKit2's tree prefabs).

What this slice did with the findings:
1. Pulled one Terra demo texture (`DirtA` + matching normal) onto a new `ForestFloor` material applied to a small 6×6 m plane around the fire — a "scorched earth" patch.
2. Used the Mountain Terrain pack's `mountain_terrain_01.fbx` as a **distant static backdrop** (60×23×60 m, 35–95 m behind the fire). This wasn't from Terra but came up while exploring "berg i bakgrunden" — and it's just a static mesh, not Unity Terrain.

No procedural terrain. No xNode graph. No new shaders. No networking touched.

## Asset table

### What was tested

| Asset | Type | Pipeline | Renders? | Notes |
|---|---|---|---|---|
| `Assets/Terra/Example/Models/Rock 01/Rock 01 prefab.prefab` | Prefab + OBJ mesh | BiRP (`Legacy Shaders/Bumped Specular`) | **No — bounds (0,0,0)** | OBJ-import has broken mesh ref in Unity 6. Same failure mode as NatureStarterKit2 trees, different cause. Deleted from scene during testing. |
| `Assets/Terra/Example/Models/Bush 05/Bush 05 prefab.prefab` | Prefab + OBJ mesh | BiRP (`Legacy Shaders/Transparent/Cutout/Bumped Diffuse`) | **No — bounds (0,0,0)** | Same. Deleted. |
| `Assets/Terra/Example/Textures/DirtA.tga` (12 MB, ~4K) | Albedo texture | n/a | **Yes** | Used in this slice on `ForestFloor.mat`. |
| `Assets/Terra/Example/Textures/DirtANorm.tga` (12 MB) | Normal map | n/a | **Yes** | Used on `ForestFloor.mat` as `_BumpMap`. |
| `Assets/Terra/Example/Textures/GrassA.tga` + norm | Grass albedo + normal | n/a | Yes (not used in this slice) | Reserved for future grass-patch material. |
| `Assets/Terra/Example/Textures/GrassB.tga` + norm | Grass variation + normal | n/a | Yes (not used) | Same. |
| `Assets/Terra/Example/Textures/Grass Flower 7.png` | Grass with flowers | n/a | Yes (not used) | Same. |
| `Assets/Terra/Example/Example.unity` | Demo scene | n/a | Not opened | Would interrupt our active scene; not necessary for the textures. |
| **138 .cs files** in `Terra/Source/`, `Terra/Library/{xNode,CoherentNoise,ReorderableListEditorField}/` | Editor + runtime scripts | n/a | **Compile clean** | Verified via `recompile_scripts → 0 errors`. Unlike NatureStarterKit2, no compile errors in Unity 6. |
| 4 Terra-internal shaders (`TerrainFirstPass*.shader`, `GrassGeometryShader.shader`) | Custom shaders | Terrain-only / geometry-shader | Compile OK, not used in our build | Geometry shader on `GrassGeometryShader` would have been a Quest-mobility concern (no geometry shader support on some mobile GPUs); not invoked. |

### What was used in the prototype

| Asset | Path | Used as |
|---|---|---|
| `DirtA.tga` | `Assets/Terra/Example/Textures/DirtA.tga` | `_MainTex` on new `ForestFloor.mat` |
| `DirtANorm.tga` | `Assets/Terra/Example/Textures/DirtANorm.tga` | `_BumpMap` on `ForestFloor.mat` |
| `mountain_terrain_01.prefab` | `Assets/Mountain Terrain rocks and tree/Prefab/mountain_terrain_01.prefab` | Static distant backdrop (Mountain Terrain pack, **not Terra**) |
| `mountain_terrain_01.mat` | `Assets/Mountain Terrain rocks and tree/Materials/Terrain/mountain_terrain_01/mountain_terrain_01.mat` | Material on the backdrop. Standard shader, full PBR (albedo + normal + metallic/smoothness + AO). |

### What was rejected

| Asset / approach | Reason |
|---|---|
| Terra as a procedural terrain *system* | Generates Unity Terrain — out of scope per user's "no Unity Terrain, no terrain-system sprint" guardrail. Multi-step xNode setup beyond a tiny slice. |
| `Rock 01 prefab.prefab`, `Bush 05 prefab.prefab` | Broken mesh refs in Unity 6 — bounds (0,0,0), invisible. Same failure pattern we saw in NatureStarterKit2's `tree01..04` and `bush01..06` prefabs from the forest slice. **The "small props" / "stones / roots / leaves / grass clumps / stumps" mentioned in the task spec — Terra simply doesn't ship those as a usable prefab library in Unity 6.** |
| `Example.unity` demo scene | Opening would interrupt the active CampfireRoom scene. Not needed once we'd verified the textures work. |
| Terra:s grass textures (`GrassA`, `GrassB`, `Grass Flower 7`) | Saved for a potential later grass-patch slice. Out of scope here — dirt around the fire is the more focused move. |
| `GrassGeometryShader.shader` | Geometry shaders aren't universally supported on Quest GPU. Not used anywhere in our build. |

## Comparison: Terra vs. the committed forest slice

| Aspect | Forest slice (commit `647bcaf`) | Terra textures (this slice) |
|---|---|---|
| Ground material | `Ground` plane has the existing dark base material | New `ForestFloor` plane sits 1 cm above `Ground` with `DirtA` + normal at 4× tiling |
| Visible improvement | Trees + stones give silhouette, but ground itself is flat color | Textured dirt with a normal map under the fire — depth, detail, "scorched earth" cue |
| Object count added | 10 (forest slice) | +1 forest-floor plane, +1 mountain backdrop = 2 |
| Shaders introduced | 0 new | 0 new (Standard) |
| Quest cost | Sub-ms | Sub-ms — one extra plane draw, one large-but-distant mesh draw, both with `castShadows = Off` |

**Should we replace the plain Ground material instead of adding a patch?** Considered, rejected for now. Reasons:

1. The `ForestFloor` patch is **smaller (6×6 m)** than the full `Ground` (20×20 m). It reads as "the area lit by the fire is scuffed dirt; everything beyond is dark earth fading into night". That dual-layer is what gives the cozy-fire vibe — the warm dirt patch in the warm pool of light, the cold outer plane fading into shadow.
2. Replacing the whole `Ground` material would lift the entire scene's brightness — losing the contrast that makes the fire feel like the warm center.
3. Reversibility: deleting one `ForestFloor` GameObject restores the pre-slice state. Replacing the whole `Ground` material would need an explicit revert.

If the dual-layer reads as "obvious round patch" in headset, easy follow-up: scale the patch up to 12 m × 12 m, or feather the edges with a different shader, or replace `Ground`'s material with a darker DirtA tint.

## Three options (in spec order)

### A — Minimal safe improvement (this slice, *prototyped*)

- One 6×6 m `ForestFloor` plane just above the existing `Ground`, with `DirtA` + normal map at 4× tiling.
- One distant `mountain_terrain_01` static mesh as horizon backdrop at 35–95 m, scale 2×, height 23 m, `castShadows = Off`.
- Two extra GameObjects, two new asset references, no new shaders.
- Built and committed-ready. See "What was used" above.

### B — Slightly richer campfire ground (future)

- Replace the `Ground` plane's material with a darker variant of `DirtA` (slight color tint, no tiling change) to ground the whole scene.
- Or: add a second `ForestFloor` plane with `GrassA` 2 m further out from the fire (still inside the seat-ring) — concentric layers of "dirt → patchy grass → outer dirt".
- 2–3 extra plane GameObjects, 1–2 extra materials.
- Quest impact: trivial. One material + one extra plane draw call.

### C — Later "proper forest glade" version (multi-day, deferred)

- Use Terra *as the system* — set up an xNode graph that paints `DirtA`, `GrassA`, and `GrassB` as splat-maps across a small Unity Terrain (~30×30 m) under the campfire. This is what the package is actually built for.
- Risks: introduces Unity Terrain (sets up Terrain shaders, terrain renderer, possibly grass-painting which uses `GrassGeometryShader` — Quest-incompatible), and is a multi-step setup that breaks every "tiny slice" guardrail we've established.
- Alternative C': skip Terra and import a CC0 ground-decal pack (Polyhaven, ambientcg) for moss / pine-needles / fallen-leaf patches — adds 5–10 small flat decals around the fire pit. Lower scope than Terra, higher signal.

## Performance notes for Quest

- `ForestFloor.mat` uses **two 4096×4096 textures** (`DirtA.tga` = 12 MB, `DirtANorm.tga` = 12 MB). Unity compresses these to ASTC on Quest builds (~2–4 MB each on GPU), so runtime cost is fine, but **import-time and build-size are large**. If file-size becomes a problem before shipping, set the textures' Max Size to 1024 in import settings — at 6 m × 6 m × 4× tiling we get a 1024-texel repeat every 1.5 m which is plenty.
- `mountain_terrain_01.fbx` mesh is a single static mesh at ~30×30 m authored size. Its material has a full PBR set (albedo + normal + metallic/smoothness + AO). Quest will compress all of them. Single mesh, single material → one draw call. Distance 35–95 m → likely beyond any frustum-culled clutter, so essentially free per frame.
- Both new objects have `shadowCastingMode = Off`. Mountain also has `receiveShadows = false` (it's too far to need fire-light shadow detail and would just pop in/out at shadow-distance boundary).

## Before / after scene impact

| Pre-slice (commit `647bcaf`) | Post-slice (this prototype, uncommitted) |
|---|---|
| Dark base `Ground` plane, flat color | Same Ground + 6×6 m `ForestFloor` dirt patch with normal-mapped texture under the fire |
| No backdrop beyond the starfield skybox | A 60-m wide mountain silhouette ~50 m behind the fire, 23 m tall — visible over the tree line |
| Hex ring of 6 trees @ 8 m | Same |
| 4 fire-pit stones | Same |
| 10 new scene objects total in `647bcaf` | +2 here = 12 scene objects added across both slices |

Other scene parts (campfire mesh, flame, embers, FireLight, audio, starfield, network rig, tutorial panel, seats) **untouched**.

## Recommended next step

If the new dirt-Ground + mountain backdrop read OK in headset:

1. **Commit this slice** as a bundle with the stone-seats slice — small, focused, reversible.
2. **Optionally bump down `DirtA.tga` and `DirtANorm.tga` import Max Size to 1024** before the next remote-fika build to shave ~20 MB off the APK. Quick Inspector tweak; safe to do later.
3. **Future grass-patch slice** can reuse `GrassA.tga` and `GrassB.tga` on a similar pattern — applied to a separate plane or replacing this Ground material.

If the dirt tiling reads as obvious repeats or the mountain feels too close / too far / too prominent: scale and reposition are 5-second Inspector tweaks (or one-line edits in `ForestFloorSetup.cs` / re-run with new constants). No code refactor needed.

## Update — went to full-Ground swap rather than 6×6 m patch

Above sections were written when the plan was a separate 6×6 m `ForestFloor` plane just above the existing `Ground`. After seeing it the user asked for the whole floor covered. Final state:

- `ForestFloorSetup.cs` now **assigns `ForestFloor.mat` directly to the existing `Ground` plane's MeshRenderer** (no extra plane).
- **Tile factor bumped from 4× to 13×** so the 20×20 m Ground gets ~1.5 m per texture repeat — same perceived dirt-grain scale as the 4× tiling on the original 6×6 m patch.
- The helper **also removes any legacy 6×6 m `ForestFloor` GameObject** if a previous run left one. Re-runnable cleanly from either starting state.

The "What was used" / "Before / after" / "Three options" sections in this doc still describe the original patch approach as a historical record; the actual scene shipped uses the full-Ground swap.

## Verification stamps

- `recompile_scripts` after Terra import: **0 errors, 0 warnings**
- `Tools/Quest Setup/Apply Forest Floor` ran cleanly: log `[ForestFloorSetup] Forest-floor patch at (0, 0.01, 0), 6.0 × 6.0 m, tiling 4×.`
- `mountain_terrain_01` post-set_transform: position (15, 0, 80), scale (2, 2, 2), `shadowCastingMode = Off`, `receiveShadows = false` — verified via `get_gameobject`
- Test instances of `Rock 01 prefab` and `Bush 05 prefab` deleted before save
- No console errors after final save
- Nothing touched: networking, voice, menu, XR rig, multiplayer
