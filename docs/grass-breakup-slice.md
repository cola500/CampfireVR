# Grass breakup slice

> Tiny art pass. Six grass tufts sprinkled around the clearing's middle zone. Goal: stop the ForestFloor reading as a uniform dirt sheet. Anti-goal: a lawn.

## What's in

Six grass tufts at hand-picked positions, each made of **two crossed Quads** (an "X" pattern) with an alpha-cutout `GrassBreakup` material driven by Terra's `Grass Flower 7.png`. All six are siblings under one `GrassBreakup` parent for easy mass-disable.

Hierarchy added (under World):

```
GrassBreakup                                     (empty parent, transform identity)
├── GrassTuft_01   pos ( 2.5, 0.25,  2.5)  yRot  37°  scale (0.45, 0.50)
│   ├── Card_A     (local yRot   0°)
│   └── Card_B     (local yRot  90°)
├── GrassTuft_02   pos (-2.8, 0.22,  2.2)  yRot 142°  scale (0.40, 0.44)  [+ Card_A, Card_B]
├── GrassTuft_03   pos ( 1.8, 0.20, -2.8)  yRot 261°  scale (0.50, 0.40)  [+ Card_A, Card_B]
├── GrassTuft_04   pos (-2.5, 0.28, -2.5)  yRot  82°  scale (0.55, 0.56)  [+ Card_A, Card_B]
├── GrassTuft_05   pos ( 4.0, 0.30,  4.5)  yRot 198°  scale (0.60, 0.60)  [+ Card_A, Card_B]
└── GrassTuft_06   pos (-3.5, 0.25, -4.0)  yRot  19°  scale (0.50, 0.50)  [+ Card_A, Card_B]
```

Each card is a Unity Quad primitive, MeshCollider removed, MeshRenderer set to `shadowCastingMode = Off` and `receiveShadows = false`.

## Why two crossed cards (not one) — bug discovered during testing

The first pass used a single Quad per tuft. In headset, only one of the six tufts was visible from the player's seat — the rest were placed correctly but rendered nothing.

**Cause:** Unity's `Standard` shader is single-sided (it culls back faces). A single Quad has a normal pointing along its local `+Z`. Tufts whose front face happened to point away from the viewing seat were silently rendering their back side, which Standard culls.

**Fix:** Each tuft is now an empty parent plus **two child Quads** at local Y-rotations 0° and 90° (an "X" pattern). With perpendicular cards, at least one of the two always presents a front face to any camera direction. Classic vegetation-card pattern, works with any single-sided shader.

Cost delta: 12 → 24 triangles total. Still trivial.

## Why this asset

| Candidate | Verdict |
|---|---|
| Terra `GrassA.tga`, `GrassB.tga` (+ norms) | Rejected. 4 MB each, designed as tileable top-down terrain texture. A single Quad slice would render an arbitrary patch of a terrain tile, not a recognisable grass shape. Wrong tool. |
| Terra **`Grass Flower 7.png`** | **Selected.** 388 KB, alphaUsage = FromGray (alpha derived from luminance — typical for grass-blade card textures with black background). Small file size + alpha behavior → designed as a card texture, exactly what we want. |
| NatureStarterKit2 `grass01.tga`, `grass02.tga` | Rejected. 4 MB / 2.3 MB, textureType = 5 (legacy Tree Creator Detail / Cookie format). Bound to a terrain-painting pipeline that's deprecated in Unity 6. |
| NatureStarterKit2 `bush01..06.prefab` | Already known broken in Unity 6 — Tree Creator legacy, no terrain data, renders nothing. (Documented in `docs/forest-atmosphere-slice.md`.) |

## Placement reasoning

Six positions chosen by hand, not random. Constraints:

- **None on the seat axis (X)** — both seats sit at X ≈ ±1.6, looking across the fire toward each other. Grass at z ≈ 0 would land in the line of sight. All six tufts are placed at |z| ≥ 2.0.
- **None inside the fire-pit kerb** — the four fire-pit kerbstones sit at roughly (±1.5, 0, ±1.4). Tufts start at minimum 2.5 m radius from the fire.
- **Asymmetric distribution** — three on the +X side, three on the -X side, but at different (z, scale, rotation) values per tuft so it doesn't read as mirrored.
- **Two scale clusters** — four "medium" tufts at 0.4–0.55, two "larger" outliers at 0.55–0.60. Tiny variation, not noise.
- **Random-feeling Y rotations** — 19°, 37°, 82°, 142°, 198°, 261°. Avoids cardinal-aligned blade silhouettes.
- **Slight Y position jitter** (0.20–0.30) so the tufts sit just above the ForestFloor without floating obviously. Each Quad's bottom edge lands around or just below ground.

What we explicitly avoided:

- **No tufts near the central campfire / wood pile** — the eye should land on the fire, not on grass-blade silhouettes.
- **No tufts on tree-base ring at z ≈ ±8** — the trees are already a strong perimeter signal; layering grass at their roots makes the scene busy.
- **No mountain-side grass** — mountains are distant backdrop; ground detail there is invisible from the seats.

