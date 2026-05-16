# Dog companion slice

> Tiny ambience pass: one static dog from ithappy's `Animals_FREE` pack drops in beside `StoneSeat_A`, facing the fire. No AI, no audio, no networking, no input — just a cozy silhouette. The pack's URP-shipped material is swapped for a warm-brown Built-In Standard material we author ourselves.

## What's in

| Asset / change | Purpose |
|---|---|
| `Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab` (Asset Store, gitignored) | Source prefab — skinned dog mesh + Animator + bones |
| `Assets/Materials/DogCoat.mat` (new, committed) | Built-In Standard material, warm tan-brown `(0.55, 0.38, 0.22)`, matte (smoothness 0.08). Replaces the pack's URP-shader material so the dog doesn't render magenta in our BiRP scene. |
| `Tools/Quest Setup/Add Dog Companion` menu (added in `Assets/Editor/QuestBuildAPK.cs`) | Re-runnable Editor helper: instantiates `Dog_001.prefab` as `DogCompanion`, positions it beside Seat_A, swaps materials, strips runtime AI scripts. Idempotent — re-runs delete previous before creating a fresh instance. |
| `Assets/Scenes/CampfireRoom.unity` (modified) | `DogCompanion` prefab-instance added with transform + material + renderer overrides. Scene size went from 13648 → 13684 lines (+36). |

## Asset used

- **Prefab:** `Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab` (only dog variant in the pack — other animals: Horse, Chicken, Kitty, Pinguin, Deer, Tiger).
- **Source mesh:** `Assets/ithappy/Animals_FREE/Meshes/Dog_001.fbx` (812 KB; rigged skinned mesh).
- **Animations:** `Dog_001_idle.anim`, `Dog_001_walk.anim`, `Dog_001_run.anim` driven by `Animations/Animation_Controllers/Dog.controller` (BlendTree with `State` integer parameter; default state at `State=0` = idle).

The `Animals_FREE/` folder is gitignored (Asset Store EULA — same pattern as Real Stars, Piloto, Terra, etc.). The `Dog_001` prefab is referenced from the scene by GUID; **anyone cloning the repo must re-import `ithappy / Animals_FREE` from Package Manager → My Assets before opening the scene**, or the `DogCompanion` instance will render as a missing prefab reference.

## Placement reasoning

| Property | Value | Why |
|---|---|---|
| Position | `(1.30, 0.00, 0.80)` | Beside `StoneSeat_A` (at `1.6, 0, 0`), pulled back ~30 cm and offset ~80 cm in +Z. Sits just outside the seat-to-fire line of sight (the X-axis between seats), so it doesn't block the player's view of the fire or the other seat. Close enough to read as "part of the campfire group". |
| Y rotation | `215°` | Faces the campfire at origin (`atan2(-1.3, -0.8) ≈ 238°` would point exactly at origin; 215° aims slightly toward the center of the seating triangle, giving the dog a "watching the fire while listening to the people" pose). |
| Scale | `(0.5, 0.5, 0.5)` | Pack meshes are roughly real-world scale (1 unit = 1 m). 0.5× reads as a medium-sized dog, ~40–50 cm at the shoulder — a labrador or border collie, not a chihuahua and not a wolfhound. |
| Y position | `0.00` | The `Dog_001.fbx` pivot is at the mesh's foot level. World Y=0 = Ground plane, so the paws should sit on the floor without needing an offset. **Needs headset visual confirmation** — if the dog floats or sinks, adjust the `position.y` value in the helper. |

