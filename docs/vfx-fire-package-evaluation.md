# VFX URP — Fire Package — evaluation

> Investigation slice. Nothing in the scene was changed. Verdict at the bottom. Companion to `docs/piloto-campfire-evaluation.md` — same disciplined approach, very different outcome.

## Goal

Decide whether the locally imported **WALLCOEUR — VFX URP - Fire Package** (`Assets/VFXPACK_FIRE_WALLCOEUR/`, gitignored) gives a presence upgrade over the polish-slice campfire on Quest **without** requiring URP migration.

## What's in the import

5.6 MB total, three folders + a demo scene + a post-processing volume:

| Path | Contents |
|---|---|
| `Prefab/` | 14 prefabs — fire / smoke / torchlight / ground fire, in regular and "green" variants |
| `Material/` | 5 materials — `A_FlameAdd 1`, `A_SmokeAdd`, `A_SmokeAlpha_1`, `A_SmokeAlpha_2`, `GridMat` (a debug grid) |
| `Texture/` | 3 textures — `a_VFX_flame.png` (431 KB), `A_Smoke.png` (884 KB), `A_Smoke_2.png` (912 KB) |
| `FireVFX.unity` | Demo scene |
| `FireScene_Volume.asset` | Post-processing volume profile (URP) |

No `.vfx` files (no VFX Graph) and no `.shader` files. **The pack ships zero shaders.** All materials reference Unity's built-in shader bundle.

## The surprise: this pack is BiRP-compatible out of the box

The "URP" in the package name turns out to refer to the **demo scene**, not the prefabs. Reading the materials directly:

| Material | `m_Shader: {fileID:N, guid:0000…f000…000}` | Resolves to |
|---|---|---|
| `A_FlameAdd 1.mat` | `fileID: 10720` | **`Mobile/Particles/Additive`** (built-in BiRP) |
| `A_SmokeAdd.mat` | `fileID: 10720` | **`Mobile/Particles/Additive`** (built-in BiRP) |
| `A_SmokeAlpha_1.mat` | `fileID: 10721` | **`Mobile/Particles/Alpha Blended`** (built-in BiRP) |
| `A_SmokeAlpha_2.mat` | `fileID: 10721` | **`Mobile/Particles/Alpha Blended`** (built-in BiRP) |
| `GridMat.mat` | external GUID `933532a4fcc9baf4fa0491de14d08ed7` | Not in project — only used by demo scene; ignore |

The `0000000000000000f000000000000000` GUID is Unity's built-in resource bundle (`Resources/unity_builtin_extra`); the `fileID` selects the specific shader. `Mobile/Particles/Additive` is **the same shader our existing Embers material uses** — so we already know it renders correctly in this project, on this Quest, in this build path.

**No URP package install required.** No HDRP install required. The fire/smoke prefabs render identically in BiRP and URP because they only depend on shaders that ship with every Unity install.

