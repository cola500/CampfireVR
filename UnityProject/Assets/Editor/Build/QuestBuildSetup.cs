using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

public static class QuestBuildSetup
{
    [MenuItem("Tools/Quest Setup/Configure Project for Quest 3")]
    public static void Configure()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        var ng = NamedBuildTarget.Android;
        PlayerSettings.SetScriptingBackend(ng, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetApiCompatibilityLevel(ng, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
        {
            GraphicsDeviceType.Vulkan,
            GraphicsDeviceType.OpenGLES3,
        });
        // Canonical product naming. The Android package identifier is
        // intentionally NOT renamed — it's part of every installed APK's
        // upgrade chain, and changing it would force every friend to
        // uninstall before next install. See docs/project-rename-notes.md.
        PlayerSettings.companyName = "CampfireVR";
        PlayerSettings.productName = "CampfireVR";
        PlayerSettings.SetApplicationIdentifier(ng, "com.unitymcplab.campfireroom");

        const string scenePath = "Assets/Scenes/CampfireRoom.unity";
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == scenePath))
        {
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        bool xrAssigned = AssignOculusLoader();
        AssetDatabase.SaveAssets();
        Debug.Log($"[QuestBuildSetup] Done. Oculus loader assigned: {xrAssigned}");
    }

    private static bool AssignOculusLoader()
    {
        var manager = GetOrCreateAndroidManager();
        if (manager == null) return false;
        return XRPackageMetadataStore.AssignLoader(manager, "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Android);
    }

    private static XRManagerSettings GetOrCreateAndroidManager()
    {
        var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (general != null && general.AssignedSettings != null)
            return general.AssignedSettings;

        if (!AssetDatabase.IsValidFolder("Assets/XR"))
            AssetDatabase.CreateFolder("Assets", "XR");

        const string perBTPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        var perBT = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(perBTPath);
        if (perBT == null)
        {
            perBT = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perBT, perBTPath);
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBT, true);
        }

        var androidSettings = perBT.SettingsForBuildTarget(BuildTargetGroup.Android);
        if (androidSettings == null)
        {
            androidSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
            androidSettings.name = "Android Settings";
            var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            manager.name = "Android Managers";
            androidSettings.Manager = manager;

            AssetDatabase.AddObjectToAsset(androidSettings, perBT);
            AssetDatabase.AddObjectToAsset(manager, perBT);
            perBT.SetSettingsForBuildTarget(BuildTargetGroup.Android, androidSettings);

            EditorUtility.SetDirty(perBT);
            AssetDatabase.SaveAssets();
        }

        return androidSettings.Manager;
    }
}
