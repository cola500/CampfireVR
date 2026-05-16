using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Adds one Big Oak Tree FREE prefab + a handful of cross-quad flower
// clusters to the campfire clearing. Atmosphere polish — meant to read
// as a calm Scandinavian summer clearing, not a fantasy meadow.
//
// Idempotent: re-running skips placement if the named objects already
// exist in the scene. Re-running the menu after editing the position /
// rotation / scale constants below regenerates that field cleanly via
// EditorUtility.SetDirty without touching anything else.
//
// Quest-friendly:
//   - Oak instance has shadow casting OFF (Quest shadow budget is already
//     tight with the realtime FireLight + Directional).
//   - Flower clusters use Standard-Cutout shader (one material, one
//     draw call per cluster — same pattern as the existing GrassTuft_*
//     cross-quads).
//   - No animation, no script, no per-frame logic.
//
// Vendor pack notes:
//   - ALP "Big Oak Tree FREE" — custom shaders (ALP/Surface Detail*,
//     ALP/Billboard Cross Wind, ALP/Cutout Translucency Wind). Verified
//     BiRP-compatible (SubShader tags = RenderType/Opaque only, no
//     UniversalPipeline tag). Runtime script ALP8310_ControllerStyle.cs
//     properly guards Editor refs with #if UNITY_EDITOR — IL2CPP safe.
//   - ALP "GrassFlowersFREE" — ships textures only (terrain-detail pack).
//     We use one of its flower textures (grassFlower04.tga) for the
//     cross-quad material rather than setting up Unity Terrain.
public static class OakAndFlowersSetup
{
    private const string OakPrefabPath   = "Assets/ALP_Assets/Big Oak Tree FREE/Prefabs/OakBigTree01_pr.prefab";
    private const string FlowerTexPath   = "Assets/ALP_Assets/GrassFlowersFREE/Textures/GrassFlowers/grassFlower04.tga";
    private const string FlowerMatPath   = "Assets/Materials/MeadowFlower.mat";

    private const string ForestParentPath  = "World/Environment/Forest";
    private const string TreesParentPath   = "World/Environment/Forest/Trees";
    private const string FlowersParentPath = "World/Environment/Forest/Flowers";

    private const string OakObjectName            = "OakBigTree_Clearing";
    private const string WindControllerObjectName = "OakWindController";

    // Subtle "evening breeze" tuning for the ALP wind shader. The vendor's
    // Reset() defaults (WindStrength=5, WindPulse=0.5, WindTurbulence=1.0,
    // BillboardWindIntensity=0.5) read as a storm in a cozy seated VR scene.
    // These values target "barely-noticeable canopy shimmer, no perceptible
    // trunk sway" — the tree should still feel rooted and heavy.
    //
    // The controller component drives shader globals (_GlobalWindIntensity,
    // _GlobalWindPulse, etc.) on every Update; the oak's leaf/billboard
    // materials sample those globals in their vertex shaders. Zero CPU cost
    // beyond the controller's tiny Update; the wind animation is GPU-side
    // and folded into the existing draw calls.
    //
    // Scope: ONLY the oak responds. The 38 tree_01 pines in the scene use
    // Unity's built-in Standard / Nature shaders, which don't sample these
    // _Global* values. Replacing the pine leaf material with an ALP shader
    // is unsafe — pine FBX lacks the vertex-color wind weights ALP expects,
    // and 38 trees × any motion overwhelms the cozy seated-VR budget. See
    // docs/oak-and-flowers-slice.md ("Why wind is not extended to other
    // trees") for the full investigation.
    private const float WindStrength           = 0.10f;   // ~2% of Reset default
    private const float WindPulse              = 0.30f;   // slow cycle
    private const float WindTurbulence         = 0.12f;   // gentle variance
    private const float WindRandomness         = 0.15f;
    private const float BillboardWindIntensity = 0.06f;

    // Oak placement: behind player A's seat, off to one side, far enough
    // that the canopy doesn't crowd the seated view but close enough to
    // frame the clearing in peripheral vision. Slight Y-rotation breaks
    // the prefab's default forward orientation so the trunk twist doesn't
    // mirror the existing tree_01 (22..) in obvious ways.
    private static readonly Vector3 OakPosition = new Vector3(-4.5f, 0f, 3.5f);
    private static readonly Vector3 OakEuler    = new Vector3(0f, 47f, 0f);
    private const float OakScale = 0.85f;

