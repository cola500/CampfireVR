---
title: Performance checklist
description: How to measure CampfireVR's runtime performance on a Quest 3, what counts as pass / fail, and which scene items are most likely to cause regressions.
category: meta
status: stable
last_updated: 2026-05-19
sections:
  - When to run this
  - Refresh-rate target
  - Enabling the Quest performance overlay
  - What to watch
  - Pass / warn / fail criteria
  - Common red flags
  - High-risk scene items today
  - What's already done at the build-config level
  - Recording a measurement
---

# Performance checklist

How to confirm CampfireVR holds frame on a Quest 3 before sharing a friend build or submitting to App Lab. Lab-notebook style — designed to be re-run quickly, not read end-to-end every time.

## When to run this

- **Before every shared friend build** if the scene changed. Cheap to re-verify after a slice that touches the scene; expensive to recover from a regression that ships to a tester.
- **Before App Lab submission** as a non-optional pass.
- **After any change** to: tree counts, shader assignments, shadow settings, particle systems, voice / network load patterns.

## Refresh-rate target

Default Quest 3 refresh rate is **90 Hz** (we have not declared otherwise in the Oculus loader settings). The frame budget at 90 Hz is **11.1 ms** per frame on the App side (GPU and CPU each).

We don't actively raise to 120 Hz — the scene budget is well under 90 Hz headroom, and 120 Hz adds 33 % more GPU pressure for a small perceptual gain on a seated scene.

## Enabling the Quest performance overlay

Two options. The first is more lightweight, the second gives more detail.

**Option A — built-in OVR metrics overlay (one shell command):**

```sh
adb=/Applications/Unity/Hub/Editor/6000.4.7f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$adb shell setprop debug.oculus.openxr.LogPerformanceCounters 0
$adb shell setprop debug.oculus.metricsOverlay 1
$adb shell am force-stop com.unitymcplab.campfireroom
$adb shell monkey -p com.unitymcplab.campfireroom -c android.intent.category.LAUNCHER 1
```

Numbers overlay in the corner of the headset view; turn off with `$adb shell setprop debug.oculus.metricsOverlay 0`.

**Option B — OVRMetricsTool standalone app:**

Install from Meta Quest Developer Hub → Tools → OVRMetricsTool. Gives detailed history graphs (App GPU time, App CPU time, missed frames count, thermal level, GPU level, CPU level, stale frame %).

Worth installing once on the Quest you use for perf measurements; not needed on tester devices.

## What to watch

| Metric | What it means | Where to find it |
|---|---|---|
| **App GPU time** | Time GPU spent rendering our frame (per eye actually accumulated) | Built-in overlay, OVRMetricsTool "App GT" |
| **App CPU time** | Time CPU spent on our frame work | Built-in overlay, OVRMetricsTool "App CT" |
| **Missed frames** | Frames where compositor had to reproject because we missed deadline | OVRMetricsTool "stale" or built-in "missed" |
| **GPU level** | Oculus-runtime-set throttle level 0-5 (higher = more headroom requested) | OVRMetricsTool, useful trend signal |
| **CPU level** | Same on CPU side | Same |
| **Memory (RSS)** | App's resident memory size | `adb shell dumpsys meminfo com.unitymcplab.campfireroom` |
| **Thermal level** | Oculus thermal state (NORMAL → NOMINAL → WARNING → CRITICAL) | OVRMetricsTool, app log warnings if WARNING+ |

## Pass / warn / fail criteria

For a **5-minute connected two-player session** with voice on:

| Status | Criteria |
|---|---|
| **PASS** | App GPU time < 8 ms 95th percentile (3 ms headroom from the 11.1 ms budget). Zero missed frames over the 5-minute window. GPU level stable at 2 or below. Memory growth < 50 MB over the window. Thermal stays NORMAL. |
| **WARN** | App GPU time spikes briefly to 9-11 ms but recovers within seconds. 1–2 missed frames during transitions (hosting, joining, voice connect). Memory growth 50–150 MB. Thermal occasionally NOMINAL. |
| **FAIL** | App GPU time over 11.1 ms for any sustained period. More than 2 missed frames after the initial connect. Memory growth > 150 MB (leak suspect). Thermal reaches WARNING. GPU level pegs at 4+ (runtime is throttling things to keep up). |

A WARN result is shippable for friend-test builds but worth investigating; a FAIL must be fixed before sharing.

## Common red flags

When perf goes wrong it's usually one of these:

