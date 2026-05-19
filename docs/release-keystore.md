---
title: Release keystore — generation, backup, and signing setup
description: How to generate the Android release-signing keystore for CampfireVR, where to store it, how to back it up, and how Unity's build pipeline reads it via environment variables.
category: meta
status: stable
last_updated: 2026-05-19
sections:
  - Why we need a release keystore
  - One-time generation
  - Where to store the keystore
  - Backup — read this twice
  - Wiring the keystore into the build
  - Verifying the APK is release-signed
  - What happens if you lose the keystore
---

# Release keystore

Every APK that goes to the Meta Horizon Store (or App Lab / Early Access) must be signed with a release keystore that we own. Unity ships APKs signed with a generic "Android Debug" key by default — that's fine for sideload, but Meta's submission flow rejects debug-signed builds.

This doc covers generating the keystore, storing it, backing it up, and wiring it into `scripts/build-quest.sh` without committing it to the repo.

## Why we need a release keystore

The keystore is the cryptographic identity of CampfireVR on Android. Every future update we publish must be signed with the **same** key, or Android refuses to install the upgrade ("INSTALL_FAILED_UPDATE_INCOMPATIBLE"). For Store-distributed apps the same rule applies — Meta tracks the signing cert across uploads.

This means two things:

1. The keystore must be generated exactly **once** per app and reused forever.
2. Losing it is **permanent and irreversible** — see the last section.

## One-time generation

Run this on your dev machine. It does not touch the repo.

```sh
mkdir -p ~/.keystores
cd ~/.keystores

keytool -genkey -v \
    -keystore CampfireVR-release.keystore \
    -alias campfirevr \
    -keyalg RSA \
    -keysize 4096 \
    -validity 10000 \
    -storetype PKCS12
```

The command prompts for:

- A **store password** — make this strong and random. Save it in your password manager *immediately* under "CampfireVR keystore store password".
- A **key password** — Android's tooling treats this as separate from the store password, but you can use the same value safely (the security boundary that matters is the keystore file's confidentiality, not store-vs-key). Save it too.
- A **distinguished name** — `CN=<your name>, OU=<dept or hobby>, O=<entity>, L=<city>, ST=<state>, C=<country>`. For an indie / hobby project, use your real name and country. These get baked into the signing cert and are visible in `apksigner --print-certs` output but not in the Store listing.

`-validity 10000` is ~27 years. Meta has historically refused certs that expire within 25 years of submission, so generous validity matters.

`-storetype PKCS12` matches Android's modern default — easier to migrate to other tooling later than the legacy JKS format.

## Where to store the keystore

**Path on your dev machine:** `~/.keystores/CampfireVR-release.keystore`

Rationale:

- Outside the repo tree → no risk of `git add .` accident.
- Single canonical location → bash + Unity build hooks know exactly where to look.
- `.gitignore` adds `*.keystore`, `*.jks`, and `CampfireVR-release.*` as defensive guards — even if you accidentally drag the file into the working tree, git ignores it.

If you're on multiple dev machines, the keystore needs to live on each one. **Don't share over Slack / Discord / email** — copy it via an encrypted USB stick or a SSH/scp transfer.

## Backup — read this twice

You need **at least two independent copies** of this file, in two different locations. If your laptop's SSD dies and you only had the keystore there, the app is permanently un-upgradable.

Suggested setup:

| Copy | Where | Why |
|---|---|---|
| Working copy | `~/.keystores/CampfireVR-release.keystore` on dev machine | What the build pipeline reads. |
| Cold backup | Encrypted external SSD (e.g. APFS-encrypted, kept in a drawer) | Survives laptop loss / theft / SSD failure. |
| Password-manager attachment | 1Password / Bitwarden as a file attachment alongside the passwords | Survives losing the SSD too. Strong vault encryption protects it at rest. |

**Don't** put the keystore in:

- A public or private GitHub repo (anywhere — gists, sub-repos, none).
- Google Drive, Dropbox, iCloud without an extra encryption layer (cloud-side breach exposes the file).
- A plaintext file alongside the passwords. The keystore is encrypted by its store password, but if both leak together that protection is gone.

When you generate the keystore: **immediately** make the cold backup and the password-manager attachment, in that order, before doing any other work. Then delete the file's reference in your shell history (`history -d` or similar) so the file path isn't visible to anyone shoulder-surfing.

## Wiring the keystore into the build

Unity reads four env vars at build time via `Assets/Editor/Build/ReleaseSigningGuard.cs` (an `IPreprocessBuildWithReport` hook):

| Env var | Value |
|---|---|
| `CAMPFIREVR_KEYSTORE_PATH` | Absolute path to the keystore, e.g. `/Users/johanlindengard/.keystores/CampfireVR-release.keystore` |
| `CAMPFIREVR_KEYSTORE_PASS` | The store password you set during `keytool -genkey` |
| `CAMPFIREVR_KEY_ALIAS` | The alias, default `campfirevr` |
| `CAMPFIREVR_KEY_PASS` | The key password (often same as store password) |

Set them in your shell startup (`~/.zshrc` or `~/.profile`) for permanent local use, or in a `.envrc` (gitignored) if you use direnv:

```sh
# In ~/.zshrc — adapt the path to your username.
export CAMPFIREVR_KEYSTORE_PATH="$HOME/.keystores/CampfireVR-release.keystore"
export CAMPFIREVR_KEYSTORE_PASS="<paste from password manager>"
export CAMPFIREVR_KEY_ALIAS="campfirevr"
export CAMPFIREVR_KEY_PASS="<paste from password manager>"
```

Verify the vars are exported in the shell `build-quest.sh` runs from:

```sh
env | grep CAMPFIREVR_
# Expect four CAMPFIREVR_* lines.
```

If any are missing, the build falls back to the Unity debug keystore and prints a warning. That's the safe default — sideload still works, but Meta submission will refuse the APK.

The values are **never** written to `ProjectSettings.asset` on disk. `ReleaseSigningGuard.Apply()` sets them on `PlayerSettings.Android` in memory only, so the committed asset has no leak risk and no per-machine drift.

## Verifying the APK is release-signed

After a build, confirm the APK's signing cert isn't the Unity debug key:

```sh
apksigner=/Applications/Unity/Hub/Editor/6000.4.7f1/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/apksigner

$apksigner verify --print-certs UnityProject/Builds/CampfireVR-latest.apk
```

A debug-signed APK shows:

```
Signer #1 certificate DN: CN=Android Debug, O=Android, C=US
```

A release-signed APK shows your DN:

```
Signer #1 certificate DN: CN=Johan Lindengård, OU=CampfireVR, O=<entity>, L=<city>, ST=<state>, C=SE
```

The DN matches what you typed during `keytool -genkey`. If you see `CN=Android Debug` after setting the env vars, check the build log for `[ReleaseSigningGuard]` warnings — usually a typo'd path or missing password.

## What happens if you lose the keystore

If the keystore file is gone and you have no backup:

- **You cannot ever update the published app.** Meta and Android both treat a new keystore as a different app — existing users would have to uninstall and reinstall, which Meta's tooling won't permit for an unchanged package id.
- **You'd have to ship a new app entry** under a different package id (e.g. `com.unitymcplab.campfirevr2`), abandon the user reviews on the original, and treat it as a fresh app for Store visibility purposes.

The fix is prevention. Generate the keystore once, back it up twice, store the passwords in your password manager, then forget about it — the build pipeline handles the rest forever.
