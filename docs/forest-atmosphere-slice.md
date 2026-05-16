# Forest atmosphere slice

> Small placement pass — gave the campfire scene a sense of enclosure using only assets that already work in BiRP on Quest. No new shaders, no new code in the runtime, no Terrain Engine, no baked lighting.

## Result

```
                          z+
                           |
                  T(0,8)  |
                           |
       T(-6.9,4)           |        T(6.9,4)
                           |
                   r r     |
                  r [fire] r              x+
   Seat_B (-1.6,0) -- (0,0,0) -- Seat_A (1.6,0)
                   r r     |
                  r       r
       T(-6.9,-4)          |        T(6.9,-4)
                           |
                  T(0,-8)  |
                           |
```

- **6 trees** in a hex ring at ~8 m radius. None on the seat-axis (X), so neither seated player has a tree in their line of sight across the fire to the other.
- **4 stones** at ~2 m radius around the campfire mesh, one per quadrant. Scaled 0.4× so they read as kerb-stones, not boulders.
- Campfire mesh, flame, embers, FireLight, crackle audio, starfield, network rig — **all untouched**.

## Assets used

All instantiated from already-imported Asset Store packs (both gitignored — source not committed).

| Object | Prefab | Where it's from | Why |
|---|---|---|---|
| 6× tree (hex ring) | `Assets/Mountain Terrain rocks and tree/Prefab/tree_01.prefab` | Jermesa Studio — Mountain Terrain Rocks and Tree | Only tree prefab in the project that actually renders in Unity 6 (see "What didn't work"). Standard shader, MeshRenderer + MeshCollider, ~2.3 × 6.0 × 2.4 m bounds. Single variant — relying on the dim night lighting to hide repetition. |
| 4× stone ring | `Assets/Mountain Terrain rocks and tree/Prefab/rock_set_01.prefab` … `04.prefab` | Same pack | Four visual variants give natural variation around the fire pit. Standard shader, MeshCollider. Authored at landscape scale (~1.8 × 1.3 × 2.8 m); scaled 0.4× in code to fit a cozy fire pit. |

Nothing else added. No new ground texture, no new ambient audio, no scattered foliage. Tight on purpose — "atmospheric prototype with intention," not a forest dump.

## Approximate counts and footprint

| Metric | Value |
|---|---|
| New GameObjects | **10** |
| New unique prefabs referenced | 5 (`tree_01`, `rock_set_01..04`) |
| Unique shaders introduced | **0** new — both Standard shader (BiRP), already in build |
| Unique materials introduced | 2 from the pack (`leaves_01`, `rock_set_mat`) — referenced from prefabs, no scene-level overrides |
| Total tree polys (very rough) | ~3,000–6,000 per tree × 6 = ~20–35 k visible tris |
| Total rock polys | ~500 per rock × 4 = ~2 k tris |
| Shadow casters added | **0** — all 10 have `shadowCastingMode = Off` (see `ForestSetup.cs`) |
| Realtime lights | unchanged (still one point light + the existing directional) |
| Draw calls added | ~10 (each tree + rock is one MeshRenderer; no batching expected from large-scale meshes) |

## What didn't work — and why we ignored it

NatureStarterKit2 (Shapes publisher) imported with `tree01.prefab` … `tree04.prefab` and `bush01.prefab` … `bush06.prefab`, plus matching bark/branch/bush textures and Nature/Tree Creator materials. **None of the 10 tree/bush prefabs render in Unity 6.** Each carries a `Tree` component with `data: null` — they were authored as **Terrain Engine Tree Prototypes**, meant to be painted onto a `Terrain` via the inspector, not instantiated standalone. The actual mesh + material come from a `TreeData` ScriptableObject we don't have (it lived in the publisher's demo scene's terrain asset, which we removed when we trimmed the pack — and Unity 6's Tree Creator pipeline has been quietly deprecated anyway).

Effect: instantiating those prefabs adds GameObjects with bounds `(0, 0, 0)` and zero visible geometry. Confirmed via `mcp-unity get_gameobject` before any commit.

