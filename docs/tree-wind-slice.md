# Tree wind slice

> Small prototype: subtle whole-tree sway on 5 trees closest to the campfire. Designed to read as "calm forest breathing" — not "trees at a rave". Two small files added, zero shader changes, zero new dependencies.

## Approach chosen

**C — tiny rigid rotation around the tree's base.**

The hierarchy of attempts went:

1. **A. Use existing asset-pack wind support.** ❌ None exists.
   - `tree_01` (Mountain Terrain pack) uses `bark_01` (Standard shader) and `leaves_01` (Legacy/Transparent/Bumped Diffuse). Neither shader has vertex animation, wind parameters, or a sway feature.
   - No Wind-related scripts found in `Mountain Terrain rocks and tree/` or `NatureStarterKit2/`.
   - No SpeedTree integration, no Tree Creator wind.
2. **B. Sway leaf/crown objects only.** ❌ Not possible.
   - `tree_01.prefab` is a single GameObject with one MeshFilter and one MeshRenderer — the trunk + foliage geometry is a single combined mesh. There's no separable "leaf object" to wiggle independently.
3. **C. Subtle rigid rotation on the whole tree.** ✓ This is what shipped.
   - The Transform's pivot is at the trunk base (mesh origin Y=0, mesh extends Y=0→6). Rotating the Transform makes the top move more than the base — i.e., reads as a sway, not a slide.
   - Amplitude is intentionally tiny (0.5° default). At a 6 m tall tree, the top moves ~5 cm peak-to-peak. Subtle on purpose. Past ~2° it starts looking rubbery; the component allows it but the default doesn't.
4. **D. Stop and document why nothing's possible.** Not needed — C works.

## What was added

| File | Purpose |
|---|---|
| `Assets/Scripts/Environment/SubtleTreeWind.cs` | Per-tree MonoBehaviour. One `Mathf.Sin` + one `Quaternion.AngleAxis` + one `transform.localRotation` write per frame. Zero allocations. |
| `Assets/Editor/TreeWindSetup.cs` | Three menu items: `Apply Tree Wind (Prototype, 5 trees)`, `Apply Tree Wind (ALL trees)`, `Remove Tree Wind (all)`. All idempotent + Undo-aware. |

## SubtleTreeWind parameters

All on the component, exposed in the Inspector:

| Field | Default | Notes |
|---|---|---|
| `amplitudeDegrees` | `0.5` | 0.3–1.0 = subtle. Above 2 starts looking like a rubber stick. Tree's perceived sway is ~`amplitudeDegrees × tree-height-in-meters / 60` cm at the top. |
| `speed` | `0.5` (rad/s) | Period ≈ `2π / speed` ≈ 12.6 s at default. 0.4–0.7 reads as breathing. Above ~1.5 starts feeling agitated. |
| `phaseOffset` | `0` (overwritten by `randomizeOnStart`) | Per-instance offset so trees don't sync. |
| `axis` | `Vector3.right` (X) | Forward/back tilt. Set to `Vector3.forward` (Z) for side-to-side instead. Vary per tree for less mechanical look. |
| `randomizeOnStart` | `true` | Scrambles `phaseOffset` to a random radian on Start. |
| `enableWind` | `true` | Runtime toggle. Flip in Inspector to freeze mid-motion without losing state. |

## Objects affected (prototype)

`Apply Tree Wind (Prototype, 5 trees)` sorts every `tree_01*` GameObject by squared distance to (0, 0, 0) and adds the component to the 5 closest. With the current scene (~43 trees in the forest), those are the trees in the inner hex ring + a few of the closer user-placed ones — the most visible from the seated player position.

Specifically (deterministic from current scene state):
- The 5 trees with smallest distance to the campfire centre.
- Each gets a randomized `phaseOffset` on first Play so their motion is desynced.

If the test reads well in headset, `Apply Tree Wind (ALL trees)` extends the same component to every `tree_01*` (43 total).

## How to disable / tune

