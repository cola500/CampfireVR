# CI/CD plan — CampfireVR Quest builds

> Investigation-only doc. Compares four ways to stop pressing Cmd+B in Unity for every Quest APK. Recommends a small, safe first slice. No pipeline created yet.

## Current manual build flow

| Step | What happens | Time |
|---|---|---|
| Open `CampfireVR` in Unity Editor | Asset import, script compile, scene load | ~10–30 s warm, several min cold |
| Run `Tools → Quest Setup → Force Legacy Input Handling` (one-shot, after XRI install) | Sets `activeInputHandler = 0`, saves PlayerSettings, triggers assembly reload | < 5 s |
| Run `Tools → Quest Setup → Build Remote Fika APK` (which calls `QuestBuildAPK.BuildTo`) | Switches to Android target, builds APK to `UnityProject/Builds/CampfireVR-remote-fika-test-v0.1.apk` | ~3–6 min warm, ~10 min cold |
| `adb install -r UnityProject/Builds/CampfireVR-remote-fika-test-v0.1.apk` | Streams APK to Quest, upgrades existing install | ~30 s |
| Launch on headset (manual via Quest library OR `adb shell monkey -p com.unitymcplab.campfireroom -c android.intent.category.LAUNCHER 1`) | App boots | ~5–15 s |

Total: 4–7 minutes wall-clock for a warm rebuild + deploy. Johan does this every time he wants to test in headset.

## Project facts that constrain CI

| Fact | Source | CI impact |
|---|---|---|
| Unity Editor `6000.4.7f1` (Unity 6.4) | `ProjectSettings/ProjectVersion.txt` | Build runner needs exact-version Unity image. GameCI ships `unityci/editor:ubuntu-6000.4.7f1-android-3` (or similar). |
| Build target: Android, ARM64, IL2CPP, OpenXR/Oculus loader, `AndroidMinSdkVersion 29` | `ProjectSettings.asset` | IL2CPP requires base OS matching target arch — Android IL2CPP works fine on Ubuntu runners. |
| `activeInputHandler = 0` (Input Manager / legacy) | `ProjectSettings.asset:797` | Already correct in-repo. `QuestBuildAPK.ForceLegacyInputHandling()` re-asserts before each build for safety. |
| Application ID: `com.unitymcplab.campfireroom` | `ProjectSettings.asset:169` | Stable, no per-build mutation needed. |
| Repo visibility: **PUBLIC** at `github.com/cola500/CampfireVR` | `gh repo view` | Eligible for free GitHub Actions minutes (3,000 / month public). Secrets still encrypted — public visibility doesn't expose them. |
| Active scene in build: only `Assets/Scenes/CampfireRoom.unity` | `EditorBuildSettings.asset` | One scene to validate; small surface. |
| One git package dependency: `com.gamelovers.mcp-unity` from public GitHub URL | `Packages/manifest.json` | CI can fetch without auth. |
| Local Unity install present | `/Applications/Unity/Hub/Editor/6000.4.7f1/` | Local-build option is immediately viable. |

### The big blocker: gitignored Asset Store packs

| Pack | Folder | Used by |
|---|---|---|
| Real Stars Skybox Lite | `Assets/Real Stars Skybox/` | Night sky |
| Piloto Studio Campfires | `Assets/Piloto Studio/` | Campfire wood pile mesh (`SM_campfire_001`) |
| WALLCOEUR VFX Fire | `Assets/VFXPACK_FIRE_WALLCOEUR/` | Flame particles |
| Nature Starter Kit 2 | `Assets/NatureStarterKit2/` | Tree / bush textures (mostly evaluated, currently unused) |
| Mountain Terrain Rocks & Tree | `Assets/Mountain Terrain rocks and tree/` | `rock_set_*` + `tree_01` + mountain backdrop + bark textures |
| Terra | `Assets/Terra/` | Dirt ground material + grass card texture |
| ithappy Animals_FREE | `Assets/ithappy/` | Dog companion + atlas texture |
| XRI Starter Assets sample | `Assets/Samples/XR Interaction Toolkit/...` | UniversalController FBX → combined hand mesh (the combined mesh IS committed at `Assets/Models/HandsControllerMesh.asset`) |

