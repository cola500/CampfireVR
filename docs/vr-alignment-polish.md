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

### 2. Stone grounding — uniform Y embed

A new Editor helper, `[MenuItem("Tools/Quest Setup/Ground Stones")]` (added in `Assets/Editor/QuestBuildAPK.cs`), walks the active scene's root GameObjects, picks those whose name starts with `rock_set_` or `StoneSeat_`, and lowers each one's world Y by **0.08 m** (8 cm). Already-embedded stones (Y ≤ -0.05) are skipped — re-runs are no-ops.

Result of running the helper once:

```
[QuestBuildAPK] Grounded 17 stone(s) by 0.08 m; skipped 0 already-embedded.
```

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
| `StoneSeat_A` (root) | `position.y: 0 → -0.08` |
| `StoneSeat_B` (root) | `position.y: 0 → -0.08` |
| `rock_set_01..04` (roots, fire-pit kerb) | `position.y: 0 → -0.08` each |
| 11 perimeter `rock_set_*` (roots) | `position.y: 0 → -0.08` each |
| `Assets/Editor/QuestBuildAPK.cs` | Added `GroundStones()` method + `[MenuItem("Tools/Quest Setup/Ground Stones")]` |
| `Assets/Scenes/CampfireRoom.unity` | Saved with the above transform changes |

Nothing else touched. Networking, voice, room code, LAN/Internet/Relay logic, XR input actions, XR rig tracking, NetworkBootstrap, TutorialOverlay, Photon Voice, hand asset source FBX, forest layout (beyond stone Y), grass tufts, trees, mountains, ForestFloor, lights, camera — all untouched.

## Risks

1. **Hand rotation may be wrong direction or wrong magnitude.** Without headset verification, `+45°` X-pitch is an educated guess based on typical Oculus Touch grip-pose vs pointer-pose offsets. The fix may need to be a different angle (most likely in the 30–70° range) or even a different axis (Y or Z) if the FBX's local forward axis doesn't match assumption. Tuning is a single field edit per hand.
2. **Hand mesh handedness.** UniversalController.fbx is a single shared mesh used for both hands. If the model is asymmetric in a way that reads "right-handed" or "left-handed", the wrong hand may look mirrored. Fix is `localScale.x = -0.9` on one side (mirroring via negative scale).
3. **Stone embed depth may be too aggressive or too shallow on individual rocks.** Different `rock_set_*` instances are scaled differently (perimeter rocks at scale 0.4, seats at 0.4 Y-scale, etc.), so a uniform 8 cm absolute lower might bury smaller rocks more than larger ones. Re-running with a different `StoneEmbedDepth` constant is a one-line edit. The helper is idempotent so a follow-up reduces the embed rather than stacking.
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
3. **Stones look embedded, not buried.** From the seated view, the lowered stones should appear to sit naturally in the dirt rather than visibly clipping into a flat surface. If the 8 cm embed reads as "sunk too far", drop the constant to 0.05 m and re-run; if "still floating", raise to 0.10 m.
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