- **Per-tree disable**: untick `Enable Wind` on the component, or remove the component.
- **All trees off**: run `Tools/Quest Setup/Remove Tree Wind (all)`.
- **More / less sway**: tweak `amplitudeDegrees` in the Inspector. Try 0.3 for "is it even moving?" or 1.0 for "yes there's a breeze".
- **Faster / slower**: tweak `speed`. Default 0.5 = ~12.6 s period.
- **Per-tree variety**: change `axis` on some trees (`Vector3.forward` instead of `Vector3.right`) so neighbours sway in different directions.

## Performance

- **Per-frame cost** per windy tree: `Mathf.Sin` + `Quaternion.AngleAxis` + one `transform.localRotation` write. Sub-microsecond on Quest 3.
- **5 trees**: ~5 µs total per frame. Lost in measurement noise.
- **All 43 trees**: ~40 µs per frame. Still negligible — Quest 3 has a ~13.9 ms budget per frame at 72 fps.
- **Zero allocations** per frame (`Quaternion` is a struct, `Mathf.Sin` returns a float, `_baseRotation` is cached in `Awake`). No GC pressure.
- **No physics, no coroutines, no Update-list management, no events**. The component is one of the simplest MonoBehaviours you can write.

## Quest-specific notes

- No shader changes — `bark_01` (Standard) and `leaves_01` (Legacy/Transparent/Bumped Diffuse) are untouched. No risk of a Quest-incompatible shader path.
- No realtime shadows added or moved — trees still have `shadowCastingMode = Off` (from `ForestSetup.cs`). The rotation doesn't change that.
- No instancing impact. The trees were already not GPU-instanced (Standard shader without "Enable GPU Instancing" set in the material). Adding a per-tree rotation doesn't change anything there either.

## Whether this should later become a shader-based wind solution

**Worth revisiting if any of these become true:**

1. **The whole-tree sway starts reading as "tree rotating around its base" rather than "tree responding to wind"** in headset. The visual cue we're missing is differential motion — trunk barely moves, leaves move a lot. With a single-mesh tree, the only way to get that is vertex animation in the shader (typically Y-weighted, so high vertices sway more than low ones). That's a shader rewrite — at least the leaves material — and would need a Quest-friendly shader (URP Lit + vertex offset, or a custom Built-in vertex shader).
2. **We import or author a tree pack where leaves are a separate mesh.** Then approach B becomes viable — sway just the leaf object, leave the trunk Transform alone. No shader work, just multi-mesh setup. Could swap the `tree_01.prefab` for a low-poly forked-mesh pine and instantly get a more believable sway.
3. **Tree count grows past ~100** and the per-Update transform writes start showing up in the profiler. The Sin-per-tree path is cheap but does scale linearly; a single global wind direction + a shader-level vertex offset is constant cost regardless of tree count. Not a concern at 43 trees, would be worth thinking about at 200+.

For now: subtle rigid sway is the right scope. Two small files, zero dependencies, instantly reversible. No shader risk.

## Validation

- `recompile_scripts` after adding both files: **0 errors, 0 warnings**.
- `Tools/Quest Setup/Apply Tree Wind (Prototype, 5 trees)` ran cleanly:
  `[TreeWindSetup] Added SubtleTreeWind to 5 tree(s) (closest to fire); skipped 0 already-windy tree(s).`
- Console after scene save: **empty** (no errors, no warnings).

## Reversibility

Three independent ways to undo:

1. `Tools/Quest Setup/Remove Tree Wind (all)` — removes the component from every tree.
2. Delete the two files: `SubtleTreeWind.cs` and `TreeWindSetup.cs`. Unity strips the missing components from the scene on next save.
3. `git revert` of this commit.

## Untouched

Networking, voice, menu, XR rig, LAN/Internet logic, tutorial panel, ForestFloor material, mountain backdrops, campfire mesh, flame, embers, FireLight, audio, starfield, stone seats, fire-pit kerbstones, the other ~38 trees that didn't make the closest-5 cut.
