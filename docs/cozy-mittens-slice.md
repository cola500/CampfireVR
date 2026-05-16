# Cozy mittens slice

> Procedural low-poly mittens replace the XRI UniversalController mesh on the local hands. ~50% fewer triangles, fits the campfire's warm low-poly aesthetic, zero new packages. The controller mesh stays on disk as a one-menu-click fallback.

## Why mittens

From the controller-visuals audit (`docs/controller-visuals-audit.md`): the local hands had been wearing a generic Quest-Touch-family silhouette since the hand-visuals slice — readable in headset, but dated and visually flat (no texture, no normal map, single matte colour). All the "make it actually look better" options came with tradeoffs:

- **Install `com.unity.xr.hands` + samples** — realistic hands, but fingers default to T-pose without hand-tracking data + adds ~18 MB.
- **Install Meta XR Core SDK** — accurate Touch 3 / Touch Pro models, but hundreds of MB.
- **Switch XR backend to OpenXR** — runtime-loaded controller models, but architectural change for a polish issue.
- **CC0 low-poly hand mesh import** — need to find one, license-check, UV-rework.
- **Procedural mittens** — fits the existing cozy stylized direction (dog, dirt ground, stone seats, bark-textured wood pile), no new dependencies.

The audit recommended mittens, and this slice executes that.

## What landed

| New asset | Path | Notes |
|---|---|---|
| `MittenHandsSetup` Editor helper | `UnityProject/Assets/Editor/Environment/MittenHandsSetup.cs` | Menu: `Tools/Quest Setup/Apply Mitten Hands`. Idempotent. |
| `LeftMittenHand.asset` | `UnityProject/Assets/Models/LeftMittenHand.asset` | ~215 KB, ~2,384 triangles. Procedurally generated; baked once and saved as a Mesh asset. |
| `RightMittenHand.asset` | `UnityProject/Assets/Models/RightMittenHand.asset` | Same as left, with the thumb mirrored to the `-X` side. |
| `MittenWarm.mat` | `UnityProject/Assets/Materials/MittenWarm.mat` | Standard shader (Built-In RP). Warm wool tint `(0.42, 0.28, 0.20)`, metallic 0, smoothness 0.04 — reads as fabric in firelight, not plastic. |
| Scene edit | `Assets/Scenes/CampfireRoom.unity` | `LeftHandMesh` + `RightHandMesh` MeshFilter/MeshRenderer swap to point at the new mesh + material. localScale (0.9, 0.9, 0.9) and the +45° X-pitch from `vr-alignment-polish` are preserved. |

## Mesh construction

Each mitten is the union of four primitive sub-meshes baked via `Mesh.CombineMeshes`:

```
Frame (authored before the hand-anchor pitch is applied):
  +Z = pointing direction (where the controller "points")
  +Y = back of hand (up when relaxed)
  +X = hand's local right

Palm sphere         pos (0, 0, 0)              scale (0.060, 0.045, 0.075)
  Slightly egg-shaped; flattened on Y so the back-of-hand silhouette
  reads as a hand, not a baseball.

Finger bulge sphere pos (0, -0.005, 0.075)     scale (0.052, 0.042, 0.085)
  Extends forward of the palm. Centered slightly low so the silhouette
  reads as "knuckles ahead" rather than a smooth blob.

Thumb sphere        pos (±0.045, 0.005, 0.020) scale (0.030, 0.028, 0.045)
                    rot (15°, ±30°, ∓8°)
  Sphere (not capsule) for the rounded mitten thumb compartment.
  Position + rotation flips sign with `thumbSide` — left hand's thumb
  is on +X, right hand's is on -X.

Cuff cylinder       pos (0, 0, -0.050)         scale (0.078, 0.014, 0.078)
                    rot (90°, 0°, 0°)
  Thin disc at the wrist. Reads as the band where the mitten meets
  your sleeve. Single material, so no contrasting colour — just the
  silhouette change.
```