- **Sudden GPU spike when a player joins** — usually remote avatar materials / mesh load. The `PolishRemoteAvatar` helper exists exactly to keep this cheap; verify it still runs.
- **GPU climbing slowly during a session** — possible particle system buildup (campfire embers if leaking), or shadow-caster count creeping up via runtime spawns. We have very few of these but always worth checking.
- **CPU spikes during voice transitions** — Photon Voice's encode/decode is normally fine; a spike on connect/disconnect can indicate a thread-pool stall worth filing.
- **Missed frames once per second** — typical signal of a GC stall. Likely an allocation in `Update` somewhere; not currently observed in CampfireVR but worth a `dotnet trace` if it appears.
- **Memory growing linearly** — leak. NGO + Relay are known not to leak in our usage; Photon Voice has had historical leak bugs on older versions. Pin the source via `dumpsys meminfo` at minute 0 and minute 30.

## High-risk scene items today

Specific render-cost items in CampfireRoom that are worth measuring individually if perf surprises us:

| Item | Risk | Quick mitigation if it's the culprit |
|---|---|---|
| **38 tree_01 pines** from Mountain Terrain pack | Vertex count not measured per-tree; if high-poly, could dominate GPU at scale | LOD tuning or replacing with simpler trees |
| **OakBigTree_Clearing** with ALP shader globals + WindZone | Wind-shader vertex animation cost on a 5-LOD tree | Disable wind on lowest LODs; reduce WindPulse if visible spikes |
| **DogCompanion SkinnedMesh** | Skinned-mesh skinning is the most expensive renderer in the scene | Already shadow-off + static atlas-mapped; if still heavy, bake to static mesh |
| **FireLight Hard shadows** | Hard shadows are cheaper than Soft but still a draw-call multiplier | Lower shadow distance / disable on distant objects |
| **GrassBreakup cross-quad cards (6 tufts)** | Transparent-cutout shader → overdraw cost when camera is close | Smaller card scale; or move further from camera; or switch to opaque + alpha-test |
| **CampfireRoom Atmosphere ambient** | Built-in RP ambient bake; if dirty after scene edits, can re-bake unnecessarily | `Window → Rendering → Lighting → Generate Lighting` after big environment changes only |

These are starting points for "where did the budget go" — don't pre-optimise.

## What's already done at the build-config level

These are baked-in choices that affect perf, set in `OculusSettings.asset` or `QuestBuildSetup.cs`:

| Setting | Value | Why |
|---|---|---|
| `FoveatedRenderingMethod` | `1` (Fixed FFR) | Reduces eye-periphery rendering cost ~10–30 % depending on scene; default for Quest 3 apps. Landed in App Lab compliance Slice 5. |
| `TargetQuest3` / `TargetQuest3S` | `1` / `1` | Declared support so Oculus runtime can pick optimal settings for current-gen hardware. Slice 5. |
| `TargetQuest2` | `1` | Keep older-hardware compatibility while scene budget allows. |
| `TargetQuestPro` | `0` | Discontinued device; not in our tester pool. |
| `PhaseSync` | `1` | Reduces input-to-photon latency. Stock-recommended on. |
| `OptimizeBufferDiscards` | `1` | Less framebuffer bandwidth. Stock-recommended on for Quest. |
| `LowOverheadMode` | `0` | Oculus recommends OFF for Vulkan (which we use). |
| `SubsampledLayout` | `0` | Pairs with FFR — slight quality gain at small bandwidth cost. Leaving OFF until we observe FFR is bandwidth-bound; enable in a follow-up if needed. |
| `SpaceWarp` | `0` | 45→90 Hz reprojection. Heavy to integrate, big visual side-effects on text panels; only worth it if we're locked over 90 Hz budget. We aren't. |
| `LateLatching` | `0` | Reduces input-to-photon latency further but adds threading complexity. Defer until measured need. |
| `targetSdkVersion` | `34` | Slice 1. Not perf-related; included for completeness. |
| `targetArchitectures` | `ARM64` | 64-bit only; Quest minimum. |
| `scriptingBackend` | `IL2CPP` | Required for Android 64-bit. |
| `graphicsAPIs` | `Vulkan, OpenGLES3` | Vulkan primary; GLES3 fallback for older runtimes. |
| Color space | `Linear` | Correct lighting math; standard for VR. |

## Recording a measurement

When you complete a perf pass, capture the numbers in `quest-logs/<date>/perf-<sessionId>.md` (not committed — it's part of the session log archive):

```
session: 2026-05-21 evening, two-headset, johan + <friend>
apk: CampfireVR-v0.1.3-suffix-20260520-2103.apk
length: 30 min connected
result: PASS

App GPU time: 6.2 ms median / 8.1 ms p95
App CPU time: 4.4 ms median / 5.9 ms p95
missed frames: 0
memory: 412 MB → 418 MB (+6 MB)
thermal: NORMAL throughout
GPU level: 2

notes:
- 1 GPU spike at min 12 when partner re-joined; recovered in 4 frames. Acceptable.
- Voice connect/disconnect: no observable CPU spikes.
```

That archive is what we point at if a future regression makes us ask "when was the last time we knew this passed".
