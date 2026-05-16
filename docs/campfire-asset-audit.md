# Campfire asset audit

> Investigation only. No scenes, materials, or assets were modified. Verifies what we already have locally before any custom authoring.

## Headline finding

**The project has zero bark / wood / log / ash / stump textures.** A `find` across `Assets/` for those keywords returned only false positives (logger scripts, the Piloto trashcan prefab, a changelog). Neither imported Asset Store pack ships realistic wood content — both are FX-oriented.

Three other useful but constrained things *are* in the project:

1. The polish-slice **EmberParticleMaterial** + procedural soft-disk texture (already wired under `Flame/Embers`).
2. The integrated **WALLCOEUR `VFX_Fire` flame** with `Mobile/Particles/Additive` shader on a `a_VFX_flame.png` texture (already in the scene as `Flame/VFX_Fire/VFX_Fire`).
3. The looping **`campfire_crackle.wav`** spatial audio source (already on `FireCrackleAudio`).

Piloto Studio's 5 campfire **meshes** are still on disk and well-modelled, but their materials are **orphaned** — the `m_gradient` material they reference (`guid: 2a968aa3406ef9441a14e5e68e2f7bab`) was inside the HDRP `Piloto Studio/Shaders/` folder we deleted on 2026-05-16 to silence shader-compile errors. Today, instantiating any `SM_campfire_*.prefab` renders the wood logs in Unity's default-material grey (the FBX `externalObjects` block in the `.meta` still points at the deleted GUID; on import Unity falls back to default).

