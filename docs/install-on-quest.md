# Install CampfireVR on your Quest

A guide for getting CampfireVR running on a Meta Quest headset for the first time. Written for a friend who's technical-ish but doesn't necessarily know Unity, Android SDKs, or adb. Plan for 20–30 minutes the first time; subsequent installs are under a minute.

## Quick start

For someone who's done all this before:

1. **Plug** the Quest into your computer with a USB-C cable.
2. **Inside the headset** — accept the "Allow USB debugging?" popup if it appears.
3. **On your computer** — run one of these:
   - You have the repo cloned: `./scripts/build-quest.sh --install-only --launch`
   - You only have the APK file: `adb install -r CampfireVR-remote-fika-test-v0.1.apk`
4. **Put on the headset**, open **Apps → Unknown Sources** (top-right dropdown), click **CampfireVR**.
5. **Join room A** with the **B button** on your right controller. Done.

If anything in steps 1–5 is unfamiliar, read the full guide below.

## A — One-time Quest setup

You only do this once per headset.

### 1. Enable Developer Mode

Developer Mode lets your computer install apps onto the headset without going through the Meta Quest Store. It's free and reversible.

1. Open the **Meta Quest mobile app** on your phone (Apple App Store / Google Play).
2. Sign in with the Meta account your headset is paired to.
3. Tap **Menu → Devices**, select your headset.
4. Tap **Headset settings → Developer mode**, toggle it **on**.
5. If this is the first headset you've enabled Developer Mode on, the app asks you to register as a developer. Give it any organisation name (e.g. your initials + "cozy dev"). It's free; no business needed.

### 2. Allow USB debugging

When you connect the Quest to your computer for the first time after enabling Developer Mode, the headset shows a popup inside VR: **"Allow USB debugging?"**

- Put the headset on, look around for the popup, click **Allow** (or **Always allow from this computer** — saves you doing it every reboot).
- If you miss the popup the first time, disconnect and reconnect the USB cable; it pops back up.

### 3. Connect via USB-C

