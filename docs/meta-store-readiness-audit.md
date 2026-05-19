---
title: Meta Horizon Store readiness audit (App Lab / Early Access track)
description: Gap analysis comparing CampfireVR's current state against Meta's current submission requirements for an Early Access (formerly App Lab) release. Investigation-only.
category: meta
status: stable
last_updated: 2026-05-19
sections:
  - Context and scope
  - What changed since "App Lab"
  - Where CampfireVR stands today
  - Blockers (must fix before submission)
  - Recommended (should fix before submission)
  - Optional / nice-to-have
  - Sequencing — a realistic order to tackle the blockers
  - Things I deliberately did NOT verify
  - Sources
---

# Meta Horizon Store readiness audit — App Lab / Early Access track

Snapshot of where CampfireVR sits relative to Meta's 2026 submission requirements. **Investigation only** — this slice does not change any code, scene, or settings. The deliverable is the gap list, the severity, and a recommended order to tackle it. Decisions about whether to actually submit live in a future slice.

## Context and scope

CampfireVR is a two-player social-VR hobby experiment for Quest 3 (`docs/vision.md`). Current distribution is direct sideload + private friend zip. The user asked: what would it take to ship this on Meta's store, specifically on the App Lab / Early Access path rather than full Horizon Store?

Mapped against:
- The 57-item **Virtual Reality Check** (VRC) list — applies equally to Early Access and full Store submissions.
- Store metadata requirements (icons, screenshots, trailer, ratings).
- Privacy + legal obligations triggered by voice chat and any data collection.

Not mapped:
- Children's privacy compliance (we recommend 13+ self-certification — see below).
- Multiplayer-matchmaking platform services (we already use Unity Relay + Photon Voice; no Meta Platform Services binding planned).
- Hand-tracking / mixed-reality / passthrough — out of scope for the current campfire scene.

## What changed since "App Lab"

The thing called "App Lab" no longer exists as a separate channel. On 2024-08-05 Meta merged it into the unified **Meta Horizon Store**. Submissions go through the same dashboard and review flow regardless; the **Early Access** badge replaces the old App Lab framing for in-progress apps.

What that means practically for an indie hobby project:

- Same technical bar (VRCs, signing, target SDK) as a full Store release.
- Lighter expectation on "feels finished" — Early Access communicates "work in progress" to users.
- Same metadata requirements (icons, screenshots, trailer) — no longer downgraded.
- Private invite links (90-day per build) still work for closed playtests, unlimited.

The 2021 Meta blog post about "App Lab submission tips" still floats high in search results but **predates the merger** and contains downgrades (e.g. some Asset VRCs marked "recommended only") that are no longer current. Don't anchor on it.

## Where CampfireVR stands today

Audit against each category. Status legend: **PASS** = meets the bar / **GAP** = work needed / **BLOCKER** = must be fixed before any submission / **UNKNOWN** = not verified in this slice.

### Build configuration (from `ProjectSettings.asset` + `QuestBuildSetup.cs`)

| Requirement | Current | Status |
|---|---|---|
| `targetSdkVersion` >= 34 (Android 14, mandatory for new app entries created on/after 2026-03-01) | `0` ("auto" — resolves to highest SDK Unity finds installed; unverified at runtime) | **GAP** — pin explicitly to 34, don't trust auto. |
| `minSdkVersion` >= 29 (Quest minimum) | `29` | **PASS** |
| 64-bit only (`arm64-v8a`) | `AndroidTargetArchitectures: 2` (= ARM64-only) | **PASS** |
| IL2CPP scripting backend | Set in `QuestBuildSetup.cs` (`ScriptingImplementation.IL2CPP`) | **PASS** |
| APK Signature Scheme v2 | Standard for Unity Android builds | **PASS** (implicit) |
| Release-signed APK (not debug keystore) | `androidUseCustomKeystore: 0` + empty `AndroidKeystoreName` — currently signed with Unity's debug keystore | **BLOCKER** — generate release keystore, configure signing, store keystore + alias secrets outside repo. |
| Linear colour space | Set in `QuestBuildSetup.cs` (`ColorSpace.Linear`) | **PASS** |
| Vulkan + GLES3 graphics APIs | Set in `QuestBuildSetup.cs` | **PASS** |
| `bundleVersionCode` increments per submission | Still `1` (never bumped) | **GAP** — needs to increment for each Store upload; current build pipeline doesn't auto-bump. |

