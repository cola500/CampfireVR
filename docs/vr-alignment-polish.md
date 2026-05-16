# VR alignment polish — hand orientation + stone grounding

> Tiny visual polish slice after the first in-headset test of CampfireVR. Two issues observed in VR; both fixed with small transform / scene-data changes and a re-runnable Editor helper. No networking, input, tracking, XR-rig, or hand-asset changes.

## Issues observed in headset

1. **Hand / controller visuals pointed the wrong way.** The combined UniversalController mesh (from XRI Starter Assets, assigned to `LeftHandMesh` / `RightHandMesh` during the hand-visuals slice) rendered with its forward axis aligned to the XR pointer pose. That made the controller body appear to "point straight forward" from the user's hands, when a held Quest Touch controller normally angles down ~40–60° from the pointer ray (the grip-pose vs pointer-pose offset).
2. **Stone seats and rocks sat on top of the ground.** The Mountain Terrain `rock_set` FBX has its pivot at the mesh bottom, so every instance placed at world Y=0 lined its base up *exactly* with the Ground plane — reading as "props placed on top" rather than "stones embedded in the forest floor". This affected the 2 stone seats, 4 fire-pit kerbstones, and 11 perimeter rocks (17 stones total).

## What changed

### 1. Hand visuals — local rotation offset

| Object | Field | Before | After |
|---|---|---|---|
| `LeftHandMesh` | `localEulerAngles` | `(0, 0, 0)` | `(45, 0, 0)` |
| `RightHandMesh` | `localEulerAngles` | `(0, 0, 0)` | `(45, 0, 0)` |

`localScale` (0.9, 0.9, 0.9), `localPosition` (0, 0, 0), MeshFilter, MeshRenderer, BoxCollider, and the GameObject's parent (`LeftHandAnchor` / `RightHandAnchor`) are all untouched. The anchor itself — which `XRHeadTracker` writes the controller pose to — is not rotated; only the visual child mesh is.

`XRControllerInputFeedback.visualTarget` is still resolved via `transform.GetChild(0)` on the anchor at OnEnable, so the trigger-press scale lerp continues to operate on the same `LeftHandMesh` / `RightHandMesh` GameObject. The rotation change does not interact with the scale animation.

The `+45°` X pitch is an initial empirical value matching the rough Quest Touch grip-to-pointer offset (typically 40–60°). It tips the mesh forward so the controller body angles down from the tracked pointer direction, looking more like a real held Touch controller. It will likely need a tuning pass after a second headset session — see "What still needs headset validation".

### 2. Stone grounding — varied deterministic Y embed

A new Editor helper, `[MenuItem("Tools/Quest Setup/Ground Stones")]` (added in `Assets/Editor/QuestBuildAPK.cs`), walks the active scene's root GameObjects, picks those whose name starts with `rock_set_` or `StoneSeat_`, and sets each one's world Y to a target value derived deterministically from the GameObject name.

The target embed depth varies by stone category so the row of rocks doesn't read as "extruded with a single tool":

| Stone group | Embed range | Notes |
|---|---|---|
| `StoneSeat_*` (seats) | **0.05–0.08 m** | shallower so the top stays ≈ 0.42 m above ground at the default 0.4 scale — still tall enough to sit on |
| `rock_set_*` (kerb + perimeter) | **0.06–0.12 m** | wider range, lets decorative rocks vary visually |

Depth per stone = `lerp(min, max, fraction)` where `fraction` is a custom stable hash of the GameObject's name mapped into `[0, 1)`. The hash is rolled by hand rather than using `string.GetHashCode()` so the same name produces the same depth across machines and .NET runtime versions — a stone keeps the same embed across runs.

**Re-runnable / idempotent.** The target Y is a pure function of the name, so a second run produces the same Y. Stones already within 5 mm of their target Y are skipped.

Result of running the helper once on the varied-depth pass:

```
[QuestBuildAPK] Grounded 15 stone(s) with varied embed
  (seats 0.05–0.08 m, rocks 0.06–0.12 m); skipped 2 already at target.
```

(15 of 17 stones moved; the 2 skipped happened to land within 5 mm of their varied target after the previous uniform 0.08 m pass.)

Sampled positions after the varied pass:

| Object | Y target | Notes |
|---|---|---|
| `StoneSeat_A` | -0.070 | within seat range |
| `StoneSeat_B` | -0.070 | within seat range |
| `rock_set_01` (kerb) | -0.102 | within rock range |
| `rock_set_02` (perimeter) | -0.102 | within rock range |

The 17 stones cover:

