using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class QuestBuildAPK
{
    private const string OutputDir = "Builds";
    private const string DefaultName = "CampfireVR-remote-fika-test-v0.1.apk";

    [MenuItem("Tools/Quest Setup/Build Remote Fika APK")]
    public static void Build()
    {
        BuildTo(Path.Combine(OutputDir, DefaultName));
    }

    // One-click in-Editor build → install → launch. The whole pipeline runs
    // without closing the Editor: Build() executes inside the same process
    // that holds the project lock (no batchmode lock conflict), then the
    // shell script is shelled out for the adb side (install + launch).
    //
    // The shell script's --install-only path auto-detects the newest
    // CampfireVR-*.apk in Builds/ by mtime, so the freshly-built APK at
    // the static DefaultName path is picked up automatically without us
    // having to pass --apk PATH.
    //
    // Use this for "tiny VR tweak → headset" iteration. For release-quality
    // builds (versioned filename + build-info.json with current git state)
    // close the Editor and run scripts/build-quest.sh from the terminal.
    [MenuItem("Tools/Quest Setup/Build + Deploy to Quest")]
    public static void BuildAndDeploy()
    {
        string apkRelative = Path.Combine(OutputDir, DefaultName);
        BuildTo(apkRelative);

        // BuildTo logs errors itself; bail if the APK doesn't exist on disk.
        string apkAbsolute = Path.GetFullPath(apkRelative);
        if (!File.Exists(apkAbsolute))
        {
            Debug.LogError($"[QuestBuildAPK] Build did not produce {apkAbsolute}; skipping install.");
            return;
        }

        // Locate the install script. Application.dataPath is .../UnityProject/Assets;
        // the repo root sits two levels up, and scripts/ lives there.
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        string script = Path.Combine(repoRoot, "scripts", "build-quest.sh");
        if (!File.Exists(script))
        {
            Debug.LogError($"[QuestBuildAPK] Install script not found at {script}; APK is built but not deployed.");
            return;
        }

        // Run synchronously so the user sees install output in the Editor
        // console before the menu returns. Install + launch is a few
        // seconds — fast enough not to need async/threading.
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{script}\" --install-only --launch",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (var proc = Process.Start(psi))
            {
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (!string.IsNullOrEmpty(stdout)) Debug.Log($"[QuestBuildAPK] install-script stdout:\n{stdout}");
                if (!string.IsNullOrEmpty(stderr)) Debug.LogWarning($"[QuestBuildAPK] install-script stderr:\n{stderr}");

                if (proc.ExitCode == 0)
                    Debug.Log("[QuestBuildAPK] Build + Deploy complete — check your headset.");
                else
                    Debug.LogError($"[QuestBuildAPK] Install script exited {proc.ExitCode}. APK is built at {apkAbsolute}; you can retry deployment from the terminal.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[QuestBuildAPK] Could not run install script: {e.Message}. APK is built at {apkAbsolute}; install manually with: scripts/build-quest.sh --install-only --launch");
        }
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

    // Drops a single dog instance from ithappy/Animals_FREE into the scene
    // as a static companion. The pack ships URP-shader materials which
    // render magenta in our Built-In RP scene, so we author our own warm
    // brown Standard material and reassign it to the dog's SkinnedMesh.
    // No AI, no audio — the prefab's Animator stays on, defaulting to the
    // BlendTree's idle state (subtle breathing). Shadow casting disabled,
    // CharacterController removed (no physics needed for a static prop).
    //
    // Idempotent: re-runs delete the previous DogCompanion before creating
    // a fresh one.
    private const string DogPrefabPath = "Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab";
    private const string DogCoatMaterialPath = "Assets/Materials/DogCoat.mat";
    private const string DogInstanceName = "DogCompanion";
    // Animals_FREE ships ONE shared atlas texture used by every animal — each
    // animal's UVs map to its region of the atlas. Re-using it on a Built-In
    // Standard material gives the dog its real coat colours without needing
    // any extra texture work or pack-package import.
    private const string DogAtlasTexturePath = "Assets/ithappy/Animals_FREE/Textures/Texture.png";

    [MenuItem("Tools/Quest Setup/Add Dog Companion")]
    public static void AddDogCompanion()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DogPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[QuestBuildAPK] Dog prefab not found at {DogPrefabPath}. Re-import Animals_FREE from Asset Store → My Assets.");
            return;
        }

        // Reset if previously placed.
        var existing = GameObject.Find(DogInstanceName);
        if (existing != null) Object.DestroyImmediate(existing);

        var dog = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        dog.name = DogInstanceName;

        // Beside StoneSeat_A, slightly behind it and outside the seat-to-fire
        // axis. Facing the fire (yaw 215° aims roughly toward origin from this
        // spot). Y=0 — paws line up with the ground plane.
        dog.transform.position = new Vector3(1.3f, 0f, 0.8f);
        dog.transform.rotation = Quaternion.Euler(0f, 215f, 0f);
        // Animals_FREE meshes are roughly real-world scale (1 unit = 1 m). A
        // 0.5× scale reads as a medium dog curled by the fire.
        dog.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        // Replace URP/missing shader with our own Built-In Standard material.
        var coat = GetOrCreateDogCoat();
        foreach (var smr in dog.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
        {
            var mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = coat;
            smr.sharedMaterials = mats;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.receiveShadows = false;
            EditorUtility.SetDirty(smr);
        }
        foreach (var mr in dog.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
        {
            var mats = new Material[mr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = coat;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            EditorUtility.SetDirty(mr);
        }

        // Static prop — strip the pack's runtime movement/AI scripts so the
        // dog doesn't try to wander or read input. The scripts have a
        // RequireComponent chain (MovePlayerInput → CreatureMover →
        // CharacterController), so a single pass leaves dependents behind.
        // Loop until no ithappy.Animals_FREE MonoBehaviour gets removed in a
        // full pass; each pass peels one layer off the dependency stack.
        // Animator (UnityEngine namespace) is always preserved so the
        // BlendTree's default idle state keeps playing.
        int prevCount = -1;
        for (int pass = 0; pass < 8 && prevCount != 0; pass++)
        {
            int removed = 0;
            foreach (var mb in dog.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            {
                if (mb == null || mb is Animator) continue;
                if (mb.GetType().Namespace != "ithappy.Animals_FREE") continue;
                var owner = mb.gameObject;
                int before = owner.GetComponents<MonoBehaviour>().Length;
                Object.DestroyImmediate(mb);
                if (owner.GetComponents<MonoBehaviour>().Length < before) removed++;
            }
            prevCount = removed;
        }
        // Now the CharacterController has no dependent scripts left.
        var cc = dog.GetComponentInChildren<CharacterController>();
        if (cc != null) Object.DestroyImmediate(cc);

        // Add subtle idle life — see DogIdleBehaviour.cs for the design.
        // Idempotent: skipped if a re-run finds the component already there.
        if (dog.GetComponent<DogIdleBehaviour>() == null)
            dog.AddComponent<DogIdleBehaviour>();

        EditorUtility.SetDirty(dog);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[QuestBuildAPK] Placed {DogInstanceName} at {dog.transform.position}, scale {dog.transform.localScale.x:F2}×, coat={coat.name}, idle behaviour attached.");
    }

    // Standalone menu: attaches DogIdleBehaviour to an existing DogCompanion
    // GameObject without destroying it. Use this when Johan has manually
    // adjusted the dog's pose and just wants to apply the idle behaviour
    // retroactively. Idempotent.
    [MenuItem("Tools/Quest Setup/Apply Dog Idle Behaviour")]
    public static void ApplyDogIdleBehaviour()
    {
        var dog = GameObject.Find(DogInstanceName);
        if (dog == null)
        {
            Debug.LogWarning($"[QuestBuildAPK] No {DogInstanceName} in scene. Run 'Add Dog Companion' first.");
            return;
        }
        if (dog.GetComponent<DogIdleBehaviour>() != null)
        {
            Debug.Log($"[QuestBuildAPK] {DogInstanceName} already has DogIdleBehaviour; nothing to do.");
            return;
        }
        dog.AddComponent<DogIdleBehaviour>();
        EditorUtility.SetDirty(dog);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[QuestBuildAPK] DogIdleBehaviour attached to {DogInstanceName}. Save the scene to persist.");
    }

    private static Material GetOrCreateDogCoat()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(DogCoatMaterialPath);
        if (mat == null)
        {
            var shader = Shader.Find("Standard");
            mat = new Material(shader) { name = "DogCoat" };
            AssetDatabase.CreateAsset(mat, DogCoatMaterialPath);
        }
        // Use the pack's shared atlas texture as the albedo. The dog's UVs
        // sample its region of the atlas, so we get the real authored coat
        // colours rather than a flat tint. White _Color lets the texture
        // through unaltered; matte smoothness avoids fire-flicker glints.
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(DogAtlasTexturePath);
        if (atlas != null) mat.SetTexture("_MainTex", atlas);
        else Debug.LogWarning($"[QuestBuildAPK] Dog atlas texture not found at {DogAtlasTexturePath} — DogCoat will render as flat colour. Re-import Animals_FREE.");
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.08f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
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
