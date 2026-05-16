using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
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

    // Separate menu: force legacy input handling. Run this once after XRI
    // (or any package that pulls com.unity.inputsystem) is added — Unity
    // will reload assemblies, then `Build Remote Fika APK` works.
    //
    // Why not inline in BuildTo: toggling activeInputHandler mid-build
    // triggers a script recompile while the build pipeline already has
    // Editor assemblies in flight, producing
    //   "script class layout is incompatible between editor and player".
    // Doing it as a discrete step lets Unity settle before BuildPlayer runs.
    [MenuItem("Tools/Quest Setup/Force Legacy Input Handling")]
    public static void ForceLegacyInputHandlingMenu() => ForceLegacyInputHandling();

    // All rocks in CampfireRoom (kerbstones, stone seats, perimeter stones)
    // share the same Mountain Terrain rock_set FBX, whose pivot sits at the
    // mesh bottom. Placed at world Y=0 they line up exactly with the Ground
    // plane and read as "placed on top" instead of "embedded in the floor".
    // Lowers their Y by StoneEmbedDepth so the base dips below the ground.
    // Idempotent: skips rocks already below SkipBelowY.
    private const float StoneEmbedDepth = 0.08f;
    private const float StoneSkipBelowY = -0.05f;
    private static readonly string[] StonePrefixes = { "rock_set_", "StoneSeat_" };

    [MenuItem("Tools/Quest Setup/Ground Stones")]
    public static void GroundStones()
    {
        int touched = 0, skipped = 0;
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!StonePrefixes.Any(p => go.name.StartsWith(p))) continue;
            var t = go.transform;
            if (t.position.y <= StoneSkipBelowY) { skipped++; continue; }
            var p2 = t.position;
            p2.y -= StoneEmbedDepth;
            t.position = p2;
            EditorUtility.SetDirty(go);
            touched++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[QuestBuildAPK] Grounded {touched} stone(s) by {StoneEmbedDepth:F2} m; skipped {skipped} already-embedded.");
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

    private static void ForceLegacyInputHandling()
    {
        var settings = Resources.FindObjectsOfTypeAll<PlayerSettings>().FirstOrDefault();
        if (settings == null) { Debug.LogWarning("[QuestBuildAPK] PlayerSettings not found; skipping input-handler fix."); return; }

        var so = new SerializedObject(settings);
        var prop = so.FindProperty("activeInputHandler");
        if (prop == null) { Debug.LogWarning("[QuestBuildAPK] activeInputHandler property not found; skipping."); return; }

        if (prop.intValue == 0) return;
        int before = prop.intValue;
        prop.intValue = 0; // 0 = Input Manager (Old), 1 = Input System (New), 2 = Both
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"[QuestBuildAPK] activeInputHandler {before} → 0 (legacy) before build.");
    }
}