| Group | Count | Naming |
|---|---|---|
| Stone seats | 2 | `StoneSeat_A`, `StoneSeat_B` |
| Fire-pit kerbstones | 4 | `rock_set_01..04` (the smaller scaled ring around the wood pile) |
| Perimeter rocks | 11 | other `rock_set_*` placed across the clearing |

Numbers after the embed:

| | `Seat_A` (functional root, MeshRenderer disabled) | `StoneSeat_A` (visual) |
|---|---|---|
| Position Y | `0.20` (unchanged — scripts may reference it) | `-0.08` (was 0) |
| Bounds center Y | n/a (disabled) | `0.167` (was 0.247) — bottom now ≈ -0.085, top ≈ 0.42 |

Stone seats stay tall enough to sit on at the default `0.4` height scale (top ≈ 42 cm above ground, just under a standard chair seat).

The functional `Seat_A` / `Seat_B` roots are **not** moved — only the visual `StoneSeat_A` / `StoneSeat_B` are. Anything that references the named seat anchors (FaceTarget, EyeHeightMarker, future seated-pose scripts) keeps the same coordinate as before.

## Exact objects changed

| Object | Change |
|---|---|
| `LeftHandMesh` (child of `LeftHandAnchor` under `VRRig/CameraOffset`) | `localEulerAngles.x: 0 → 45` |
| `RightHandMesh` (child of `RightHandAnchor`) | `localEulerAngles.x: 0 → 45` |
| `StoneSeat_A` (root) | `position.y: 0 → -0.070` (varied within 0.05–0.08) |
| `StoneSeat_B` (root) | `position.y: 0 → -0.070` (varied within 0.05–0.08) |
| `rock_set_01..04` (roots, fire-pit kerb) | `position.y: 0 → varied within -0.06 to -0.12` |
| 11 perimeter `rock_set_*` (roots) | `position.y: 0 → varied within -0.06 to -0.12` |
| `Assets/Editor/QuestBuildAPK.cs` | Added `GroundStones()` method + `[MenuItem("Tools/Quest Setup/Ground Stones")]` |
| `Assets/Scenes/CampfireRoom.unity` | Saved with the above transform changes |

Nothing else touched. Networking, voice, room code, LAN/Internet/Relay logic, XR input actions, XR rig tracking, NetworkBootstrap, TutorialOverlay, Photon Voice, hand asset source FBX, forest layout (beyond stone Y), grass tufts, trees, mountains, ForestFloor, lights, camera — all untouched.

## Risks

1. **Hand rotation may be wrong direction or wrong magnitude.** Without headset verification, `+45°` X-pitch is an educated guess based on typical Oculus Touch grip-pose vs pointer-pose offsets. The fix may need to be a different angle (most likely in the 30–70° range) or even a different axis (Y or Z) if the FBX's local forward axis doesn't match assumption. Tuning is a single field edit per hand.
2. **Hand mesh handedness.** UniversalController.fbx is a single shared mesh used for both hands. If the model is asymmetric in a way that reads "right-handed" or "left-handed", the wrong hand may look mirrored. Fix is `localScale.x = -0.9` on one side (mirroring via negative scale).
3. **Per-category embed ranges may need tuning.** Seats sit at 0.05–0.08 m and rocks at 0.06–0.12 m by deterministic name-hash. If the range still reads as "too uniform" or "too varied" in headset, the four range constants in `QuestBuildAPK.cs` (`SeatMinEmbed` / `SeatMaxEmbed` / `RockMinEmbed` / `RockMaxEmbed`) are one-line edits each; re-running the helper after a constant change re-targets every stone idempotently. Still needs **headset visual validation** to confirm the varied depths read as natural rather than as a uniform 8 cm extrusion.
4. **Seat sit height changed by 8 cm.** Players sit at world Y=1.2 (camera offset) regardless, so eye height is unchanged — but the *perceived* gap between bottom and seat surface shrank. Should still feel like a stone you sit on (top ≈ 42 cm) rather than a stool, but worth a sanity check in headset.
5. **`rock_set_*` is a name-based match.** Any future rock that uses the same FBX but a different name would be missed. Easily extended via the `StonePrefixes` constant in `QuestBuildAPK.cs`.
6. **The 17-stone batch grounding includes the perimeter rocks** that the user hand-placed in earlier slices. If any of those were *intentionally* sitting slightly above ground (e.g. as broken/tilted), they were lowered uniformly. Re-runs of `Ground Stones` are no-ops past the threshold, but the first run already committed the embed. A specific perimeter rock can be raised individually back via the Inspector if needed.