### Functional VRCs (subset most likely to hit us)

| Requirement | Current | Status |
|---|---|---|
| Head-tracked graphics within 4 s of launch, or show loading indicator | Tutorial panel + scene present at boot; not measured explicitly. | **UNKNOWN** — needs a timed boot measurement on Quest 3. |
| Recenter must work via user-triggered action | Right A button → recenter via NetworkBootstrap, confirmed in `docs/install-on-quest.md` instructions to testers. | **PASS** (pending physical-headset re-verification — seat-relative rig might confuse system recenter expectations) |
| Render / hide hands / ignore input when focus is lost (system menu) | Not explicitly handled — XRHeadTracker keeps reading device pose unconditionally in `LateUpdate`. | **GAP** — investigate Unity's `OnApplicationFocus(false)` and pause input + hide hand visuals while paused. |
| No crash / freeze during a 30+ minute session | Long-press Y stop + try/catch teardown in `NetworkBootstrap` covers session-recovery path. No formal soak test. | **UNKNOWN** — recommend a 30-min two-headset session before submission. |
| Completability — user can't get stuck without an exit path | Long-press Y returns to "Ready" idle state; Meta button always reachable. | **PASS** |

### Performance VRCs

| Requirement | Current | Status |
|---|---|---|
| Maintain declared refresh rate (72 / 90 / 120 Hz, Quest 3 default 90) | Not declared explicitly; current scene is low-poly and well within budget on warm runs, but no profiled measurements committed. | **UNKNOWN** — needs frame-time capture during a connected two-player session (worst case: head + hand pose sync + Photon Voice + Relay traffic). |
| Foveated rendering / fixed foveated rendering enabled | Not configured. | **GAP** — Oculus loader supports it; trivial enable, real perf wins. |
| App labelled with target refresh rate | Not set. | **GAP** — declare in dashboard during submission, decide 72 vs 90 first. |

### Store metadata (none exists yet)

| Required asset | Format | Status |
|---|---|---|
| App icon | 512x512 PNG (24-bit) | **BLOCKER** — `m_BuildTargetIcons: []`; placeholder Unity icon ships today. |
| Hero cover | 3000x900 PNG | **BLOCKER** |
| Landscape cover | 2560x1440 PNG | **BLOCKER** |
| Square cover | 1440x1440 PNG | **BLOCKER** |
| Portrait cover | 1008x1440 PNG | **BLOCKER** |
| Screenshots | exactly 5, 2560x1440 PNG, no duplicates | **BLOCKER** |
| Trailer | MP4 H.264 / AAC, 1080p–2K, 30 s – 2 min | **BLOCKER** |
| Trailer cover | 2560x1440 PNG | **BLOCKER** |
| Logo | optional transparent PNG up to 9000x1440 | optional |
| App description | dashboard form | **BLOCKER** |
| Supported devices | dashboard form (Quest 2 / 3 / 3S / Pro) | **BLOCKER** |

### Ratings + age

| Requirement | Current | Status |
|---|---|---|
| IARC rating completed (dashboard questionnaire) | Not done. | **BLOCKER** |
| Age-group self-certification (13+ / Mixed / Children) | Not done. **Recommended: 13+** to stay out of COPPA scope and avoid the Platform-SDK `Get Age Category` integration that Mixed-with-voice would require. | **BLOCKER** |
| Comfort rating (Comfortable / Moderate / Intense) | Not declared. Seated experience with no locomotion → very likely **Comfortable**. | **BLOCKER** |

