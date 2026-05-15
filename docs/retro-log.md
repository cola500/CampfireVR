# Retro log

Lessons that were not obvious before we shipped the slice they came from.

## Networking

**NGO 2.x does not auto-bind `UnityTransport`.** Even with `UnityTransport` on the same GameObject as `NetworkManager`, `NetworkConfig.NetworkTransport` is `null` until explicitly assigned. Symptom: `[Netcode] No transport has been selected!` + `NullReferenceException` from `StartHost`. Fix: one line in our setup menu (`nm.NetworkConfig.NetworkTransport = transport;`). One line, easy to miss.

## XR / HMD tracking

**Floor tracking origin alone was not enough on Quest 3.** Setting `OculusSettings.EnableTrackingOriginStageMode = 1` and calling `TrySetTrackingOriginMode(Floor)` did not land the camera at the user's real head height. The user spawned at floor level inside the fire pit. Fix: switch to Device origin and use an explicit `CameraOffset y=1.2` child between `VRRig` and `VRCamera`. The offset is the absolute base eye height; HMD pose deltas are added on top.

**Camera does not auto-track HMD pose in Unity 6 + Oculus XR Plugin.** A `Camera` with the `MainCamera` tag gets stereo eye matrices applied (so depth looks right) but no transform updates from the HMD. Without a Tracked Pose Driver the world appears glued to the head — the user moves but the camera doesn't. Fix: tiny `XRHeadTracker` that reads `XRNode.CenterEye` via `UnityEngine.XR.InputDevices` each `Update` + `Application.onBeforeRender`. No packages required.

**The "world follows my head" bug had three different causes** before we found the real one. We initially suspected scene parenting, then tracking mode, before realising the camera simply wasn't being moved by anything. Worth checking the simple `transform.position` first.

## Editor / MCP workflow

**A single MCP server with a bad tool schema can brick the whole Claude Code session.** Adding `@meta-quest/hzdb` to `.mcp.json` produced `API Error: 400 … tools.18.custom.input_schema: input_schema does not support oneOf, allOf, or anyOf at the top level` on the very first prompt, and every subsequent prompt failed identically until the server was removed. The Anthropic API rejects the entire `tools` array when one tool is invalid, so the tools that *would* work — including all of mcp-unity's — also stopped working. We ruled out mcp-unity quickly because (a) the only delta from a working session was the new `hzdb` block, and (b) removing that single block restored normal operation without touching mcp-unity. The root cause is well-documented upstream (`anthropics/claude-code` issues #4886, #10606, #30212) and Anthropic has closed every request to relax the validation, so the realistic fix path is on the MCP author. See `docs/tooling-known-issues.md` and `docs/hzdb-investigation.md`. Lesson: when adding a new MCP server, expect "all-or-nothing" failure mode — there's no graceful degradation per tool.

**`recompile_scripts` does not refresh the asset database.** A `.cs` written via MCP isn't visible to Unity until you call `Assets/Refresh` (or focus the Editor). Symptom: `update_component` fails with `Component type 'X' not found in Unity` immediately after writing the script.

**`save_scene` blocks in Play mode.** If a setup menu modifies the scene during Play, the changes only affect the running instance — they don't persist. Stop Play, re-run the menu, then save. We hit this when fixing the transport binding.

**MCP cannot bind `UnityEngine.Object` references through JSON.** `{"target": "Flame"}` and `{"target": {"instanceId": -2864}}` both return success but leave the field null. Workarounds we ended up using:
- Auto-find by name in `OnEnable` (used in `FaceTarget`, `NetworkHead`).
- Wire references inside an Editor menu using `SerializedObject` (used in `NetworkSetup` for the prefab's visual reference).

**`get_gameobject` returns large payloads.** ~250 lines per object, with circular references and `Unable to serialize` placeholders. Workable, just noisy.

## Unity Cloud

**"Missing Project ID — you are not a member of this project" can appear on build with no project-side change.** `ProjectSettings.asset` still has the correct `cloudProjectId` / `organizationId`, the account is the same one that linked the project, the dashboard shows everything Active. The dialog is Unity Editor's stale auth-token cache mis-answering its own membership check. Fix: `Unity → Quit`, reopen the project via Hub, build again. No code, settings, or dashboard changes needed.

## Build / device

**Rosetta 2 is required even though Unity is arm64-native.** Some toolchain step needs it. Symptom: a dialog about Rosetta when running Unity in batch mode. Fix: `sudo softwareupdate --install-rosetta --agree-to-license`. Not prominently documented anywhere.

**Default Oculus settings target Quest 2 only.** `TargetQuest2: 1`, others `0`. Quest 3 runs Quest 2 builds fine but it is worth flipping `TargetQuest3` when we care about Quest 3-specific features.

**Empty controller battery looks exactly like a software bug.** We spent time debugging "left hand tracking is broken" before realising the controller was simply off. Now we list battery as step one whenever a tracked thing doesn't appear.

## Process

**Thin slices keep paying off.** Every multi-step slice has surfaced something we did not expect. Splitting "Quest VR" into spawn → world parenting → HMD tracking → hand presence → trigger feedback made each bug observable in isolation. None of those would have been findable inside a single "make VR work" PR.

**Stop and ask before destructive or external-state actions.** Package installs, restarts of Claude Code, GitHub repo creation, Quest build deploys — we stop and ask. The few seconds of friction is cheaper than rolling back.

**Always include a `Why:` when committing a non-obvious fix.** Future-us reading the log six months later will not remember why `EnableTrackingOriginStageMode` matters or what `ClientNetworkTransform` overrides. The commit message is the documentation.