What we still got out of NatureStarterKit2: the trim cleanup itself (removed legacy post-processing junk + the broken `ColorCorrectionLookupEditor.cs` that didn't compile in Unity 6), and the gitignore entries that keep the pack local-only. The `Nature/`, `Textures/`, `Materials/` subfolders are still on disk in case a future slice uses them via a Terrain.

## Performance notes for Quest

- **Shadow budget** is the main risk on Quest with this many additions. Mitigated by setting `shadowCastingMode = Off` on every tree and rock via the `ForestSetup` editor helper. The campfire scene's directional light + flickering point light still render shadows — they just don't have to compute shadow maps for 10 new occluders.
- **No LOD groups.** Mountain Terrain pack's tree_01 doesn't ship with LODs. At 8 m distance the full mesh renders per frame for every tree. If a future slice imports a tree pack with LODs (or we author a `LODGroup` ourselves), this is a clean win.
- **No GPU instancing on the trees.** Single tree prefab × 6 instances — could be instanced if Standard shader's "Enable GPU Instancing" gets ticked on its material. Worth doing in a follow-up tune pass; would collapse 6 draw calls to 1.
- **No vertex animation / wind.** Static trees in a still scene. Quest-friendly. If we ever want gentle sway, that's per-vertex shader work or animated UVs — separate slice.
- **MeshColliders on trees and rocks** are present (from the prefabs). They have no functional purpose here (no player physics), but cost nothing in render and a tiny amount in scene load. Acceptable; would remove in a clean-up pass.

## Composition decisions

| Design intent | What we did |
|---|---|
| Don't block visibility toward the fire | Trees only at z ≠ 0 (none on the seat-line). NE / NW / SE / SW trees at (±6.9, 4) and (±6.9, −4) — outside any plausible head-rotation sightline between seats. |
| Don't block seated players | Seats are at x = ±1.6, z = 0. Closest tree is 4 m away (z = ±4). Closest stone is 1.4 m from the seat — under the seat's line-of-sight on the inside of the ring. |
| Sense of enclosure | Hex ring at 8 m radius. Inside the 20 × 20 m ground plane (room to spare to the wall). N and S trees frame the long view across the fire; NE/NW/SE/SW trees fill the periphery. |
| Slightly darker outer perimeter | The trees alone — at 6 m tall, with their canopies absorbing FireLight that bounces past 5 m — create the darker outer band naturally. No new material was needed. |
| Keep center warm | FireLight (warm flicker) is unchanged at intensity ~4, range 9 m. All 10 new objects are within or just outside that range, so they all catch warm light gradually falling off into darkness. |

## Vibe estimate

**Cozy prototype.** Not "Unity asset store explosion." 10 visible objects across a 20 × 20 m area is sparse on purpose. The composition reads as "small clearing in a forest" rather than "dense woodland."

Risks I can name from the seat (cannot verify without headset):

1. **6 identical tree models could read as cloned** in bright editor preview. In Quest's dim night lighting + FireLight rolloff past 8 m, the tree silhouettes should blur into dark shapes and the repetition should hide. If it doesn't, a future slice can scale + Y-rotate each tree differently. (Not done in this slice because: (a) `set_transform` is more mcp-unity calls than I wanted to chain, and (b) it's the kind of nudge that's faster to do in the Inspector once we see it in headset.)
2. **Beech leaves on tree_01 read as "summer day forest"** more than the "cozy pine night" the original spec asked for. The pack didn't ship pine. Acceptable for this slice; future imports could swap.
3. **No ground variation.** Ground is still our flat plane with the dark base material. Could darken further or scatter a few rocks/twigs as a follow-up.

## Suggested future improvements

If this composition feels too sparse or too uniform in headset, in priority order:

1. **Vary each tree's Y-rotation and scale slightly** (e.g. rotation ∈ [0°, 360°), scale ∈ [0.85, 1.10]). 30-second Inspector pass or a small extension to `ForestSetup.cs`.
2. **Enable GPU instancing** on `leaves_01.mat` and `bark_*.mat` materials. Drops draw calls.
3. **Scatter 3–5 small rocks on the perimeter** (rock_set_01..04 again, scale 0.2–0.6). Breaks the empty mid-ring.
4. **Replace ground material with `NatureStarterKit2/Textures/ground01.tga`** assigned to a Standard shader material, tiled 4× across the 20 × 20 m plane. Existing material is plain dark — a forest-floor diffuse would lift "we're outdoors."
5. **A second ambient audio layer**: wind through trees or distant owl loop (CC0 import — out of scope for this slice).
6. **If trees ever feel "ribbon-flat" in headset** (the canopies are billboarded planes typical of Tree Creator era), import a low-poly stylized pine pack (Polyhaven CC0 forest or similar) and let those replace tree_01.
7. **Eventually**: a small Unity Terrain underneath the ground plane, painted with the NatureStarterKit2 tree/bush Tree Prototypes. This is the *correct* way to get those prefabs working — but it's a multi-day scope and undoes the "atmospheric prototype" framing.

## Reversibility

Everything in this slice is reversible without code changes:

- Disable any of the 10 new GameObjects in the Inspector → it's gone.
- Delete the 10 GameObjects → restores the pre-slice scene.
- Delete `Assets/Editor/ForestSetup.cs` → removes the menu (other scene state untouched).

The `.gitignore` additions for NatureStarterKit2 and Mountain Terrain are also non-destructive: removing the lines makes those folders trackable again if the project decides to commit them later (subject to Asset Store EULA — which says don't).