### Privacy + legal

| Requirement | Current | Status |
|---|---|---|
| Privacy policy URL (HTTPS, publicly accessible, names org + app, describes processing) | Not exists. The only "privacy" string in the repo is a one-line note in `debug-logging.md` about local log storage. | **BLOCKER** — voice chat + Photon servers + Unity Relay all trigger this. |
| Data Use form in dashboard | Not done. Must disclose: voice audio (Photon Voice 2), Relay alloc metadata (Unity Services), local debug logs (don't leave the headset unless tester sends them). | **BLOCKER** |
| Voice chat moderation / reporting | No in-app report flow exists. For 13+ self-cert with two-player private rooms, Meta does not mandate moderation tooling but app policy still requires response to abuse reports. | **GAP** (not blocker for 13+ self-cert with closed-room model). |
| COPPA-compliant flows for under-13 users | We do not support under-13 — 13+ self-cert sidesteps this. | **PASS** (by virtue of opting out). |

## Blockers (must fix before submission)

In rough size order:

1. **Release-signing keystore** — generate via `keytool`, configure in `ProjectSettings → Publishing Settings`, store the keystore file outside the repo, document recovery (lose the keystore = lose the ability to upgrade the app on existing installs, permanent). ~30 min.
2. **App icon + store-asset bundle** — 8 PNGs + 1 MP4 trailer + 1 trailer cover. Realistic timing: 1–2 evenings if we already have a visual identity for CampfireVR; longer if we're starting cold. The screenshots and trailer should be shot from the actual app — needs a stable two-player session for any non-solo scenes.
3. **Privacy policy hosted at a stable HTTPS URL** — minimum content: org name, app name, what data we process (voice audio via Photon, Relay metadata via Unity, local debug logs that never leave the device unless tester opts in), purpose, retention, deletion request path, contact. GitHub Pages on the `cola500/CampfireVR` repo is the cheapest hosting. ~1 hour to write + publish.
4. **Pin `targetSdkVersion = 34`** — `QuestBuildSetup.cs` currently uses `AndroidSdkVersions.AndroidApiLevelAuto`. Change to `AndroidSdkVersions.AndroidApiLevel34` (or whatever the current Unity enum is). ~5 min + a build to verify.
5. **IARC rating + age self-cert + comfort rating** — dashboard questionnaires, no work in the project itself. Likely 30 min including reading IARC's questions.
6. **App description + supported devices declaration** — dashboard. ~1 hour.

## Recommended (should fix before submission)

7. **`OnApplicationFocus(false)` handler** in NetworkBootstrap (or a new SystemFocusGuard MonoBehaviour) — pause input reads + hide remote hand visuals while the system menu is open. Reduces "ghost input fires while user is in the Meta menu" risk. ~1 hour.
8. **`bundleVersionCode` auto-increment in the build pipeline** — extend `scripts/build-quest.sh` or `QuestBuildAPK.cs` to bump the code based on git commit count or a monotonic stamp. Meta only requires that it strictly increases across Store uploads — Editor-side iteration builds don't need it. ~30 min.
9. **Foveated rendering** — flip on in Oculus loader settings; trivial perf win. ~5 min.
10. **Frame-time capture during a real two-player session** — confirm we're locked at 90 Hz with no missed frames during a 5-min connected session including voice + remote head/hand sync. Use OVRMetricsTool or `adb logcat` for the headset's perf overlay. ~1 hour.
11. **30-minute soak test on two physical Quests** — equivalent to a Remote Fika session. Watch for memory growth in `adb shell dumpsys meminfo` and any thermal throttling.

## Optional / nice-to-have

12. **Logo asset** for the optional transparent PNG slot — not required but improves Store presentation.
13. **`scripts/store-assets/` directory** with source files (Affinity / Krita / Blender) for icon + cover assets, so cuts can be re-rendered against new screenshot data later.
14. **In-app "report a problem" UI** — not required for 13+ self-cert with private rooms, but useful for tester feedback regardless.
15. **`docs/store-listing-copy.md`** — keep the public app description, what's-new text, and tagline under version control rather than only in the Meta dashboard.

## Sequencing — a realistic order to tackle the blockers

This is one possible sequence — not a commitment. Each step is independently slice-sized.

1. **Pin `targetSdkVersion = 34`** (1 hour, build verification included). Cheapest blocker, gets us closer to "submittable" immediately.
2. **Release keystore + `bundleVersionCode` auto-increment** as one slice (~1 hour). Both build-pipeline; logical bundle.
3. **`OnApplicationFocus` handler + foveated rendering** (~1.5 hours). Functional VRC + perf VRC, both small.
4. **Privacy policy draft + GitHub Pages publish** (~1 hour). Unblocks the dashboard side without touching code.
5. **Frame-time capture + 30-min soak test** (~2 hours including session setup). Validates the perf VRC claim before we commit screenshots / trailer to a build.
6. **Store-asset production** — icon, 4 cover sizes, 5 screenshots, trailer (~3–6 hours, depending on how much we want to art-direct).
7. **Dashboard fill-in** — IARC, age self-cert, comfort rating, description, supported devices (~1 hour).
8. **Submission** — upload via MQDH or `ovr-platform-util`, request review.

Total: ~10–15 hours over 1–2 weeks if done in evening-sized slices.

## Things I deliberately did NOT verify

Honest list of stuff this audit punted on:
- I did not run the project on a headset during this slice — `UNKNOWN` items in the tables above need physical verification.
- I did not read every VRC in the 57-item list. I covered the categories most likely to hit a social-VR app with voice; specialised VRCs (hand tracking, mixed reality, controller-free input) don't apply to the current build.
- I did not validate the `AndroidTargetSdkVersion: 0` claim that "auto resolves to 34" — that depends on which Android SDK platforms Unity finds installed on Johan's Mac. Should be re-verified by building once and inspecting the resulting AndroidManifest.xml.
- I did not test whether the **Photon Voice 2** EULA is compatible with Meta Store distribution (Photon's terms have evolved; quick check before submission is prudent).
- I did not check whether Unity Relay's free-tier ToS allows Store-distributed apps (likely yes, but worth confirming).
- I did not audit `AndroidManifest.xml` for declared permissions — Unity generates it from `ProjectSettings`, but Photon Voice / XRI / Oculus loader can each inject additional permissions that the review team will scrutinise. Worth a pre-submission `aapt dump permissions` pass on the built APK.

## Sources

- Meta Horizon VRC Guidelines — <https://developers.meta.com/horizon/resources/publish-quest-req/>
- Submitting Your App — <https://developers.meta.com/horizon/resources/publish-submit/>
- Android 14 / API 34 deadline 2026-03-01 — <https://developers.meta.com/horizon/blog/meta-quest-apps-android-14-march-1/>
- App Lab → Horizon Store merger 2024-08-05 — <https://developers.meta.com/horizon/blog/get-apps-ready-app-lab-meta-horizon-store-meta-quest-developers/>
- Asset Design Guidelines (icon / screenshot / trailer sizes) — <https://developers.meta.com/horizon/resources/asset-guidelines/>
- Privacy Policy Requirements — <https://developers.meta.com/horizon/policy/privacy-policy/>
- Age Group Self-Certification + COPPA — <https://developers.meta.com/horizon/resources/age-groups/>
- Build Upload Overview (MQDH / `ovr-platform-util`) — <https://developers.meta.com/horizon/resources/publish-upload-overview/>
- "Tips for ex-App-Lab developers on Horizon Store" — <https://developers.meta.com/horizon/blog/new-meta-horizon-store-app-lab-tips-overcoming-5-key-challenges/>