Use the USB-C cable that came with the Quest (or any USB-C 3.0 data cable — charging-only cables won't work). The "Link" cable Meta sells is overkill for installing apps; any data-capable USB-C cable is fine.

## B — Get the tools

You need a way for your computer to talk to the headset. Pick **one** of the three:

### Option 1 — Meta Quest Developer Hub (MQDH) — easiest, no terminal

Download from <https://developer.oculus.com/downloads/package/oculus-developer-hub-mac/> (or the Windows / Linux equivalent). Sign in with your Meta account. Plug in the Quest; it appears in MQDH's sidebar. Drag-and-drop APKs onto the device — that's it.

### Option 2 — Android Platform Tools (adb) — terminal, lightweight

Download from <https://developer.android.com/studio/releases/platform-tools>. Unzip wherever you like; the only file that matters is `adb`. Add the folder to your `PATH`, or call adb with its full path.

To test:
```sh
adb devices
# Should print:
#   List of devices attached
#   2G0YC5ZG20031Y    device
```

If the second line is empty, your USB cable or the Allow popup is the issue — see Troubleshooting below.

**Note for Johan's setup:** if you have Unity 6 installed via Unity Hub, you already have adb at:
```
/Applications/Unity/Hub/Editor/6000.4.7f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
```
No separate download needed.

### Option 3 — SideQuest — third-party GUI

<https://sidequestvr.com/> — free, friendly, drag-and-drop installer. Walks you through Developer Mode setup too if you skipped that step. Good middle ground between MQDH and adb.

## C — Get the APK

The APK is `CampfireVR-remote-fika-test-v0.1.apk` (about 86 MB). It's not on the Meta Quest Store yet — Johan builds it locally and shares it.

Ways to get the file:

- **Direct from Johan** — AirDrop, Discord, email, USB stick. Easiest.
- **From the repo, if you have the build tools** — clone <https://github.com/cola500/unity-mcp-lab>, install Unity 6.4 (`6000.4.7f1`), then `./scripts/build-quest.sh` to produce the APK locally. About 5 minutes of build time. Only do this if you actually want to make changes.

The APK is the same regardless of source — same package ID (`com.unitymcplab.campfireroom`), same version (1.0). Reinstalling over an existing install with `-r` just upgrades cleanly.

## D — Install the APK

Pick the method that matches the tool you set up in Part B.

### Method 1 — Script (if you cloned the repo)

```sh
cd unity-mcp-lab
./scripts/build-quest.sh --install-only --launch
```

What it does:
- Skips the Unity build (`--install-only`) — uses the APK already at `UnityProject/Builds/CampfireVR-remote-fika-test-v0.1.apk`.
- Runs `adb install -r` to upgrade-install onto the connected Quest.
- Launches the app immediately (`--launch`) so you can put on the headset right away.

If the APK isn't there yet, the script tells you to build first: `./scripts/build-quest.sh` (no flags). Or copy a pre-built APK into `UnityProject/Builds/` with the expected filename.

### Method 2 — Manual adb (no repo needed)

Save the APK somewhere convenient (Desktop, Downloads, wherever). Then:

```sh
adb install -r ~/Downloads/CampfireVR-remote-fika-test-v0.1.apk
# Output:
#   Performing Streamed Install
#   Success
```

The `-r` flag means "reinstall if already there" — needed for upgrades.

To launch the app via adb (optional — you can also launch from inside the headset):

```sh
adb shell monkey -p com.unitymcplab.campfireroom -c android.intent.category.LAUNCHER 1
```

### Method 3 — Meta Quest Developer Hub

Open MQDH. The Quest shows up in the left sidebar. Drag the `.apk` file onto it. Done. To launch, click the headset → Apps tab → CampfireVR → Launch.

## E — Launch and find the app

Once installed, put on the headset:

1. From the home menu, click **Apps** (bottom row).
2. Top-right of the Apps panel, click the dropdown that says **All** (or "App Library" depending on Quest OS version).
3. Select **Unknown Sources** — that's where side-loaded apps live. (Meta hides them by default; this is normal, not a bug.)
4. Click **CampfireVR**.

First launch takes ~5–15 seconds — you'll see a Unity splash, then the campfire scene fades in.

## F — First minute in headset

What you'll see when CampfireVR boots:

- A campfire at night, two stone seats around it, a forest backdrop, a small dog companion beside one of the seats.
- A world-space text panel in front of you with the legend (X host, B join, Y mode, etc.).
- A line that reads **`Room: A`** — that's the default room. You and your friend both join "A" and you're in the same campfire.

To **host**: press **left X** on your controller. Tell your friend "we're on A". The panel switches to **YOUR FIRE / Room: A / waiting for friend…**

To **join**: press **right B**. The panel reads **Joining room A…** and within a few seconds you're connected.

To **switch between Internet and Same Wi-Fi mode**: press **left Y**. The bottom line of the panel flips between `mode · Internet` and `mode · Same Wi-Fi`.

- **Internet** is what you want for two people in different houses. Uses Unity Relay (free).
- **Same Wi-Fi** is for two headsets in the same room sharing one router. Currently requires a manually baked IP — **don't pick this mode unless Johan has set up your build for it.** Stick with Internet.

To **change the room letter** (only needed if multiple pairs are testing at once): nudge the **right thumbstick** sideways. The room cycles A → B → ... → Z → A. Whoever you're meeting needs to land on the same letter.

To **recenter your seat** if the view feels off after putting the headset on: press **right A**.

## G — Troubleshooting

| Symptom | What's wrong | Fix |
|---|---|---|
| `adb devices` shows nothing or "unauthorized" | The "Allow USB debugging" popup wasn't accepted inside the headset | Put the headset on, look for the popup, click Allow. If you don't see it, unplug and replug the USB cable. |
| `[install] No authorised Quest connected` | Same as above, OR the Quest isn't plugged in, OR the cable doesn't carry data | Try a different USB-C cable (charging-only cables silently fail). Run `adb devices` to confirm. |
| `Failed to install … INSTALL_FAILED_UPDATE_INCOMPATIBLE` | Old version of the app installed under a different signing key | Uninstall first: `adb uninstall com.unitymcplab.campfireroom`, then install fresh. You'll lose any in-app state, which for this build is nothing. |
| Headset never shows the USB debugging popup | Developer Mode is off, or you've already approved this computer once | Check Developer Mode in the Meta Quest mobile app (see Part A.1). If it's on, you might be already authorised — try `adb devices` to confirm. |
| App not in **Unknown Sources** after install | Quest's app list hasn't refreshed yet | Restart the headset (hold power for 10 s → Restart). The app should appear after reboot. |
| Re-running `--install-only` while Editor is open | Editor doesn't block install (only build) | This is fine — `--install-only` skips the Unity build, so the Editor can stay open. Only `./scripts/build-quest.sh` (without `--install-only`) requires the Editor to be closed. |
| Black screen after launching | App crashed early; usually a missing-prefab error | Open `adb logcat -d \| grep -i unity` on your computer right after launching. If you see "missing prefab" errors, the APK was built without the Asset Store packs that the scene needs — get a fresh APK from Johan. |
| Voice doesn't come through | Microphone permission not granted, or Photon Voice cloud blip | First launch: the headset asks for mic permission once. Click Allow. If still no voice after both confirm a connection, both quit and relaunch — Photon usually catches up within 3 seconds of the second try. |

## H — Friendly notes for the first session

- **Headphones strongly recommended.** Quest speakers leak voice into the *other* Quest's microphone if both headsets are in earshot of each other (e.g. testing on the same couch). With headphones the spatial voice across the campfire works properly — you hear your friend from the seat across the fire, not from a tinny speaker.
- **Sit down in a real chair** that supports your arms for 20+ minutes. The campfire is a seated experience, not a stand-up game.
- **Internet mode** uses a free Unity Relay tier. No accounts to make, no settings to configure on your end.
- **There's no in-VR quit button yet.** When you're done, press the **Meta button** on the right controller to return to the Quest home, then close CampfireVR from the dock.
- **The app icon still says "CampfireRoom"** in some Quest OS versions — historical name from before the rebrand. Same APK, same package, just a stale label.
- **Room A is the no-touch default.** If you and your friend both launch and don't touch the stick, you'll both be on A and just need to press the right buttons (X to host, B to join). The room letter only matters if multiple pairs are testing at once.

## More info

- Project README: <https://github.com/cola500/unity-mcp-lab/blob/main/README.md>
- Full session protocol (the 20-minute "Remote Fika" structured hangout): [docs/remote-fika-test.md](remote-fika-test.md)
- Single-letter room flow visual verification: [docs/verification/join-flow.md](verification/join-flow.md)
- Build / CI plan if you want to build your own APKs: [docs/ci-cd-quest-build-plan.md](ci-cd-quest-build-plan.md)
