using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Configure Android release-keystore signing from environment variables so
// the keystore file + passwords stay out of the repo (and out of the
// committed ProjectSettings.asset).
//
// Required env vars for release signing:
//   CAMPFIREVR_KEYSTORE_PATH  — absolute path to the .keystore file
//   CAMPFIREVR_KEYSTORE_PASS  — store password
//   CAMPFIREVR_KEY_ALIAS      — key alias (typically "campfirevr")
//   CAMPFIREVR_KEY_PASS       — key password (often same as store password)
//
// If CAMPFIREVR_KEYSTORE_PATH is unset, falls back to Unity's debug
// keystore. Debug-signed APKs install fine on sideloaded Quest devices
// but Meta Horizon Store / App Lab review rejects them.
//
// Applied in-memory only — never written to ProjectSettings.asset on
// disk, so the committed asset has no leak risk and no per-machine drift.
// See docs/release-keystore.md for keystore generation + backup strategy.
public class ReleaseSigningGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 100;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        Apply();
    }

    public static void Apply()
    {
        string path  = Environment.GetEnvironmentVariable("CAMPFIREVR_KEYSTORE_PATH");
        string sPass = Environment.GetEnvironmentVariable("CAMPFIREVR_KEYSTORE_PASS");
        string alias = Environment.GetEnvironmentVariable("CAMPFIREVR_KEY_ALIAS");
        string kPass = Environment.GetEnvironmentVariable("CAMPFIREVR_KEY_PASS");

        if (string.IsNullOrEmpty(path))
        {
            PlayerSettings.Android.useCustomKeystore = false;
            Debug.LogWarning("[ReleaseSigningGuard] No CAMPFIREVR_KEYSTORE_PATH set — " +
                "Quest build will use the Unity debug keystore. Debug-signed APKs " +
                "install fine on sideloaded devices but Meta Horizon Store / App Lab " +
                "rejects them. See docs/release-keystore.md to set up release signing.");
            return;
        }

        if (!File.Exists(path))
        {
            PlayerSettings.Android.useCustomKeystore = false;
            Debug.LogError($"[ReleaseSigningGuard] CAMPFIREVR_KEYSTORE_PATH={path} " +
                "does not exist on disk. Falling back to debug keystore. " +
                "Did you move the keystore? See docs/release-keystore.md.");
            return;
        }

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = path;
        PlayerSettings.Android.keystorePass = sPass ?? string.Empty;
        PlayerSettings.Android.keyaliasName = alias ?? "campfirevr";
        PlayerSettings.Android.keyaliasPass = kPass ?? string.Empty;
        Debug.Log($"[ReleaseSigningGuard] Signing with release keystore " +
            $"{Path.GetFileName(path)} (alias: {PlayerSettings.Android.keyaliasName}).");
    }
}
