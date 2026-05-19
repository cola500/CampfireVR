using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Apply the Android bundleVersionCode from the CAMPFIREVR_VERSION_CODE env
// var before the build runs. Meta Horizon Store / App Lab requires every
// uploaded APK to have a strictly-increasing versionCode — uploading the
// same value twice (or a lower value) is rejected by the submission flow.
//
// Source of truth is scripts/build-quest.sh, which computes
// `git rev-list --count HEAD` and exports it as the env var before
// launching Unity batchmode. The integer always increases on every commit
// to main, so it's a free monotonic counter that doesn't need a separate
// state file.
//
// Applied in memory only — restored to the committed baseline value
// after the build so the on-disk ProjectSettings.asset never drifts.
// PreloadedAssetsGuard's post-build AssetDatabase.SaveAssets() runs at
// int.MaxValue (after this guard's post-build restore), so by the time
// the asset is serialized to disk the value is back to baseline.
//
// Editor-side iteration builds (no env var set) leave the committed
// value untouched. Only release builds via scripts/build-quest.sh, with
// CAMPFIREVR_VERSION_CODE exported, get the auto-bumped code in the
// produced APK.
public class VersionCodeGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 200;

    // Captured pre-build so we can restore in post-build. nullable so we
    // can tell "haven't modified yet" from "previous was 0".
    private static int? _previousCode;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        Apply();
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        Restore();
    }

    public static void Apply()
    {
        string raw = Environment.GetEnvironmentVariable("CAMPFIREVR_VERSION_CODE");
        if (string.IsNullOrEmpty(raw)) return;

        if (!int.TryParse(raw, out int code))
        {
            Debug.LogWarning($"[VersionCodeGuard] CAMPFIREVR_VERSION_CODE='{raw}' " +
                "is not an integer — leaving bundleVersionCode unchanged.");
            return;
        }
        if (code <= 0)
        {
            Debug.LogWarning($"[VersionCodeGuard] CAMPFIREVR_VERSION_CODE='{code}' " +
                "must be >= 1 — leaving bundleVersionCode unchanged.");
            return;
        }

        int previous = PlayerSettings.Android.bundleVersionCode;
        if (previous == code)
        {
            Debug.Log($"[VersionCodeGuard] bundleVersionCode already at {code}; no change.");
            return;
        }
        _previousCode = previous;
        PlayerSettings.Android.bundleVersionCode = code;
        Debug.Log($"[VersionCodeGuard] bundleVersionCode {previous} → {code} " +
            $"(temporary; will be restored to {previous} post-build).");
    }

    private static void Restore()
    {
        if (!_previousCode.HasValue) return;
        int current = PlayerSettings.Android.bundleVersionCode;
        int baseline = _previousCode.Value;
        PlayerSettings.Android.bundleVersionCode = baseline;
        _previousCode = null;
        Debug.Log($"[VersionCodeGuard] post-build: bundleVersionCode " +
            $"{current} → {baseline} (committed baseline restored).");
    }
}