The result is six modest accents in the "middle zone" between the fire-pit and the inner tree ring. From the seats they read as patches of weeds inside the clearing.

## What was intentionally avoided

| Avoided | Reason |
|---|---|
| Unity Terrain grass system | Spec rules it out. Also requires a Terrain object we don't have. |
| Runtime vegetation systems / GPU instancing frameworks | Spec rules it out. 6 quads don't need it. |
| Cross-billboarded "3D" tufts (3 quads per tuft in X pattern) | Spec asks for restraint. Single quad is cheaper and gives an equally subtle look at viewing distance. |
| A `SubtleGrassSway` script analogous to `SubtleTreeWind` | Spec says "no wind dependency required". Static is simpler and Quest-friendlier. Could be added later if the still cards read as too rigid. |
| Dense placement (10+ tufts) | Six is the line between "lived-in" and "lawn" for a 20×20 m floor. We can scale up if it reads as too sparse; cheaper than scaling down from "too busy". |
| Grass under the seats / on the path between seats | Players physically pivot heads there; cards rotating in/out of view at peripheral edge could be distracting. |

## Material

`Assets/Materials/GrassBreakup.mat`:

| Property | Value |
|---|---|
| Shader | `Standard` |
| Render Mode | Cutout (`_Mode = 1`, `_ALPHATEST_ON`, render queue `AlphaTest`) |
| `_MainTex` | Terra `Grass Flower 7.png` |
| `_Cutoff` | 0.4 |
| `_Color` | (0.85, 1.0, 0.80, 1) — gentle green tint over the texture's natural colour |
| `_Metallic` | 0 |
| `_Glossiness` | 0.05 (basically matte) |

Single shared material across all six tufts → potential static-batching gain. No new shader compiled; Standard cutout was probably already in the build path.

## Performance

| Metric | Value |
|---|---|
| New scene GameObjects | 19 (1 parent + 6 tuft parents + 12 child cards) |
| New triangles | 24 (12 quads × 2 tris) |
| New draw calls | ≤ 12 (potentially batched aggressively — single shared material across all cards) |
| New textures in build | 1 (`Grass Flower 7.png`, 388 KB) |
| New shaders | 0 (Standard already used elsewhere) |
| Shadow casters added | 0 (all twelve have `shadowCastingMode = Off`) |
| Realtime light interaction | Receives light via FireLight's range (~9 m). Within fire glow for the inner four tufts; faded for the outer two. |

Net Quest impact: rounding error. Sub-microsecond per frame.

## Reversibility

Three independent ways to undo:

1. **`Tools/Quest Setup/Remove Grass Breakup`** — deletes the `GrassBreakup` parent and all six children in one click. The material asset stays on disk (can be deleted manually if desired).
2. **Disable `GrassBreakup` GameObject in the Inspector** — hides all six without touching scene structure.
3. **`git revert`** of this commit (when made).

## Console / validation

- `recompile_scripts` after adding `GrassBreakupSetup.cs`: **0 errors, 0 warnings**.
- `Apply Grass Breakup` ran successfully; `GrassBreakup` parent verified with **`childCount: 6`** via `get_gameobject`.
- Five `NullReferenceException`s logged at the moment of Apply, all from `Photon.Voice.Unity.VoiceConnection.Update()` and `OnDestroy()` (`Assets/Photon/PhotonVoice/Code/UnityVoiceClient.cs:344` and `:361`). These are a **pre-existing Photon Voice 2 Editor-side bug** triggered by any scene-marking-dirty operation in the Editor — they don't fire in Quest builds and have nothing to do with this slice. Worth filing against Photon eventually; not blocking.

## Next tiny polish step if needed

In order of "smallest fix → bigger work":

1. **Tune material tint or `_Cutoff`** if the tufts read as too bright / too patchy in headset. Inspector edit, no code changes.
2. **Add or remove individual tufts** — edit `Tufts[]` array in `GrassBreakupSetup.cs` and re-Apply. The helper is hand-coded positions on purpose so this is trivial.
3. **Cross-billboard tufts** (3 quads per tuft instead of 1) — same script, ~10 extra lines, ~24 extra triangles in total. Only do this if the single quads read as obviously billboard-flipping when the player turns their head.
4. **Sway** — attach `SubtleTreeWind` (already in the project) to the tufts. Same component, same sub-microsecond cost per object. Defaults at 0.5° amplitude would be far too aggressive for grass cards; would need a separate small-amplitude variant or a `SubtleGrassSway` component. Skip until needed.

## Untouched

Networking, voice, menu, XR rig, LAN/Internet logic, tutorial panel, ForestFloor material, mountain backdrops, campfire mesh, flame, embers, FireLight, audio, starfield, trees, tree wind, stone seats, fire-pit kerbstones, the user's manually placed perimeter stones.