    // Flower clusters — 4 small, asymmetric placements:
    //   1. At the oak base (mossy roots feel)
    //   2. Behind StoneSeat_A (player A's peripheral vision)
    //   3. Near the fire-pit kerb on the far side
    //   4. Outer edge of clearing as a "sight-line ender" toward the trees
    // Each cluster's localScale is the visual size of the cross-quad pair.
    private static readonly (string name, Vector3 pos, Vector3 euler, float scale)[] FlowerClusters =
    {
        ("FlowerCluster_OakBase",   new Vector3(-3.7f, 0f,  3.2f), new Vector3(0f,  20f, 0f), 0.45f),
        ("FlowerCluster_SeatBack",  new Vector3( 1.95f, 0f, -0.55f), new Vector3(0f, 130f, 0f), 0.32f),
        ("FlowerCluster_Kerb",      new Vector3(-0.85f, 0f, -1.25f), new Vector3(0f,  60f, 0f), 0.30f),
        ("FlowerCluster_FarEdge",   new Vector3( 2.4f, 0f, -3.2f), new Vector3(0f, -35f, 0f), 0.38f),
    };

    [MenuItem("Tools/Quest Setup/Place Oak and Flowers")]
    public static void Apply()
    {
        var forestParent  = EnsureSceneGroup(ForestParentPath);
        var treesParent   = EnsureSceneGroup(TreesParentPath);
        var flowersParent = EnsureSceneGroup(FlowersParentPath);
        if (forestParent == null || treesParent == null || flowersParent == null)
        {
            Debug.LogError("[OakAndFlowersSetup] Could not resolve scene parents. Aborting.");
            return;
        }

        int oakAdded = PlaceOak(treesParent) ? 1 : 0;

        var mat = GetOrCreateFlowerMaterial();
        int flowersAdded = 0;
        if (mat != null)
        {
            foreach (var cluster in FlowerClusters)
            {
                if (PlaceFlowerCluster(flowersParent, mat, cluster.name, cluster.pos, cluster.euler, cluster.scale))
                    flowersAdded++;
            }
        }

        // Always re-tune the wind controller — constants above are the source
        // of truth, so re-running after edits propagates without needing to
        // delete the existing controller.
        bool windAdded = PlaceWindController(forestParent);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[OakAndFlowersSetup] Added {oakAdded} oak + {flowersAdded} flower cluster(s){(windAdded ? " + wind controller" : " (wind controller re-tuned)")}.");
    }

    // -- oak --------------------------------------------------------------

    private static bool PlaceOak(Transform parent)
    {
        var existing = parent.Find(OakObjectName);
        if (existing != null) return false;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OakPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[OakAndFlowersSetup] Oak prefab not found at {OakPrefabPath}.");
            return false;
        }

        var oak = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        oak.name = OakObjectName;
        oak.transform.localPosition = OakPosition;
        oak.transform.localRotation = Quaternion.Euler(OakEuler);
        oak.transform.localScale    = Vector3.one * OakScale;

