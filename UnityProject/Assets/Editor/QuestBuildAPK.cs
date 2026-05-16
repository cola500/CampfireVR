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
    // Sets each stone to an absolute Y derived deterministically from its
    // GameObject name, so different stones embed at different depths and
    // the row of kerbstones stops looking extruded with a single tool.
    // Seats stay shallower (still tall enough to sit on); decorative rocks
    // span a wider range.
    //
    // Re-runnable: target Y is a pure function of the name, so the same
    // stone always lands at the same depth across runs and machines.
    // Idempotent: stones already within StoneSkipTolerance of their target
    // are skipped.
    private const float SeatMinEmbed = 0.05f;          // 5 cm
    private const float SeatMaxEmbed = 0.08f;          // 8 cm — keeps top ≈ 0.42 m at the default 0.4 scale
    private const float RockMinEmbed = 0.06f;          // 6 cm
    private const float RockMaxEmbed = 0.12f;          // 12 cm
    private const float StoneSkipTolerance = 0.005f;   // 5 mm — re-run no-op band

    private const string SeatPrefix = "StoneSeat_";
    private const string RockPrefix = "rock_set_";

    [MenuItem("Tools/Quest Setup/Ground Stones")]
    public static void GroundStones()
    {
        int touched = 0, skipped = 0;
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            bool isSeat = go.name.StartsWith(SeatPrefix);
            bool isRock = go.name.StartsWith(RockPrefix);
            if (!isSeat && !isRock) continue;

            float minEmbed = isSeat ? SeatMinEmbed : RockMinEmbed;
            float maxEmbed = isSeat ? SeatMaxEmbed : RockMaxEmbed;
            float fraction = StableHashFraction(go.name);
            float targetY = -Mathf.Lerp(minEmbed, maxEmbed, fraction);

            var t = go.transform;
            if (Mathf.Abs(t.position.y - targetY) <= StoneSkipTolerance)
            {
                skipped++;
                continue;
            }

            var p = t.position;
            p.y = targetY;
            t.position = p;
            EditorUtility.SetDirty(go);
            touched++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[QuestBuildAPK] Grounded {touched} stone(s) with varied embed " +
                  $"(seats {SeatMinEmbed:F2}–{SeatMaxEmbed:F2} m, rocks {RockMinEmbed:F2}–{RockMaxEmbed:F2} m); " +
                  $"skipped {skipped} already at target.");
    }

    // Custom stable hash → fraction in [0, 1). string.GetHashCode() is
    // intentionally non-stable across .NET runtime versions, so we roll our
    // own to guarantee the same name maps to the same fraction on every
    // machine and re-launch — a stone keeps the same depth across runs.
    private static float StableHashFraction(string s)
    {
        int h = 17;
        for (int i = 0; i < s.Length; i++) h = unchecked(h * 31 + s[i]);
        uint u = (uint)h;
        return (u % 1000) / 1000f;
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