## What still needs headset validation

These can't be confirmed from the flat Editor view; the next two-Quest session should look for them in this order:

1. **Hand mesh orientation actually reads as "natural held controller"** at `+45°` X pitch. If it still looks wrong:
   - Pointing too far down → reduce to `+30°`.
   - Still pointing forward unchanged → likely the FBX's local axis doesn't match the assumption; try a different axis (e.g. `localEulerAngles = (0, 0, -45)` or `(60, 0, 0)`).
   - Looks correct in one hand but mirrored in the other → flip one hand with `localScale.x = -0.9` (or apply different rotations per hand).
2. **No clipping under the hand mesh** as the user gestures — at scale 0.9, the controller mesh is ~10 × 7 × 5 cm, so even at a 45° pitch it shouldn't intersect the seat or wood pile in casual use, but worth a quick visual.
3. **Stones look embedded, not buried — and the variation reads as natural.** From the seated view, the lowered stones should appear to sit naturally in the dirt rather than visibly clipping into a flat surface. Different stones now embed at different depths within the per-category ranges (seats 0.05–0.08 m, rocks 0.06–0.12 m); the variation should subtly disrupt the previously-uniform extrusion. If the spread reads as "too random", tighten the rock range (e.g. 0.07–0.10 m); if "still too uniform", widen to 0.05–0.14 m. Both are one-line edits to the constants in `QuestBuildAPK.cs`.
4. **Sit-height comfort.** The stone seat tops now sit at world Y ≈ 0.42. Visually the player should still read "I'm sitting on a stone" from inside the headset, not "I'm hovering above a half-sunken stone".
5. **Campfire composition still reads as a circle of kerbstones around the wood pile.** The 4 fire-pit `rock_set_01..04` got the same uniform lower; the ring should still look like a defined fire pit, not like four scattered rocks half-buried.

## Validation

- `recompile_scripts` after editing `QuestBuildAPK.cs`: **0 errors, 0 new warnings** (33 pre-existing third-party CS0618 deprecations unchanged).
- `Tools/Quest Setup/Ground Stones` ran cleanly: log `Grounded 17 stone(s) by 0.08 m; skipped 0 already-embedded.`
- `get_gameobject LeftHandMesh` post-fix: `localEulerAngles = (45, 0, 0)`, `localScale = (0.9, 0.9, 0.9)`, parent + components intact.
- `get_gameobject RightHandMesh` post-fix: same as Left.
- `get_gameobject StoneSeat_A` post-fix: `position = (1.6, -0.08, 0)`, scale unchanged, MeshRenderer + collider intact.
- `save_scene`: scene saved cleanly.

## Reversibility

- **Hand rotation:** Inspector edit `localEulerAngles` back to `(0, 0, 0)` on both `LeftHandMesh` / `RightHandMesh`, or `git revert` this slice.
- **Stone grounding:** raise each stone's Y back to 0 individually in the Inspector, or `git revert` the scene file. Re-running `Tools/Quest Setup/Ground Stones` after a revert would just re-embed them.

## What we did NOT change

- Networking, voice, room code, LAN/Internet/Relay logic, Photon Voice, NGO transports.
- XR input actions, controller binding logic, `NetworkBootstrap` controller polling.
- `XRHeadTracker` (the anchor still tracks the raw device pose).
- `XRControllerInputFeedback` (still auto-binds to `transform.GetChild(0)` — i.e. the now-rotated mesh).
- `XRRig` / `CameraOffset` / `VRCamera` / hand anchors — only the visual children rotated.
- Hand asset import / source FBX / `HandsControllerMesh.asset` / `HandController.mat` (the combined mesh + material are unchanged; only the per-hand `Transform` rotation is the polish).
- Forest layout — trees, grass tufts, ground, mountains, fire-pit composition (the wood pile, flame, embers, FireLight, crackle audio) — all untouched. Only the stone Y positions changed.
- `productName`, `bundleVersion`, build settings.

## Next tiny polish step if needed

In priority order (all optional):

1. **Headset-tune hand rotation** if `+45°` reads wrong. One-line edit.
2. **Per-hand handedness mirror** via `localScale.x = -0.9` if the shared mesh reads as right-handed on the left.
3. **Per-rock embed offset** if uniform 0.08 m isn't right for individual perimeter rocks. The helper could grow a per-instance override dictionary; not worth the complexity unless many rocks need it.
4. **Subtle dirt-ring shader / decal** under each stone to sell the "rooted in earth" reading without relying purely on the embed depth.