So the practical situation is:
- We have **good fire FX** (own embers + WALLCOEUR flame). Done.
- We have **adequate ambient audio**. Done.
- We have **passable but ugly logs** (capsule primitives, no material assigned, but functional and Quest-friendly).
- We have **zero realistic wood look** to apply, and the meshes that *could* look better (Piloto's) need new materials to do so.

## Asset tables

### Piloto Studio — `Assets/Piloto Studio/` (gitignored, EULA)

#### Campfire / wood meshes (`Campfire And Torches Pack/Models/`)

| Asset | Type | Likely use | Quality | Pipeline status | Material status |
|---|---|---|---|---|---|
| `SM_campfire_001.fbx` | Stylized low-poly campfire (stacked logs + small stones) | **Direct replacement for our capsule logs.** | Good — clean low-poly silhouette, ~few hundred tris | Mesh itself is pipeline-agnostic (just geometry) | **Broken** — references deleted `m_gradient.mat` GUID; renders default grey on instantiate |
| `SM_campfire_002.fbx` | Variant arrangement | Alt log arrangement | Same | Same | Same |
| `SM_campfire_003.fbx` | Variant arrangement | Alt log arrangement | Same | Same | Same |
| `SM_campfire_004.fbx` | Variant (likely larger) | Alt | Same | Same | Same |
| `SM_campfire_005.fbx` | Variant (likely larger) | Alt | Same | Same | Same |
| `SM_planks_001.fbx`, `SM_planks_002.fbx` | Wood planks | Could be cross-pieces or kindling | Same | Same | Same |
| `SM_torch_*.fbx` (3), `SM_irontorch_*.fbx` (3) | Torches | Out of scope for a campfire | Same | Same | Same |
| `SM_props_candle_01/02.fbx` | Candles | Out of scope | Same | Same | Same |
| `SM_trashcan_001.fbx` | Trashcan | Cute, but absolutely not for a forest fire | Same | Same | Same |
| `InfiniteBackground_Better.fbx` (in `Models/`) | Backdrop card | Could double as a "horizon" billboard | Unknown without inspecting | Mesh OK | Material broken |

#### Piloto Studio textures (`Piloto Studio/Textures/`)

| Asset | Type | Usable for cozy fire? |
|---|---|---|
| `FireQuad_Stylized.png` | Stylized flame quad | Could feed a Mobile/Particles/Additive flame layer as an alternative to WALLCOEUR's `a_VFX_flame.png` |
| `FireNoise.png` | Tileable fire noise | Useful in a custom flame shader (we don't have one) — skip |
| `SmokeHarsh_BlackSpots.png` | Smoke noise | Useful if we add smoke (we explicitly aren't) |
| `Trail_WispyGeneric.png` | Trail / wisp | FX trails — out of scope |
| `Flare_FatDot.png`, `Flare_Glowdot.png`, `Flare_Cross.png` | Lens flare quads | FX flares — out of scope |
| `Glowdots_Speckles.png` | Bokeh speckle | FX — out of scope |
| `HalftoneCrescent.png` | Halftone crescent ring | FX — out of scope |
| `gradient_004.png` | 1D color ramp | Could feed a custom toon shader — we don't have one, skip |
| `PackedNoises_Generic.png` | Channel-packed noises | Custom shader fodder — skip |
| `tex_Noise_Caustic_Normal.png` | Normal-map noise | Could be a subtle bumpy log normal map — but unusable on its own without an albedo |
| `Heart_PinkPuffy.png` | Pink heart | 🤷 |
| `Piloto_Logo.png` | Publisher logo | Don't ship |

**There is no bark, no wood-grain albedo, no ash, no burnt overlay anywhere in Piloto's textures.** It's a stylized-FX pack; the campfire models were always meant to be rendered with the gradient toon shader that's now gone.

#### Piloto Studio materials (`Piloto Studio/Materials/`)

All surviving Piloto materials reference the HDRP `UberFX` shader (`d985403ea514e7c46bf7c2fab31b9d95`) we determined incompatible in `docs/piloto-campfire-evaluation.md`. They render as `Hidden/InternalErrorShader` (magenta) if used in our BiRP project. Skip them entirely. List: `InfiniteBG.mat`, `Fire_AlphaDistort_Ramped.mat`, plus 9 shared FX materials (`FuzzballAdditive_Soft`, `Trail_WispyGeneric_Alpha`, `Nokdef_Add_Soft`, `Flare_FatDot_Alpha`, `FuzzAdd`, `Flare_FatDot_Add_Soft`, `Flare_GlowdotPickbook_Add`, `Crescent_Halftone_Add`, `Smoke_HarsherAlbedo_Alpha_Soft`).

The `m_gradient.mat` material that the campfire FBX prefabs reference **no longer exists** — it was inside the deleted `Shaders/SimplyToon_Piloto/` folder.

### WALLCOEUR — `Assets/VFXPACK_FIRE_WALLCOEUR/` (gitignored, EULA)

Already evaluated in detail in `docs/vfx-fire-package-evaluation.md`. Repeating the cozy-fire-relevant subset here:

| Asset | Type | Pipeline status | Currently used? |
|---|---|---|---|
| `Prefab/VFX_Fire.prefab` | Orange flame + smoke (2 PS) | BiRP-compatible — `Mobile/Particles/Additive` | **Yes** — integrated as `Flame/VFX_Fire`. Smoke layer disabled (`maxParticles: 0`, renderer off). |
| `Prefab/VFX_Fire 1.prefab` | Variant flame + smoke (2 PS, different smoke material) | BiRP-compatible | No |
| `Prefab/VFX_BlackSmoke.prefab`, `VFX_Smoke.prefab` | Smoke (1 PS each) | BiRP-compatible | No — out of scope (no smoke guardrail) |
| `Prefab/VFX_GroundFire_Circle.prefab`, `VFX_GroundFire_Line.prefab` | Spread fire over an area (3 PS) | BiRP-compatible | No — for floor fire, not a campfire |
| `Prefab/VFX_TorchLight.prefab` | Torch (3 PS) | BiRP-compatible | No — too tall + busy |
| `Prefab/VFX_*_Green.prefab` (6 variants) | Same FX, green tint | BiRP-compatible | No — supernatural-looking, not cozy |
| `Material/A_FlameAdd 1.mat` | Additive flame | `Mobile/Particles/Additive` ✓ | Yes (via VFX_Fire) |
| `Material/A_SmokeAdd.mat` | Additive smoke | `Mobile/Particles/Additive` ✓ | No |
| `Material/A_SmokeAlpha_1.mat`, `A_SmokeAlpha_2.mat` | Alpha smoke | `Mobile/Particles/Alpha Blended` ✓ | No |
| `Material/GridMat.mat` | Demo grid | External shader GUID, not in project | Skip |
| `Texture/a_VFX_flame.png` | 431 KB flame texture (warm, soft-edged tongues) | Pipeline-agnostic PNG | Yes (via the material above) |
| `Texture/A_Smoke.png`, `A_Smoke_2.png` | ~900 KB smoke textures | Pipeline-agnostic | No |
| `FireScene_Volume.asset` | URP post-processing Volume profile | URP-only; silently ignored in BiRP | Skip |
| `FireVFX.unity` | URP demo scene | URP-only; do not open | Skip |

**No wood, no logs, no ground, no bark from WALLCOEUR.** It's a pure VFX pack.

### In-repo authored assets

| Asset | Type | Currently used? | Notes |
|---|---|---|---|
| `Assets/audio/campfire_crackle.wav` | 6.3 MB looping spatial audio | Yes — `FireCrackleAudio` on `Flame` at volume 0.3 | Already cozy and present. No competing assets to choose from. |
| `Assets/Materials/FlameMaterial.mat` | Standard shader, warm orange `(1.0, 0.55, 0.15)` with emission `(1.6, 0.8, 0.288)` | **Disabled** — the capsule renderer was turned off when VFX_Fire integrated | Kept as a fallback. Could be re-enabled in seconds if needed. |
| `Assets/Materials/EmberParticleMaterial.mat` | `Mobile/Particles/Additive`, warm tint | Yes — `Flame/Embers` (24-particle drifting embers) | Already wired and cheap on Quest. |
| `Assets/Materials/EmberParticleTex.png` | 32×32 procedural soft-disk RGBA (4 KB) | Yes — used by EmberParticleMaterial | Generated by `Editor/CampfirePolishSetup.cs` if missing; safe to keep. |
| `Assets/Real Stars Skybox/` | Asset Store starfield (gitignored) | Yes — `RenderSettings.skybox` via `NightAtmosphere.cs` | Out of scope for this audit (sky, not fire). |

### Asset gap (the actually-useful summary)

What we **don't** have and would need for anything beyond the current setup:

- A **bark / wood albedo texture** (anything from 256×256 stylized to 1024×1024 PBR works for Quest).
- A **wood normal map** (we have `tex_Noise_Caustic_Normal.png` but it's noise-shaped, not bark-shaped).
- An **ash / charcoal albedo + alpha** for a darker patch under the fire pit.
- **Stones** if we want a ring around the pit (Piloto's `SM_campfire_*` includes small stones in the mesh, but we can't render them properly without solving the material problem).
- **Additional ambient audio** — wind, distant owl, leaves rustling. Right now there's only the crackle.

None of these are sitting unused in the project. Every option below either lives with what we have or imports new content.

## Proposed upgrade tiers

### A — Fastest "good enough cozy" (no new assets, ~30 minutes)

The premise: accept that the current setup *already does the job* once polished a hair. Three tiny moves:

1. **Apply our existing `FlameMaterial`** (Standard + emission) to **`Log_1` and `Log_2`**. They currently render with default-grey material — tinting them to a warm dark-brown (e.g., `_Color = (0.18, 0.10, 0.06)`, no emission, no metallic) reads as "wooden log" at three metres of headset distance without any texture. New material asset: `Assets/Materials/LogMaterial.mat`, one Standard shader, three slider values. Sub-millisecond Quest cost.
2. **Darken the `Ground`** material in the same pass to a damp-earth brown (`(0.10, 0.08, 0.06)`) so the warm fire light pool reads strongly against it.
3. **Optionally**: scale `Embers` emission up from 4.5/s to ~7/s while we're tuning. Already exposed in the existing Editor menu (`Tools/Quest Setup/Apply Campfire Polish`).

That's it. **No imports, no shader work, no scene-layout changes.** The cozy feel comes from the already-integrated VFX_Fire flame + Embers + warm flicker light + crackle audio + starfield. Tinting the surrounding props turns them from "Unity primitives in a void" into "wooden things lit by a fire."

### B — Slightly more polished (one CC0 import, ~2–3 hours)

For a meaningful step up, import one (1) CC0 or CC-BY tileable bark texture from Polyhaven or ambientcg.com:

- Suggested asset: Polyhaven "**Bark Brown 02**" or "**Wood Plank 02**" at 1K resolution (~2 MB compressed).
- Pipeline: download as PNG/JPG, import with `sRGB = true`, generate mipmaps, Quality compression.
- Optionally also grab the matching normal map (1K, ~1.5 MB).
- Wire to a new `Assets/Materials/LogBark.mat` (Standard shader, albedo + normal, no metallic, smoothness 0.2 for matte).
- Apply to `Log_1` / `Log_2`. Tiling math (since logs are world-size 0.25 m thick × 1.4 m long, capsule mesh has UVs spanning [0,1]):
  - Along the log (length axis): tiling **2.0** so each repeat is ~70 cm — feels like wood-grain rings.
  - Around the log: tiling **1.0** (one wrap per capsule circumference) — avoids visible seams.
  - Offset: leave at (0, 0).
- Optionally also import one CC0 dirt/forest-floor albedo (~1 MB) for `Ground` with tiling around **4.0** (one repeat per 5 m).

Single material change per object, single shader, no new code, no new prefab. Same Quest cost as tier A.

Tier B is the next slice I'd actually pitch.

### C — Dream version later (multi-day, scope creep risk)

Things to keep in mind but not commit to:

- **Replace capsule logs with Piloto's `SM_campfire_001` mesh.** Requires solving the orphaned `m_gradient.mat` reference — either author a new BiRP `gradient`-style material (could be Standard shader + texture from `gradient_004.png` as albedo, or a Unity gradient ramp shader from the Asset Store), or do a custom toon-lit shader. Realistic stretch goal: 2–3 days including iteration.
- **Add 3–5 small stones** around the fire pit. No stone asset is imported; would need a CC0 pebble pack from Polyhaven (~30 MB for a small collection), one new material, scattered manually.
- **Replace the WALLCOEUR flame texture with `FireQuad_Stylized.png`** from Piloto's surviving textures (BiRP-compatible — it's just a PNG; only the materials around it were problematic). Worth A/B comparing in headset because WALLCOEUR's particles render in **Mesh** mode not Billboard — they may read as too volumetric for our cozy scale.
- **Soundscape layer 2**: add a CC0 wind-through-trees loop at 5 % volume, ducked when voice is active. Need to import an audio asset; would also need a small `AmbientMixer` script.
- **Bloom / post-processing**: BiRP's built-in `PostProcessing` package would let the flame edges bleed warmly. Significant scope (BiRP post-processing is the deprecated stack, and the URP/HDRP stack would need a pipeline migration).
- **Fire-pit ground darkening**: a decal or a darkened mesh patch beneath the flame to suggest scorched earth. Needs a CC0 ash texture and a small decal mesh.

All of tier C is genuinely *nice* and none of it is needed. The current scene is already strong enough for the remote fika test.

## Current `Log_1` reference (for tier A tiling)

Read live from the scene via mcp-unity:

```
Log_1
  position: (0, 0.15, 0)         world Y = 0.15 (rests on ground)
  rotation: (90°, 0°, 0°)        capsule axis laid along world Z
  localScale: (0.25, 0.7, 0.25)  → world bounds (0.25, 0.25, 1.4)
  MeshFilter: Unity built-in Capsule
  MeshRenderer.material: (not surfaced via mcp-unity get_gameobject;
                          presumed Unity default-material grey since
                          no LogMaterial exists in Assets/Materials/)
  CapsuleCollider: enabled
```

So a log is currently a **25 cm thick × 1.4 m long horizontal sausage** with no material assignment. `Log_2` is symmetric on the other side of the fire (not inspected this audit, but the scene structure implies it).

## Bonus — bark texture recommendation

**Cannot apply: no bark texture exists in the project.** A search for `*bark*`, `*wood*`, `*log*` across `Assets/` returned only scripts (Logger.cs files), the Piloto Logo, and the trashcan prefab — none of which are usable as a log albedo.

If you want to make the tier B recommendation real, the smallest single import I'd suggest is:

- **Polyhaven "Bark Brown 02"** (or any of their bark scans), 1K resolution. CC0. ~2 MB compressed.
- Import path: `Assets/Materials/Polyhaven/Bark_Brown_02_albedo.jpg` (and `_normal.jpg`).
- New material `Assets/Materials/LogBark.mat`, Standard shader, smoothness 0.2, no metallic.
- Tiling on Log_1/Log_2: `_MainTex_ST` scale = **(2.0, 1.0)**, offset (0, 0). Adjust to taste in the Editor — at this scale one tiling unit ≈ one capsule wrap, two units along the log gives a clear wood-grain repeat without obvious seams.
- Same material applied to both logs. Add to `.gitignore` mirroring the existing CC0/EULA pattern? Polyhaven is CC0 so it can actually be committed if we want — but mirroring the gitignored pattern keeps the repo lighter and is one less thing to remember.

If you'd rather **stay with zero imports** for now, tier A's plain warm-brown tint reads convincingly enough in a dim night scene. Texture realism matters less than colour temperature when the only light source is the flickering fire.

## Recommended next move

**Tier A (no imports, 30 min)** if the goal is to make next week's remote fika feel cozier without any new tooling overhead. The result will be: warm flame + drifting embers + warm pool of flickering light + crackle + dark brown logs sitting on dark earth. That's a cozy campfire.

**Tier B (one bark import)** if you want the logs to actually *look* like wood, not just be brown shapes. Pitch this only if tier A gets shipped and feels insufficient in headset.

Tier C is for if/when the project picks up environmental-art momentum.

## Verification stamps

- All paths verified to exist in `UnityProject/Assets/` via `find`.
- All Piloto material → shader chains verified broken via GUID resolution against `Assets/` (no longer present after the 2026-05-16 cleanup).
- WALLCOEUR material → shader chains verified in `docs/vfx-fire-package-evaluation.md` (commit `4c65f15`).
- Log_1 transform verified live via `mcp-unity get_gameobject`.
- No bark/wood textures verified absent via case-insensitive `find` across `Assets/`.
- Nothing in this audit touched a scene, material, asset import, or script.