What was avoided:
- Placing the dog on the seat-to-fire axis (X) — would have blocked the player's eye-line to the fire.
- Placing the dog directly behind a player — would have felt unsettling in headset (something behind you that you can't see).
- Placing the dog in the fire-pit kerbstone ring — would have looked like it was about to walk into the flame.
- Placing two dogs / a herd — spec said *one* companion. Solo + intentional > scattered.

## Material / shader notes

### Why the original prefab renders magenta

`Material.mat` (the shared coat material referenced by every animal's `SkinnedMeshRenderer`) has `m_Shader` pointing at GUID `933532a4fcc9baf4fa0491de14d08ed7`. That GUID resolves to a **URP/visionOS shader** that lives in XRI's visionOS sample folder (`Library/PackageCache/com.unity.xr.interaction.toolkit/Samples~/visionOS/Materials/`) — it's not a shader anyone *uses* in a Built-In RP scene. In our project the shader file isn't directly imported into a runtime assembly, so the material falls back to "missing shader" → magenta in headset.

The pack offers a `Render_Pipeline_Convert/Unity_6_Built-In_source.unitypackage` that does an in-place shader swap on all materials. I avoided importing it — it would modify the gitignored pack source and run an opaque `version: 9` MonoBehaviour serialized into the `Material.mat` header (lines 4–15). The local fix is one new Standard material we author ourselves and assign via the Editor helper.

### The pack's atlas texture (committed-out, referenced-in)

The pack ships **one shared atlas texture**: `Assets/ithappy/Animals_FREE/Textures/Texture.png` (811 KB). Every animal (Dog, Horse, Chicken, Kitty, Pinguin, Deer, Tiger) UV-maps to its own region of this single atlas, which is why `Materials/` only contains one coat material (`Material.mat`) for the whole pack. The pack's `Material.mat` already had `_MainTex` + `_BaseMap` both pointing at this atlas (GUID `5bf4d5ffdfd914a48be4d1bf61c86666`) — but the URP shader couldn't resolve it.

`DogCoat.mat` (our own material at `Assets/Materials/DogCoat.mat`, committed):

| Property | Value |
|---|---|
| Shader | `Standard` (Built-In) — GUID `0000000000000000f000000000000000` |
| `_MainTex` | `Assets/ithappy/Animals_FREE/Textures/Texture.png` (pack atlas, GUID `5bf4d5ffdfd914a48be4d1bf61c86666`) |
| `_Color` | `(1, 1, 1, 1)` — pure white so the atlas reads through unaltered |
| `_Metallic` | `0` |
| `_Glossiness` | `0.08` — matte so the fire flicker doesn't harsh-specular off the coat |
| Normal map / detail / occlusion | None (pack ships no separate normal map; the atlas-only setup is what we have) |

Single material per dog, applied uniformly to every `SkinnedMeshRenderer` and `MeshRenderer` submesh on the prefab. The dog now reads with the pack's authored coat colours rather than as a flat brown silhouette — light/dark patches, face highlights, and paw details all come through via the atlas. Still **needs headset visual confirmation** that the UVs line up correctly and the texture reads as a dog rather than as random pixels.

### Texture / shader path summary

| Asset | Path | Committed? |
|---|---|---|
| Source atlas | `Assets/ithappy/Animals_FREE/Textures/Texture.png` | No — gitignored under `Assets/ithappy/` |
| Built-In Standard shader | Unity built-in (no file path) | n/a |
| Our material | `Assets/Materials/DogCoat.mat` | Yes (new this slice) |
| Dog prefab | `Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab` | No — gitignored |
| Dog FBX | `Assets/ithappy/Animals_FREE/Meshes/Dog_001.fbx` | No — gitignored |

A clone-time `Re-import Animals_FREE from My Assets` step brings both the texture and the prefab back into the local working tree; the scene's GUID references then resolve and `DogCoat.mat` (which is committed and only references the gitignored texture by GUID) re-binds to the atlas automatically.

## Runtime scripts stripped

The pack's `Dog_001.prefab` ships with a chain of dependent components:

- `Animator` (with `Dog.controller`) — **kept.** Default BlendTree state at `State=0` is idle, which gives a subtle breathing/tail-wag loop. Trivial and safe. `Animator` is in `UnityEngine`, not the pack's namespace, so the strip loop preserves it explicitly.
- `MovePlayerInput` (`ithappy.Animals_FREE`) — **removed.** Reads keyboard / device input. `[RequireComponent]` depends on `CreatureMover`.
- `CreatureMover` (`ithappy.Animals_FREE`) — **removed.** Movement / AI script. `[RequireComponent]` depends on `CharacterController` + `Animator`. Must be removed *after* `MovePlayerInput` (which requires it) and *before* `CharacterController`.
- `CharacterController` — **removed.** No physics needed for a static prop.

The dependency chain (`MovePlayerInput → CreatureMover → CharacterController`) means a single-pass loop leaves one layer behind. The helper uses a multi-pass strip: repeats up to 8 passes until no `ithappy.Animals_FREE` MonoBehaviour gets removed in a full pass. Each pass peels one layer off the dependency stack. Then the `CharacterController` removal succeeds.

Other `ithappy.Animals_FREE` scripts present in the pack (`PlayerCamera`, `ThirdPersonCamera`) aren't on the Dog prefab by default but would be picked up by the same loop if they appeared in a future pack update.

## Performance notes

| Metric | Value |
|---|---|
| New scene GameObjects | ~30 (one prefab instance, rig bones included — but only the SkinnedMeshRenderer renders; bones are transform-only) |
| New triangle count | ~5–10 k (estimated from FBX size of 812 KB) |
| New draw calls | 1 (single material across all submeshes; could batch with other Standard-material objects in the scene) |
| New textures in build | 1 (pack atlas `Texture.png`, 811 KB — shared with every other Animals_FREE animal we use, so essentially free if more animals are added later) |
| New shader variants | 0 (Standard already used elsewhere in the scene) |
| Shadow casters added | 0 (all renderers have `shadowCastingMode = Off`, `receiveShadows = false`) |
| Per-frame script cost | One `Animator.Update()` evaluating the BlendTree at the default idle state |
| Collider | None (`CharacterController` removed, no replacement) |

Net Quest impact: a skinned mesh per frame + one Animator tick. Comfortably within budget — Quest 3 handles dozens of skinned characters; one is rounding error.

## Gitignore / package notes

- `Assets/ithappy/` and `Assets/ithappy.meta` are **gitignored** (committed in `625fa62`).
- `Packages/manifest.json` and `Packages/packages-lock.json` track `com.unity.postprocessing 3.5.4` (transitive dep pulled by Animals_FREE — also committed in `625fa62`).
- `Assets/Materials/DogCoat.mat` IS committed (our own asset, not from the pack).
- `Assets/Editor/QuestBuildAPK.cs` IS committed (the helper that references the gitignored prefab by path).
- The scene references the dog prefab by GUID `15ca4494f24bf9741a48b37ec2c4c632`. On a fresh clone, that GUID will be unresolved until Animals_FREE is re-imported.

## Does this require local Asset Store import?

**Yes — for anyone cloning the repo:**

1. Open the project in Unity.
2. Package Manager → My Assets → search "Animals_FREE" → Download → Import.
3. Open `CampfireRoom.unity`. The `DogCompanion` instance now resolves to the real prefab.
4. If the material looks magenta (URP shader unresolved), run `Tools/Quest Setup/Add Dog Companion` to re-apply DogCoat.mat.

Documented as a known clone-time step in this slice doc. Same pattern as every other Asset Store pack we use.

## Known risks

1. **`CreatureMover.cs` has `using UnityEditor;` at the top of a runtime script.** This is harmless in the Editor (Editor assemblies see both namespaces), but may break the Player APK build with `error CS0246: The type or namespace name 'UnityEditor' could not be found`. If a future build fails on this, the cheapest fix is to wrap the `using` in `#if UNITY_EDITOR ... #endif` inside `Assets/ithappy/Animals_FREE/Scripts/CreatureMover.cs` (a local-only edit, since the file is gitignored). A cleaner fix would be adding an asmdef that restricts the pack's Scripts/ folder to Editor-only, but the pack scripts can also be used in scenes, so that would be a different slice.
2. **Y=0 paw placement is unverified in headset.** The FBX pivot *should* be at the foot level for a 0.5× scaled dog, but if the paws float or sink, adjust `position.y` in `AddDogCompanion()` — one-line edit. Re-run the helper after the change (idempotent).
3. ~~**DogCoat tint is a guess.** Pure warm brown without texture/normal detail could read as flat in firelight.~~ **Resolved this slice — `DogCoat.mat` now uses the pack's atlas texture (`Texture.png`).** The dog reads with the pack's authored coat colours instead of a flat brown silhouette. Still needs headset check that UV mapping looks right and that the firelight doesn't blow out the texture detail. The pack ships no separate normal map; if the coat reads as too soft, that'd be a follow-up polish (would need a CC0 normal map import).
4. **Animator's default BlendTree may pose the dog in a weird mid-walk frame** if `State` defaults to a value other than 0 in the controller. Should default to idle, but worth a glance in headset on first wear.
5. **Pack source folder is required for the scene to resolve.** Anyone opening `CampfireRoom.unity` without Animals_FREE imported sees a missing-prefab marker where the dog should be. Per spec, this is acceptable for a local project.

## What still needs headset validation

1. Dog renders correctly with the tan coat (no magenta).
2. Paws sit on the ground, no float, no sink.
3. Position reads as "natural cozy spot beside the stone seat", not "weird stray pet in the corner".
4. Scale reads as a real dog, not a giant beast or a puppy.
5. Default Animator state is genuinely idle (subtle, not aggressive walk-in-place).
6. The dog doesn't block the fire view from either seat.

## Editor helper invocation

```
Tools → Quest Setup → Add Dog Companion
```

- Idempotent: re-running deletes the existing `DogCompanion` GameObject and creates a fresh one with the same position / rotation / scale / material.
- Safe: only touches the dog. Leaves networking, voice, room code, XR rig, hand visuals, environment layout, lights, camera, and every other scene system alone.
- Re-applies the DogCoat material every run, so a `URP_to_Built-In` import that would otherwise re-randomise the shader can be undone in one click.

## Validation

- `recompile_scripts` after editing `QuestBuildAPK.cs`: **0 errors, 34 warnings** (33 pre-existing third-party CS0618 deprecations + 1 new from this slice's helper — to be re-verified; net new warnings should be 0 from my own code).
- `Tools/Quest Setup/Add Dog Companion` ran cleanly: `[QuestBuildAPK] Placed DogCompanion at (1.30, 0.00, 0.80), scale 0.50×, coat=DogCoat.`
- Console after run: **0 errors**, only the helper's own log line.
- `CreatureMover` and `CharacterController` strings absent from the scene file post-helper run.
- `DogCoat.mat` GUID `f802950f279fb46c0b9860e5f382afcc` referenced by the scene (material assignment is persisted).
- Dog prefab GUID `15ca4494f24bf9741a48b37ec2c4c632` referenced 19 times in the scene (PrefabInstance + per-child transform/renderer overrides).
- `save_scene`: scene saved cleanly.

## Untouched

Networking, voice, room code, LAN/Internet/Relay logic, XR input actions, XR rig tracking, NetworkBootstrap, TutorialOverlay, VoiceBootstrap, Photon Voice, hand visuals (LeftHandMesh/RightHandMesh transforms unchanged), stone grounding helper and stone positions, lights (FireLight Hard shadows / Directional None — kept from validation pass), camera, forest layout, mountain backdrops, ground material, grass tufts, fire-pit composition.