Authored size: roughly 8 × 5 × 16 cm before the 0.9× scale on the hand mesh GameObject. With scale applied → ~7 × 4 × 14 cm visible in headset. Slightly oversized compared to the controller mesh (intentional — mittens are bulkier than bare controllers, that's the whole point).

## Materials / palette

Single shared material `MittenWarm.mat` across both hands. Standard shader, matte fabric tone:

| Property | Value | Why |
|---|---|---|
| `_Color` | `(0.42, 0.28, 0.20, 1)` | Sun-bleached wool brown. Lives between the dog coat `(0.55, 0.38, 0.22)` and the dark grey controller `(0.22, 0.20, 0.18)` we replaced. |
| `_Metallic` | `0` | Fabric, not metal. |
| `_Glossiness` | `0.04` | Almost zero specular — fabric should swallow the fire's flicker, not bounce it. |
| `_MainTex`, `_BumpMap`, etc. | none | Solid colour, single material, single draw call. |

The wool tone sits in the same family as the campfire wood pile (`CampfireWood.mat`), the dog (`DogCoat.mat`), and the remote head (`RemotePlayer.mat`). The intent: when you look at your own hands from across the fire, they belong to the scene's colour world.

## Performance

| Metric | Before (controller mesh) | After (mittens) |
|---|---|---|
| Triangles per hand | ~5,000 | ~2,384 |
| Material | `HandController.mat` (Standard, solid) | `MittenWarm.mat` (Standard, solid) |
| Submeshes | 1 (combined) | 1 (combined) |
| Textures | 0 | 0 |
| Shadow casters | 0 (off) | 0 (off) |
| Draw calls | 1 per hand | 1 per hand (could batch with remote hands later) |
| BoxCollider | yes (inert) | yes (inert, untouched) |

Net runtime impact: slightly cheaper than before. Both meshes load from `.asset` files, no per-frame procedural cost.

## Fallback path

The previous `HandsControllerMesh.asset` (703 KB) and `HandController.mat` are intentionally **left on disk**. Two menu items now exist for hand-visual swapping:

- `Tools/Quest Setup/Apply Mitten Hands` — current default. Assigns mitten mesh + `MittenWarm.mat`.
- `Tools/Quest Setup/Apply Hand Visuals` — restores the combined XRI UniversalController + `HandController.mat`. Useful for A/B comparison in headset, or if mittens read wrong.
- `Tools/Quest Setup/Apply Hand Visuals (Force Sphere)` — older sphere-fallback. Lowest-fidelity option.

Each menu is idempotent and overwrites the previous selection's MeshFilter / MeshRenderer assignments. No mesh assets are deleted by any of them.

## XR / input / networking impact

Zero. By design:

- `LeftHandAnchor` / `RightHandAnchor` transforms are untouched. `XRHeadTracker` continues to drive them from `InputDevices.GetDeviceAtXRNode(XRNode.LeftHand/RightHand)`.
- `XRControllerInputFeedback` continues to auto-bind to `transform.GetChild(0)` (= `LeftHandMesh`/`RightHandMesh`) at OnEnable. `_baseScale` re-captures `(0.9, 0.9, 0.9)` cleanly on next play — the trigger-press 15 % scale pulse continues to fire on the mitten mesh.
- `BoxCollider` retained — no script depends on it, but no reason to remove it in this slice.
- `NetworkHead.cs` and `PlayerHead.prefab` (= the remote avatar) are **not** touched. Remote players still see the XRI controller mesh + `HandController.mat` on each other's hands. See "Remote avatars" below.

## Remote avatars — recommendation, not action

Per slice spec ("Do NOT replace remote avatar visuals yet unless trivially shareable") I deliberately left `PlayerHead.prefab` on the controller mesh. The mitten meshes are local-only for this slice.

If the headset test reads the local mittens as a clear win, the trivial-share extension is a 5-line addition to `MittenHandsSetup.Apply()` that also loads `PlayerHead.prefab` via `AssetDatabase.LoadAssetAtPath<GameObject>`, finds the `LeftHandVisual` / `RightHandVisual` children, swaps the same mesh + material via the existing `RemoteAvatarPolish.cs` pattern, and saves the prefab. The rationale for waiting is just "validate the look locally first, then mirror it".

## What was NOT changed

- `NetworkBootstrap`, `NetworkHead`, `ClientNetworkTransform`, `VoiceBootstrap`, `ServicesBootstrap` — networking + voice untouched.
- `XRHeadTracker`, `XRControllerInputFeedback`, `XRTrackingOriginSetter` — tracking untouched.
- `LeftHandAnchor`, `RightHandAnchor`, `VRRig`, `CameraOffset` transforms — XR rig untouched.
- `HandVisualsSetup.cs` (the older controller-mesh helper) — kept as the fallback menu.
- `PlayerHead.prefab` — remote avatar untouched.
- Scene hierarchy beyond the two MeshFilter/MeshRenderer property edits.
- Room code, session logic, build pipeline, all input bindings.

## Validation

- `recompile_scripts` after adding `MittenHandsSetup.cs`: **0 errors, 34 warnings** (all pre-existing third-party CS0618 deprecations).
- `Tools/Quest Setup/Apply Mitten Hands` ran cleanly: `[MittenHandsSetup] Applied cozy mittens to 2 hand mesh(es) — material=MittenWarm (RGBA(0.420, 0.280, 0.200, 1.000)), tris≈2384 per hand.`
- Scene post-save: `LeftMittenHand` (1 ref), `RightMittenHand` (1 ref), `MittenWarm.mat` (2 refs — both hands). XRHeadTracker (3 refs) and XRControllerInputFeedback (2 refs) intact.
- Old `HandsControllerMesh.asset` + `HandController.mat` still on disk for fallback.
- Console clean after the helper run.

## Headset validation still needed

- **Silhouette proportions** — palm/finger-bulge/thumb sizes are tuned from the controller-mesh footprint (~10 cm). May read too small or too bulky in headset.
- **Cuff visibility** — the wrist disc was sized to match the back-of-hand width; if the camera-to-hand distance feels different in VR, the cuff might disappear behind the palm or stick out as a brim.
- **Thumb angle** — the `(15°, ±30°, ∓8°)` rotation is a first guess. Real mittens have the thumb sticking out *and* slightly forward; if it reads as "boxing glove with a tumour" the angle's wrong.
- **Colour in firelight** — `(0.42, 0.28, 0.20)` should warm up under FireLight's tint, but the actual readability in VR is unknown until headset test.
- **Trigger-press pulse** — the 15 % scale animation on `_baseScale * 1.15f` should still feel correct on the mitten mesh.

All tunable via constants at the top of `MittenHandsSetup.cs`. Re-run the menu after any edit; it's idempotent.

## Future options

In rough order of polish-value-per-effort:

1. **Mirror to remote avatars** — 5-line addition to apply same mesh + material to `PlayerHead.prefab` so friends see each other in matching mittens.
2. **Two-toned mittens** — split palm + finger bulge into separate sub-meshes, assign a slightly lighter wool tone to the palm via a second material slot. Reads as "darker on top, lighter on the gripping side" without needing a texture.
3. **Cuff colour stripe** — extract the cuff cylinder into its own submesh, give it a third material in a contrasting warm tone (deep red, forest green, mustard). Tiny ornament that picks a "team colour" per player if we ever do multiple pairs of testers.
4. **Subtle wool texture** — CC0 noise-pattern albedo + normal map (Polyhaven has a few "fabric" textures suitable). Adds detail without per-vertex authoring. ~10 lines added to material setup.
5. **Animated thumb on trigger press** — track trigger axis (already polled in `XRControllerInputFeedback`), animate the thumb sphere's rotation by ~10° on press for the "I'm pinching" feedback. Tiny scope, looks alive.
6. **Per-finger split** — abandon the mitten silhouette for full gloves with five separate fingers. Big jump in complexity (especially without bones/animation); not recommended for an indie cozy project.
