# Stone seats slice

> Tiny visual swap. The cube-box placeholders for `Seat_A` and `Seat_B` are now hidden behind real stone meshes from the Mountain Terrain Rocks and Tree pack. Functional anchors preserved.

## What changed

| GameObject | Before | After |
|---|---|---|
| `Seat_A` | Cube primitive (visible), BoxCollider, position (1.6, 0.2, 0), scale (0.6, 0.4, 0.6) | Same Transform + BoxCollider. `MeshRenderer.enabled = false` (cube hidden). |
| `Seat_B` | Same, mirrored at (-1.6, 0.2, 0) | Same Transform + BoxCollider. `MeshRenderer.enabled = false`. |
| `StoneSeat_A` (new) | — | `rock_set_01` prefab instance at (1.6, 0, 0), scale (0.45, 0.4, 0.45), Y-rotation 15°. World bounds ≈ 1.09 × 0.51 × 1.42 m, seat top ≈ Y=0.5 m. |
| `StoneSeat_B` (new) | — | `rock_set_02` prefab instance at (-1.6, 0, 0), scale (0.45, 0.4, 0.45), Y-rotation -12°. Roughly the same size, deliberately different rock shape for variety. |

## What was preserved

Per the spec — anything that could be a future hook stays intact:

- **Both `Seat_A` and `Seat_B` GameObjects** still exist with their original names.
- **Their `Transform` properties** (position, rotation, scale) are unchanged. A future seat-aware script using `GameObject.Find("Seat_A").transform` still gets the same world coordinates and the same BoxCollider extents.
- **`BoxCollider` on both** is enabled and unchanged. Any physics queries / overlap tests / future spawn-on-seat logic continues to work.
- **Tag (`Untagged`), layer (`Default`), parent (`World`)** all unchanged.
- **No MonoBehaviour scripts** were attached before this slice (verified via `get_gameobject` and a project-wide grep — no script in `Assets/Scripts/` or `Assets/Editor/` references `Seat_A` or `Seat_B`). So there's nothing extra to preserve beyond what's listed above.

## What was hidden

- The two cube `MeshRenderer`s on `Seat_A` and `Seat_B` are disabled. The cubes themselves are still in the scene file but render zero pixels. Re-enabling them in the Inspector is a single click — they return as the previous placeholder boxes.

## Why this composition

- **Different rocks per seat** (`rock_set_01` vs `rock_set_02`). Reuses the existing Mountain Terrain rock prefabs we're already using for the fire-pit kerbstones — no new shaders, no new draw call families, no new material families. The pack ships four variants (01–04); the two fire-pit stones I'd want furthest from the seats are `_03` and `_04` (the existing fire-pit kerb uses one of each at scale 0.4×), so seats picking `_01` and `_02` keeps visual variety across the eight rocks total in the scene without becoming a stone-cataloguing exercise.
- **Slight asymmetric Y-rotation** (15° / -12°) so the stones don't look like a matched factory pair. Weathered look, not arranged.
- **Scale (0.45, 0.4, 0.45)** picked so the stone top sits roughly at Y=0.5 m — the previous cube top was at Y=0.4 m, the new stone top is at Y=0.5 m. Marginally taller, still in plausible seated-hip-height range. World footprint of each stone (1.1 × 1.4 m flat-ish) reads as a slab to sit on rather than a boulder.
- **Long axis (Z, 1.42 m)** stretches from-and-toward the fire. So when a seated player looks down at the stone, they see a long flat-ish surface aimed at where their legs go — feels naturally "seat-like" rather than perched-on-a-pebble.

## Performance / Quest

- **`MeshCollider` disabled on both new stones.** Seat_A and Seat_B already have BoxColliders covering the seat volume; doubling the collider with the stone's MeshCollider would be wasteful and a small physics-query cost. (Spec asked to "prefer simple BoxCollider … if colliders are needed" — keeping the existing BoxCollider, dropping the new MeshCollider, satisfies that.)
- **`shadowCastingMode = Off`** on both stones. Quest shadow budget is already trimmed for the trees + rocks + campfire mesh — these two seat stones don't earn back the cost of a shadow pass.
- **`receiveShadows = false`** on both. They're inside the campfire's FireLight cone — they get warm direct light, no need for shadow detail across their surface from other geometry.
- **Net new objects: 2.** Same prefab family already used elsewhere → likely batched. No new shaders, no new materials beyond what was already in the scene.

## Sightline / seating UX

- Stone tops sit at ~Y=0.5 m. Original cube tops were at Y=0.4 m. A 10 cm change is below the threshold that the `CameraOffset y=1.2` seated-eye-height setup will notice — players still see eye-to-eye across the fire.
- Stones don't extend toward the fire beyond Seat_A/B's BoxCollider footprint (BoxCollider is 0.6 × 0.6 m, stone footprint is ~1.1 × 1.4 m → stone *does* extend a bit further out and toward the fire, but Z=±0.7 still leaves the fire centre at (0,0,0) clear).
- Two stones at X=±1.6 m, fire at (0,0,0): gap between stone edges = 1.6 - 0.7 = 0.9 m on either side of the fire. No physical blocking. Sightline across the fire stays clear.

## Reversibility

Three independent ways to undo:

1. **In the Inspector**: enable `Seat_A.MeshRenderer` and `Seat_B.MeshRenderer`; then disable or delete `StoneSeat_A` and `StoneSeat_B`. Back to cubes.
2. **Git**: revert this commit — restores the scene to the post-forest-floor state.
3. **Re-run a slice helper later**: this slice didn't add a helper script because the change was small enough to be done via mcp-unity directly. If we want to make it idempotent for future re-imports, a 30-line `StoneSeatsSetup.cs` would fit the existing pattern (`CampfireMeshSetup`, `ForestSetup`, `ForestFloorSetup`).

## Untouched

Networking, voice, menu, XR rig, LAN/Internet mode logic, forest floor material, mountain backdrop, trees, fire-pit kerbstones, campfire mesh, flame, embers, audio, starfield, tutorial panel — all left exactly as they were.

## Verification stamps

- `get_gameobject Seat_A` before changes: confirmed no MonoBehaviour scripts attached, only Transform + MeshFilter + MeshRenderer + BoxCollider.
- Project-wide grep for `Seat_A` / `Seat_B` in `Assets/Scripts/` and `Assets/Editor/`: zero hits.
- `get_gameobject StoneSeat_A` after changes: position (1.6, 0, 0), Y-rotation 15°, scale (0.45, 0.4, 0.45), MeshRenderer.castShadows=Off, receiveShadows=false, MeshCollider.enabled=false. ✓
- Save scene: clean (no console errors).
