---
title: Privacy policy — draft
description: Plain-language description of every data path CampfireVR touches. Draft only — not yet hosted at a public URL, not yet legally reviewed.
category: meta
status: draft
last_updated: 2026-05-19
sections:
  - About this draft
  - What CampfireVR is
  - Data we process while you use the app
  - What we deliberately do NOT collect
  - Third parties
  - Retention
  - Sharing your data back with us
  - Children
  - Changes to this policy
  - Open questions to resolve before publishing
---

# CampfireVR — privacy policy (DRAFT)

> **Status:** this is a working draft. It is not hosted at a public URL, has not been reviewed by anyone with legal training, and a few facts about third-party data handling still need verification before this can become the canonical privacy policy. The list of unresolved items is at the bottom. Treat this as the engineer's honest description of what the app does — useful for shaping the published version, not the published version itself.

## About this draft

The point of this document is to name every data path CampfireVR actually has, in plain language, so that:

1. When we publish a real privacy policy (probably as a GitHub Pages site on the project's repo before any Meta Horizon Store submission), it is grounded in reality rather than copy-pasted boilerplate.
2. Future-Johan can quickly see what changed when a new feature adds a new data path.
3. The Meta dashboard's "Data Use" form gets answered accurately on submission day.

This draft assumes a **13+** age-group self-certification (we do not knowingly support users under 13). That choice keeps us out of COPPA scope and avoids the Meta Platform SDK `Get Age Category` integration that mixed-age apps would require. See `docs/meta-store-readiness-audit.md` for the rationale.

## What CampfireVR is

CampfireVR is a hobby social-VR app for Meta Quest 3. Two players join a shared virtual campfire, sit on stones around the fire, and talk. The whole experience is two heads + two hands + voice. There are no avatars, profiles, friend lists, accounts to create, leaderboards, in-app purchases, or persistent worlds.

It is built by **Johan Lindengård** as a personal project. The contact email for anything in this document is **johan@jaernfoten.se**.

## Data we process while you use the app

### Voice audio

When you press **left X (host)** or **right B (join)**, CampfireVR connects to **Photon Voice 2** (operated by Exit Games GmbH). Your Meta Quest microphone captures audio, the app streams it to Photon's cloud servers, and the servers route it to the other player in your room. The other player's audio comes back the same way.

- CampfireVR does **not** record or save your voice locally. Once the audio leaves the headset it is gone, as far as our app is concerned.
- Photon's servers handle the routing. Their privacy policy is at <https://www.photonengine.com/en-US/Photon/Privacy-Policy> — please read it for what they do with the audio in transit. We don't have evidence that they retain it after routing, but verify with Photon's docs before publication.
- When you long-press **left Y** to stop, or open the Meta system menu, voice transmission stops. (`AppLifecycle.cs` mutes `Recorder.TransmitEnabled` on `OnApplicationFocus(false)` — landed in App Lab compliance sprint Slice 4. The mic stream stays initialised so voice resumes instantly on focus regain; nothing reaches Photon's cloud while focus is lost. Headset verification of the actual Meta-menu behaviour is still pending — see open question #8 below.)

### Multiplayer session metadata

To meet a friend across the public internet, the app uses two services together:

- **Unity Services / Unity Relay** brokers the network connection. When you host, the app asks Unity for a Relay allocation and gets back a short alphanumeric join code. When you join, the app exchanges the join code for the same allocation.
- **Unity Authentication** signs you in with **anonymous authentication** — there is no username, no password, no email. Unity gives back an anonymous `PlayerId` that's tied to this install of the app. This ID is stored on the device by Unity's SDK and reused across sessions so the same install keeps the same ID.

  > **Important nuance:** anonymous authentication is not the same as "no identifier exists." Unity Authentication does create and store a persistent identifier per install. We never read it, log it, or send it anywhere ourselves, but Unity could theoretically correlate sessions from the same install in their server logs. Verify Unity's published retention policy before publication.

- The Relay join code is **shared between host and joiner** via a **Photon room property** (key `rc`). Both players are already in the same Photon voice room (named by a single letter A–Z), and we use Photon's room property mechanism to broker the Relay code so the joiner can connect without manually typing it. This means Photon's servers briefly see the Relay join code as a room property string.
- Position data — head pose, hand poses, button events — flows over Unity Relay between the two players for the duration of the session. None of this is recorded by us; it lives only in the network stream.

### Local debug logs on the headset

For diagnosing problems during friend tests, CampfireVR writes a structured log to the headset's app storage:

- Location: `/sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/campfirevr-log-YYYYMMDD-HHMMSS.jsonl`.
- Contents: timestamped events for app start, mode/room changes, host/join attempts, Relay allocation, voice connection state, networking connect/disconnect, errors, manual markers. **No voice audio, no controller-input recordings, no continuous frame data, no user identifiers beyond what Meta's runtime already exposes** (device model, install mode).
- Size: each log file caps at **5 MB**, the **10 most-recent** files are kept, older ones are deleted automatically. No manual cleanup needed.
- These logs **never leave the headset on their own**. The only way they reach us is if you intentionally run `adb pull` on a computer you've connected the Quest to, and choose to send the resulting files to johan@jaernfoten.se. Without that explicit action, the files sit on your headset and eventually get rotated out.

### What's in your operating system, not us

Meta's Quest runtime (the OS the app runs in) collects telemetry that our app has no control over — system performance metrics, crash reports, store activity, etc. Their privacy policy at <https://www.meta.com/legal/privacy-policy/> describes what they collect at the platform level. Our app does not add to it and does not see it.

## What we deliberately do NOT collect

This list is verified by code search at the time of this draft (every claim here is checkable in the public repo):

- **No analytics SDK.** Not Unity Analytics, not Google Firebase, not Segment, not anything.
- **No telemetry sent from CampfireVR to anyone except Photon (voice) and Unity (Relay/Auth).**
- **No advertising identifiers.** We do not query Android's advertising ID and do not link anything to Meta's user ID.
- **No `PlayerPrefs`.** The app does not store any user preferences in Unity's local-storage mechanism. (Anonymous auth's PlayerId is stored by Unity's SDK, not by us — see the nuance above.)
- **No location data.** No GPS, no IP geolocation, no Wi-Fi triangulation, no Bluetooth scanning.
- **No camera or passthrough data.** The current build is pure VR (not mixed reality); the Quest's cameras are not accessed.
- **No biometric data.** No eye tracking, no face tracking, no hand-tracking-skeleton retention.
- **No purchase or payment data.** There is no monetisation in CampfireVR.
- **No friends list, no contacts, no social graph.** We don't access Meta's friends API.

## Third parties

CampfireVR uses three external services. Each has its own privacy policy that you should read:

| Service | Operator | What they handle | Privacy policy |
|---|---|---|---|
| Photon Voice 2 | Exit Games GmbH | Voice audio routing between the two players in a room, and the room property used to broker Relay codes | <https://www.photonengine.com/en-US/Photon/Privacy-Policy> |
| Unity Authentication | Unity Technologies | Anonymous sign-in token; persistent PlayerId per install | <https://unity.com/legal/privacy-policy> |
| Unity Relay (Multiplayer Service) | Unity Technologies | NAT-traversal routing for the two players' position-sync traffic | <https://unity.com/legal/privacy-policy> |

CampfireVR does not share any data with anyone else.

## Retention

| Data | Retention by us | Notes |
|---|---|---|
| Voice audio | None. Never recorded. | Photon may retain audio in transit per their own policy; we do not. |
| Multiplayer session metadata (Relay codes, room properties) | None. Lives only for the duration of the session and the brief window Photon/Unity hold it during brokering. | Photon's room-property lifetime is until the room closes (single-digit minutes after the last player leaves). |
| Anonymous PlayerId | Stored locally by Unity's SDK on the device. | We never read it, log it, or transmit it ourselves. Unity may retain a server-side copy per their policy. |
| Local debug logs | Stored on the headset until rotated out (5 MB × 10 files = max ~50 MB on disk). | Never uploaded by the app. If you send them to us by `adb pull` + email, we keep them only as long as needed to diagnose your bug report (typically a few weeks). |

## Sharing your data back with us

We only receive data from you if **you** choose to send it — typically a `debug-logs/*.jsonl` file you pulled with `adb pull` and emailed to johan@jaernfoten.se. We don't have a pipeline that pulls anything automatically.

To request that we delete debug logs you've shared with us, email **johan@jaernfoten.se** with the subject "delete my CampfireVR logs". We will confirm deletion within 14 days. There is nothing else to delete (anonymous PlayerIds and voice traffic are not held by us, so any deletion of those needs to go to Unity / Photon directly).

## Children

CampfireVR is intended for users **13 and older**. We do not knowingly collect data from anyone under 13. If we learn that a user under 13 has used the app and sent us logs containing identifying information, we will delete those logs.

This corresponds to the **13+** age-group self-certification on the Meta dashboard.

## Changes to this policy

When the published policy changes, we will:

- Update the `last_updated` field at the top of this draft.
- Note the substantive change in a new "## Change history" section at the bottom (added when the first published version ships).
- Keep older versions accessible from the project's git history at <https://github.com/cola500/CampfireVR>.

## Open questions to resolve before publishing

The honest list. Each of these needs a clear answer before the draft can be turned into a hosted, linkable privacy policy.

1. **Photon Voice 2 retention.** Does Photon's voice service log audio packets or only route them in-memory? Their privacy policy is general — find or request a specific answer for the Voice product.
2. **Photon room-property retention.** How long does Photon retain custom room properties (like our `rc` Relay join code) after the room closes? Their docs reference "minutes after the last player leaves" but the exact value should be verifiable.
3. **Unity anonymous auth PlayerId retention.** How long does Unity retain a server-side copy of the anonymous PlayerId after the user uninstalls the app? Section 8 of Unity's privacy policy mentions retention "as long as necessary" — get a specific window if possible.
4. **Unity Relay session-metadata retention.** Similar question for the join codes and session names — how long do they persist in Unity's logs after the session ends?
5. **AndroidManifest permission audit.** Run `aapt2 dump permissions Builds/CampfireVR-latest.apk` and confirm every declared permission is justified in this draft. Expected: `RECORD_AUDIO` (voice), `INTERNET` + `ACCESS_NETWORK_STATE` (Relay/Photon), Oculus-specific permissions (HMD, hand tracking — even if unused by the app, the SDK might declare them). Document any surprises.
6. **Hosting location.** GitHub Pages on `cola500/CampfireVR` is the planned hosting recipe — verify the chosen URL stays stable and that the page renders with the current `last_updated` field. The Meta dashboard wants a single URL that doesn't move.
7. **Legal review.** A 15-minute look from someone with privacy-policy experience before publishing. Not strictly required for App Lab / Early Access submissions but cheap insurance against language that overpromises or underdiscloses.
8. **System-menu mic mute — headset verification.** Slice 4 has landed (`AppLifecycle.cs` mutes `Recorder.TransmitEnabled` on focus loss). The code path is in place but full headset verification — open Meta menu mid-session, confirm via debug log that `app_focus_lost` fires with `voice_transmit_muted: true`, and confirm via the second tester that no audio reaches them while the menu is open — is still pending Johan's next two-headset session.
9. **Logged "device_name" field.** `DebugLogger` logs `SystemInfo.deviceName` on every `app_started` event. On Quest this is usually a generic string ("Oculus Quest 3"), but if Meta's runtime ever exposes a user-set device name we'd be quietly capturing it. Verify what `deviceName` actually contains on a clean install before publishing.
10. **Update cadence claim.** This draft doesn't promise a review cadence. Decide whether to commit to an annual review or "we update when something changes" — the latter is more honest for a hobby project.

---

When all ten are resolved, this draft becomes the hosted `privacy.html` (or equivalent). Until then, it stays as a working document tracking the gap between intent and verification.