        ConfigureShadowFlagsOff(oak);
        EditorUtility.SetDirty(oak);
        return true;
    }

    // -- wind controller --------------------------------------------------

    // Places (or re-tunes) the single ALP8310Controller in the scene. Wind
    // is a global system — one controller drives every ALP shader instance
    // via Shader.SetGlobalFloat, so only the oak's leaf/billboard materials
    // sample these values. The flower cross-quads use Standard-Cutout and
    // are unaffected, as desired.
    private static bool PlaceWindController(Transform forestParent)
    {
        bool created = false;
        var existing = forestParent.Find(WindControllerObjectName);
        ALP8310Controller ctrl;
        if (existing == null)
        {
            var go = new GameObject(WindControllerObjectName);
            go.transform.SetParent(forestParent, worldPositionStays: false);
            ctrl = go.AddComponent<ALP8310Controller>();
            created = true;
        }
        else
        {
            ctrl = existing.GetComponent<ALP8310Controller>();
            if (ctrl == null) ctrl = existing.gameObject.AddComponent<ALP8310Controller>();
        }

        ctrl.WindStrength            = WindStrength;
        ctrl.WindDirection           = 0f;
        ctrl.WindPulse               = WindPulse;
        ctrl.WindTurbulence          = WindTurbulence;
        ctrl.WindRandomness          = WindRandomness;
        ctrl.BillboardWindEnabled    = true;
        ctrl.BillboardWindIntensity  = BillboardWindIntensity;
        ctrl.SynchWindZone           = false;   // we drive globals directly
        ctrl.SynchTheVegetationEngine = false;
        ctrl.SynchMicrosplat         = false;

        EditorUtility.SetDirty(ctrl);
        EditorUtility.SetDirty(ctrl.gameObject);
        return created;
    }

    // -- flowers ----------------------------------------------------------

    private static Material GetOrCreateFlowerMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(FlowerMatPath);
        if (mat == null)
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[OakAndFlowersSetup] Standard shader not found.");
                return null;
            }
            EnsureAssetFolder("Assets/Materials");
            mat = new Material(shader) { name = "MeadowFlower" };
            AssetDatabase.CreateAsset(mat, FlowerMatPath);
        }

        // Standard shader cutout mode (rendering keyword + render queue).
        mat.SetFloat("_Mode", 1f);                // 0=Opaque, 1=Cutout
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        mat.SetFloat("_Cutoff", 0.5f);

        // Slight warm white — reads as a meadow wildflower without
        // competing with the campfire's amber light.
        mat.SetColor("_Color", new Color(0.96f, 0.94f, 0.88f, 1f));
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.08f);

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FlowerTexPath);
        if (tex != null)
        {
            mat.mainTexture = tex;
        }
        else
        {
            Debug.LogWarning($"[OakAndFlowersSetup] Flower texture not found at {FlowerTexPath}. Material created without main texture.");
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
    }

    private static bool PlaceFlowerCluster(Transform parent, Material mat, string name, Vector3 pos, Vector3 euler, float scale)
    {
        var existing = parent.Find(name);
        if (existing != null) return false;

        var cluster = new GameObject(name);
        cluster.transform.SetParent(parent, worldPositionStays: false);
        cluster.transform.localPosition = pos;
        cluster.transform.localRotation = Quaternion.Euler(euler);
        cluster.transform.localScale    = Vector3.one;

        // Two perpendicular quads — cross-card billboarding (no actual
        // billboard component, just two static quads). Same pattern as
        // the existing GrassTuft_* objects.
        CreateFlowerQuad(cluster.transform, "QuadA", mat, scale, yawDeg:  0f);
        CreateFlowerQuad(cluster.transform, "QuadB", mat, scale, yawDeg: 90f);

        EditorUtility.SetDirty(cluster);
        return true;
    }

    private static void CreateFlowerQuad(Transform parent, string name, Material mat, float scale, float yawDeg)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        // CreatePrimitive adds a MeshCollider on a Quad — strip it. Flowers
        // don't need physics + the collider would clip seats / hands.
        var col = q.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        q.transform.SetParent(parent, worldPositionStays: false);
        // Quad mesh is in XY plane with pivot at center. Lift Y so the
        // quad's base sits on the ground (y=0 ground plane).
        q.transform.localPosition = new Vector3(0f, scale * 0.5f, 0f);
        q.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
        q.transform.localScale    = new Vector3(scale, scale, scale);

        var mr = q.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial      = mat;
            mr.shadowCastingMode   = ShadowCastingMode.Off;
            mr.receiveShadows      = false;
        }
    }

    // -- helpers ----------------------------------------------------------

    // Walks a slash-separated scene path, creating empty group GameObjects
    // along the way for any segment that doesn't already exist.
    private static Transform EnsureSceneGroup(string slashPath)
    {
        var segments = slashPath.Split('/');
        Transform current = null;

        // Root segment: search at scene root.
        var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            if (root.name == segments[0]) { current = root.transform; break; }
        }
        if (current == null)
        {
            var rootGo = new GameObject(segments[0]);
            current = rootGo.transform;
        }

        for (int i = 1; i < segments.Length; i++)
        {
            var child = current.Find(segments[i]);
            if (child == null)
            {
                var go = new GameObject(segments[i]);
                go.transform.SetParent(current, worldPositionStays: false);
                child = go.transform;
            }
            current = child;
        }

        return current;
    }

    private static void ConfigureShadowFlagsOff(GameObject go)
    {
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            EditorUtility.SetDirty(mr);
        }
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
        var parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
        var leaf = Path.GetFileName(assetFolder);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
