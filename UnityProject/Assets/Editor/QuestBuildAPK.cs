using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class QuestBuildAPK
{
    private const string OutputDir = "Builds";
    private const string DefaultName = "CampfireVR-remote-fika-test-v0.1.apk";

    [MenuItem("Tools/Quest Setup/Build Remote Fika APK")]
    public static void Build()
    {
        BuildTo(Path.Combine(OutputDir, DefaultName));
    }

    public static void BuildTo(string relativeApkPath)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[QuestBuildAPK] No scenes enabled in Build Settings. Run Tools/Quest Setup/Configure Project for Quest 3 first.");
            return;
        }

        Directory.CreateDirectory(OutputDir);
        string apkPath = relativeApkPath;
        if (File.Exists(apkPath)) File.Delete(apkPath);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        Debug.Log($"[QuestBuildAPK] Building {scenes.Length} scene(s) → {apkPath}");
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            string fullPath = Path.GetFullPath(apkPath);
            long sizeMB = (long)(summary.totalSize / (1024UL * 1024UL));
            Debug.Log($"[QuestBuildAPK] OK · {sizeMB} MB · {summary.totalTime} · {fullPath}");
        }
        else
        {
            Debug.LogError($"[QuestBuildAPK] FAILED · result={summary.result} · errors={summary.totalErrors}");
        }
    }
}