The only URP-flavoured content is `FireScene_Volume.asset` — a post-processing Volume profile referencing a `Vignette` MonoBehaviour with script GUID `899c54efeace73346a0a16faa3afe726` (URP's `UnityEngine.Rendering.Universal.Vignette`). Without the URP package installed, Unity stores it as an unrecognized MonoBehaviour and **silently ignores it**. No errors, no warnings on import — verified in the editor console after the import landed (clean: 0 errors, 0 warnings).

## Per-prefab inventory

All 14 prefabs use only ParticleSystem components — no Lights, no MeshRenderers, no AudioSources, no MonoBehaviours. They expect the host scene to provide light and audio (which fits us perfectly: keep our existing `FireLight` + `FireLightFlicker` + `FireCrackleAudio`).

Particle counts: **`maxNumParticles: 20`** on every system inspected. For comparison, our current `Embers` system uses 24, and Piloto's AmbienceFX prefabs allocate roughly 120+ across 6 systems. WALLCOEUR sits comfortably in the cozy/Quest-safe bracket.

Sorted by complexity (lines / particle systems / materials):

| Prefab | Lines | PS | Materials |
|---|---|---|---|
| VFX_BlackSmoke | 4,832 | 1 | A_SmokeAlpha_1 |
| VFX_Fire_Green | 4,832 | 1 | A_SmokeAdd (despite the name, this is single-PS, not pair) |
| VFX_GreenSmoke | 4,832 | 1 | A_SmokeAlpha_1 |
| VFX_Smoke_Green | 4,832 | 1 | A_SmokeAlpha_1 |
| VFX_Smoke | 4,832 | 1 | A_SmokeAlpha_1 |
| **VFX_Fire** | 9,663 | **2** | **A_FlameAdd 1 + A_SmokeAdd** |
| VFX_Fire 1 | 9,663 | 2 | A_SmokeAlpha_2 + A_SmokeAdd |
| VFX_TorchLight_Green | 9,663 | 2 | A_SmokeAlpha_1 + A_SmokeAdd |
| VFX_GreenTorchLight | 9,663 | 2 | A_SmokeAlpha_2 + A_SmokeAdd |
| VFX_GroundFire_Circle | 14,494 | 3 | A_SmokeAlpha_1 + A_SmokeAdd + A_FlameAdd 1 |
| VFX_GroundFire_Circle_Green | 14,494 | 3 | same |
| VFX_GroundFire_Line | 14,494 | 3 | same |
| VFX_GroundFire_Line_Green | 14,494 | 3 | same |
| VFX_TorchLight | 14,494 | 3 | A_SmokeAlpha_1 + A_SmokeAdd + A_FlameAdd 1 |

The only prefab that contains an **orange flame** is **`VFX_Fire`** (and the ground / torch composites, which carry it as one of three layers). Every other "Fire" name in the pack is either smoke-only, a green/supernatural variant, or a composite for floor/torch use cases.

## Quest cost assessment

Conservative back-of-envelope, assuming the lightest path (one flame ParticleSystem + smoke disabled):

- 20 particles × additive blend × 1 system ≈ same fillrate cost as our existing Embers (24 particles, additive). Sub-millisecond on Quest 3.
- Texture footprint: `a_VFX_flame.png` is 431 KB compressed PNG → roughly 256 KB on GPU after ASTC compression (Quest default for textures). Negligible vs the 256 MB Quest 3 has available.
- Single draw call per ParticleSystem, GPU instancing supported on this shader.
- Smoke layers (if enabled) would add ~900 KB texture each plus an alpha-blended pass — alpha blend is more expensive than additive on Quest. The "no smoke" guardrail saves us this.

For context vs the polish slice:

| | Polish-slice (current) | Piloto AmbienceFX | WALLCOEUR VFX_Fire (flame only) |
|---|---|---|---|
| Pipeline | BiRP | HDRP-only ❌ | BiRP-compatible ✅ |
| Renders in our project? | Yes | No (magenta) | Yes |
| Max particles | 24 (Embers only) | ~120+ across 6 systems | 20 (single flame system) |
| Materials | 1 procedural | 5 HDRP shaders | 1 built-in Mobile/Particles/Additive |
| Includes smoke? | No | Hidden smoke layer (would need disabling) | Has smoke (can disable manually) |
| Brings own Light? | No (uses our `FireLight`) | No | No |
| Brings own audio? | No (uses our crackle) | No | No |
| Quest cost | < 1 ms | unknown (HDRP) | < 1 ms expected |
| Visual sophistication | Simple emissive capsule + soft-disk embers | Layered flame body + glow + rings (cinematic) | Textured flame plume — between the two |

## Recommendation

**Yes, this package is realistically usable, and it's the right next experiment** — small, low-risk, BiRP-compatible, Quest-friendly.

**Smallest-safe follow-up slice:**

1. Instantiate `VFX_Fire` as a child of the existing `Flame` GameObject at localPosition zero.
2. Disable the smoke `ParticleSystem` (the one whose renderer references `A_SmokeAdd`) on the instance — just `gameObject.SetActive(false)` on the child or disable the component. Keeps the prefab unmodified so a future re-import doesn't fight it.
3. Disable the existing capsule-flame `MeshRenderer` (current emissive flame). Keep the GameObject as the parent transform/audio anchor; do not delete it.
4. Optionally also disable the `Embers` ParticleSystem we added in the polish slice — `VFX_Fire`'s flame may already provide enough warm motion. Or keep both and judge in headset.
5. Leave `FireLight`, `FireLightFlicker`, `FireCrackleAudio` untouched. They continue to provide the warm pool of light + crackle.

Estimated diff: a few hundred lines of scene YAML for the instantiated prefab, no code changes (or one tiny editor helper script for repeatability, mirroring the `CampfirePolishSetup` pattern). Quest cost: indistinguishable from the current campfire.

## Better or worse than Piloto for our use case?

**Substantially better.** Direct comparison:

- **Piloto** ships beautiful low-poly campfire *meshes* (logs, pit, etc.) + cinematic FX, all locked behind HDRP-only shaders. The mesh would have been useful but its `Simply Toon.shader` is also HDRP-only. Net: nothing usable in our BiRP project without a render-pipeline migration.
- **WALLCOEUR** ships nothing but FX — but the FX use built-in Mobile shaders that work everywhere. Less to look at than Piloto's full pack, but everything that's there is immediately usable.

For "make the flame feel more alive on Quest without changing the project's pipeline," WALLCOEUR is the right tool. Piloto would only beat it after a multi-day URP/HDRP migration we've explicitly ruled out.

## Risks & caveats

- **Asset Store EULA.** Same as Piloto and Real Stars Skybox — the source must not be committed to this public repo. This evaluation slice adds `Assets/VFXPACK_FIRE_WALLCOEUR/` to `.gitignore` so it's safely local-only.
- **Don't open the demo scene `FireVFX.unity`.** It uses URP-specific post-processing (the `FireScene_Volume.asset` Vignette). Opening it in our BiRP project will log warnings about missing URP components and won't render the way the publisher intended.
- **The demo Volume profile (`FireScene_Volume.asset`) is a URP MonoBehaviour.** Currently silent (Unity stores it as unrecognized) — confirmed clean console after import. If a future Unity upgrade tightens MonoBehaviour deserialization, this *could* start logging. Mitigation: delete the file if it ever does.
- **Texture sizes are bigger than our current procedural setup** (431 KB flame + two ~900 KB smoke vs. 4 KB procedural). Still trivial for Quest 3, but worth noting for a future Quest 1/2 evaluation if that ever matters.
- **Naming is misleading.** `VFX_Fire_Green` is *not* a green flame — it's a single-PS smoke prefab with a different tint. The only orange flame in the pack is inside `VFX_Fire` (and the composites).
- **No iconic "wood logs" mesh** — this is a pure FX pack. If we want the Piloto-style log/pit silhouette, we'd need a different asset (or model it ourselves).

## What NOT to use from this pack

- The demo scene `FireVFX.unity` (URP-dependent).
- The `FireScene_Volume.asset` (URP Vignette).
- The "Green" variants (off-tone for our cozy fire).
- The `VFX_GroundFire_*` and `VFX_TorchLight*` composites (overkill for a single seated campfire).
- The smoke layers in any prefab (per slice guardrail).
- Any of the `*Smoke*.mat` materials in isolation.

## Verdict

The package is realistically usable in this BiRP project. The **smallest-safe next experiment** is a single-prefab swap: instantiate `VFX_Fire`, disable its smoke ParticleSystem, disable our capsule mesh renderer, keep everything else. ~30 minutes of work, no new code, no scene-layout changes, no networking/voice/UX impact, sub-millisecond Quest cost. Worth doing — and worth keeping the polish-slice fallback intact so we can A/B compare in headset.