All seven packs are gitignored per Asset Store EULA (source may not be redistributed). The scene references them by GUID. **A CI runner that clones the repo without these packs will:**

- Render most of the world as missing-prefab markers / magenta materials.
- Successfully compile every `.cs` script (compile doesn't need scene assets).
- Successfully produce an APK — but the APK will be visually broken in headset.

This is the central constraint that shapes every CI option below.

XRI Samples are re-importable via `Sample.Import()` inside the existing `HandVisualsSetup` helper (Unity registry, no auth). Asset Store packs are *not* re-importable without an authenticated Unity account that owns each pack.

## Option comparison

### Option A — GitHub Actions + GameCI `unity-builder`

**How it works.** Pre-built Docker images from `unityci/editor` run Unity in `-batchmode -nographics` on Ubuntu GitHub runners. The `game-ci/unity-builder` action wraps the CLI invocation, the `game-ci/unity-test-runner` runs EditMode/PlayMode tests, the `game-ci/unity-activate` flow handles license activation.

| Aspect | Verdict |
|---|---|
| Setup complexity | Medium. 1 workflow YAML, 3 GitHub secrets (`UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_LICENSE` for Personal — or `UNITY_SERIAL` for Pro). License activation is fiddly the first time (manual `.alf → .ulf` round trip via Unity's license server). |
| Cost | **GitHub Actions:** free 2,000 min/month for private repos, **unlimited for public** repos. **Unity Personal license:** free (eligibility: org revenue < $200k/year). **Unity Pro:** ~$2,200/seat/year if needed. Total cash cost for hobby-level CampfireVR usage: **$0**. |
| Quest APK output | Yes — Android target works. ~10–15 min per build typical for a small project on `ubuntu-latest`; the first cold build is slower (15–25 min) due to Library import. Caching via `actions/cache` on `Library/` speeds subsequent builds. |
| Asset Store packs | **Blocker.** GameCI has no way to fetch gitignored Asset Store packs. Workarounds: (a) commit them anyway (EULA violation); (b) host them privately on S3 / git-LFS and fetch in CI (cost + license risk); (c) skip scene-level testing in CI and only validate compile. |
| Automatic deploy to headset | No. CI runs in cloud — has no USB connection to your Quest. Could publish APK as artifact + use Meta Developer Hub channels later, but that's a separate pipeline (Meta Quest Store / SideQuest / App Lab) that needs Meta Developer credentials. |
| Risk if license activation breaks | Build skipped silently or fails with cryptic auth error. Personal license re-activation is required ~every 30 days for some runners. |

### Option B — Unity Cloud Build (UCB) / Unity DevOps

**How it works.** Unity hosts the runners. Cloud-side config wired through Unity Dashboard, builds triggered by git push or manually.

| Aspect | Verdict |
|---|---|
| Setup complexity | Low for the build itself (UI-driven), but binding Asset Store packs requires the **Asset Manager** product (separate offering — paid). |
| Cost | Free tier exists for indie projects (last known: ~200 build min/month, 1 concurrent build) but pricing changes often and the official page is behind a 403 from this audit. Pro tier starts in the $9–$99/month range; **verify current pricing at unity.com before committing**. |
| Quest APK output | Yes — explicit Android support. |
| Asset Store packs | UCB has Asset Manager integration, but each Asset Store pack would need to be bound to the same Unity org via legitimate purchases linked to that org's Unity account. For a hobby project with packs purchased on a personal account, the licensing chain is fragile. |
| Automatic deploy to headset | No (same cloud-build limitation). UCB can ship the APK to a Unity-hosted distribution link, which works for testers with a download URL but doesn't auto-install. |
| Lock-in | Higher. Bound to Unity Dashboard org structure. |

### Option C — Local Mac build script

**How it works.** Wrap the existing `QuestBuildAPK.Build` Editor menu in a shell script that drives Unity in `-batchmode` from the command line, then `adb install -r` to a connected Quest.

| Aspect | Verdict |
|---|---|
| Setup complexity | **Lowest.** ~20-line shell script. No secrets, no Docker, no Unity license dance (the editor is already activated on Johan's Mac). |
| Cost | $0 marginal. Uses the laptop already running Unity. |
| Quest APK output | Yes — exactly the same `QuestBuildAPK.BuildTo` path Johan uses today, just invoked from CLI. |
| Asset Store packs | **Resolved by definition** — Johan's local Unity already has them imported via My Assets. Same as the manual flow. |
| Automatic deploy to headset | **Yes.** If the Quest is plugged in via USB-C, the script can chain `adb install -r → adb shell monkey` to install + launch. |
| Limitations | Depends on Johan's Mac being on, Unity not currently in use (Editor can't run a batch-mode invocation while the GUI is editing the same project — would need to close the Editor first). Runs sequentially with manual work. |
| Time savings | Mostly removes the Cmd+B click and the manual `adb install` step. ~30 s saved per cycle, plus eliminates the "did I remember to recompile after edit X?" question. |

### Option D — Hybrid: local Mac build + GitHub Actions compile validation

**How it works.** Two pipelines, each owning what it's good at.

- **Local Mac**: full APK build + auto-deploy (= Option C).
- **GitHub Actions** (separate workflow): on every push, compile-validate the C# scripts using `game-ci/unity-test-runner` or just `dotnet build` against a stub project. Catches type errors, missing references, broken Unity API usage *before* you spend 5 minutes building locally.

| Aspect | Verdict |
|---|---|
| Setup complexity | Medium-low. Add the local script (Option C) immediately; add a compile-only GitHub Action later as a separate slice. |
| Cost | $0 (public repo + Personal license). |
| Quest APK output | Yes (via local script). |
| Asset Store packs | **Compile validation doesn't need scene assets** — just the source `.cs` files, which compile against committed packages + Unity DLLs. CI passes / fails on script correctness only. |
| Automatic deploy to headset | Yes (via local script). |
| Failure modes | If GitHub Action breaks but local script works, you can still ship — CI is advisory, not blocking. |

## Risk analysis

### Are Asset Store assets gitignored?

Yes — 7 packs, ~600 MB total local source, all gitignored per EULA. Documented inline in `.gitignore` with the rationale comment for each. Re-importable on a fresh clone via Package Manager → My Assets for any account that owns the packs.

### Will CI fail without them?

| CI task | Without Asset Store packs |
|---|---|
| `dotnet build` of scripts | ✅ Passes (scripts don't reference pack types directly) |
| Unity compile via `-quit -batchmode` | ✅ Passes |
| Unity scene-level test (open scene + check no missing refs) | ❌ Fails (CampfireRoom.unity has GUID refs to gitignored prefabs) |
| Unity full APK build of CampfireRoom scene | ⚠️ Produces APK but it renders broken |

### Repo readiness

| Question | Answer |
|---|---|
| `Library/`, `Builds/`, `Temp/`, `Logs/` gitignored? | ✅ Yes (`.gitignore` lines 2–4, 54–60) |
| `.mcp.json` (with absolute paths) gitignored? | ✅ Yes (per `Known issues` in README); `.mcp.example.json` is the committed template |
| Build output not committed? | ✅ The 85 MB APK is in `Builds/` which is gitignored |
| All required runtime asset folders committed? | ⚠️ Partial — `Assets/Photon/` (base libs) + `TextMesh Pro/` + `XRI/` + `Models/HandsControllerMesh.asset` + scripts/materials/scene are committed; the 7 Asset Store source folders are not. |
| Secrets in repo history? | Should audit before going public-CI — `git log -p | grep -i 'token\|secret\|key'` is a standard pre-CI sweep. Skipped for this doc (audit-only slice). |

### Non-blocker but worth tracking

- `CreatureMover.cs` (ithappy/Animals_FREE) has `using UnityEditor;` at the top of a runtime script. Will fail Player IL2CPP build with `CS0246` — already flagged in `docs/dog-companion-slice.md` risks. Local fix possible; CI workflow needs to plan for it.
- Unity Hub auto-activates a Personal license on macOS desktop; in CI you have to manage the `.ulf` license file as a secret.

## Recommended first slice — Hybrid, local script first

**Phase 1 (this slice's recommendation, ~30 min):**

1. Add `scripts/build-quest.sh` that wraps `Unity -batchmode -executeMethod QuestBuildAPK.Build -quit` and chains an optional `adb install -r` step.
2. Add `scripts/deploy-quest.sh` (or fold into the build script) that does install + monkey-launch.
3. README update: replace "Cmd+B" in the iteration loop with "`./scripts/build-quest.sh`".

Why first: zero infra, zero secrets, fully reproducible, immediately useful. Doesn't preclude any cloud option later. Works whether or not the Editor is open (script tells you to close it first if it is).

**Phase 2 (separate slice, future, ~1–2 hours):**

1. Add `.github/workflows/compile-check.yml` running `game-ci/unity-test-runner@v4` with EditMode-only test scope.
2. Add a single trivial EditMode test (e.g. assert that `NetworkBootstrap` exists) so the test runner has something to run.
3. Wire `UNITY_LICENSE` + `UNITY_EMAIL` + `UNITY_PASSWORD` as GitHub repository secrets (Personal license).
4. Workflow runs on push to `main` and on PRs — fast (< 5 min), advisory, no APK output.

Why second: catches "I broke the build at 11 PM" before the next morning's session, but doesn't introduce a hard blocker if license activation glitches.

**Phase 3 (much later, only if Phase 2 proves useful):**

- Investigate **Unity Cloud Build** for full APK production *if* Johan is willing to bind the Asset Store packs to a Unity org and budget for the Asset Manager add-on, or commits to building all art assets in-house.
- Investigate **Meta Quest Developer Hub / App Lab** for distribution if you decide to make CampfireVR more widely playable. Each is a separate slice with its own EULA reading.

## Secrets / licenses needed (per phase)

| Phase | What you need |
|---|---|
| 1 (local script) | Nothing new — Unity already activated on Johan's Mac. |
| 2 (GitHub compile check) | GitHub secrets: `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_LICENSE` (Personal `.ulf` file content). Generate `.ulf` via one-time GameCI `unity-request-activation` workflow. |
| 3 (Unity Cloud Build, optional) | Unity Dashboard org account, Asset Manager license binding for each pack, potential Pro seat ($$). |

## Future delivery options (out of scope here, just noting)

- **SideQuest / App Lab** — distribute to testers without going through full Meta Quest Store review. Needs Meta Developer account + signed APK.
- **Meta Quest Store** — full storefront. Needs Meta Developer + Oculus Store Submission Guidelines compliance + payment integration. Several days of work to navigate first time.
- **Direct `adb install` for personal use only** — what we do today, what Phase 1 automates.

## Untouched per scope

This slice produces documentation only. No scene, gameplay, networking, voice, dog, hands, environment, or other runtime code changed. No CI configuration written yet — Phase 1's `scripts/build-quest.sh` is *proposed* in this doc, not yet created.

## Verdict + open questions for Johan

**Recommended next slice:** Phase 1 — create `scripts/build-quest.sh` + update README. ~30 minutes of work, zero risk, immediate quality-of-life win.

**Open questions before going further:**

1. Do you want Phase 1 implemented now, or just the plan filed? (Spec said "do not implement pipeline yet unless obviously safe" — local shell script is borderline safe but technically *is* implementation.)
2. Phase 2 (GitHub Actions compile validation) — interesting to you, or premature?
3. Phase 3 — willing to pay for Unity Cloud Build / commit to running CampfireVR on a Unity org account? If not, full cloud APK build is essentially gated by the Asset Store EULA problem.
4. Should I do the pre-CI secret audit (`git log -p | grep`) before Phase 2 ever runs?
