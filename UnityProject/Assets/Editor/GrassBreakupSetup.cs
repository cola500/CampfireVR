using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Sparse grass tufts as ground breakup. Uses Terra's "Grass Flower 7"
// PNG as a transparent cutout card. Six hand-picked positions give a
// "lived-in clearing" feel without turning the floor into a lawn. No
// grass on the seat-to-fire axis (X), nothing in the central fire ring.
//
// Each tuft is a small "X" of two crossed Quads (= 4 triangles). Six
// tufts = 24 triangles added to the scene. Crossed quads solve the
// back-face culling problem of a single quad: Standard shader is
// single-sided, so a single quad disappears when viewed from behind.
// Two perpendicular quads guarantee at least one front face is visible
// from any direction.
//
// Idempotent: re-run rebuilds the tufts in place without duplicating.
// Reversible via "Remove Grass Breakup" menu.
public static class GrassBreakupSetup
{
    private const string MaterialPath = "Assets/Materials/GrassBreakup.mat";
    private const string GrassTexturePath = "Assets/Terra/Example/Textures/Grass Flower 7.png";
    private const string ParentName = "GrassBreakup";

    private struct TuftSpec
    {
        public Vector3 position;
        public float yRotation;
        public Vector3 scale;
    }

    // Six hand-tuned spots. Asymmetric on purpose. None on the
    // seat-axis (X axis), none inside the fire-pit kerb (~2 m radius).
    private static readonly TuftSpec[] Tufts = new TuftSpec[]
    {
        new TuftSpec { position = new Vector3( 2.5f, 0.25f,  2.5f), yRotation =  37f, scale = new Vector3(0.45f, 0.50f, 1f) },
        new TuftSpec { position = new Vector3(-2.8f, 0.22f,  2.2f), yRotation = 142f, scale = new Vector3(0.40f, 0.44f, 1f) },
        new TuftSpec { position = new Vector3( 1.8f, 0.20f, -2.8f), yRotation = 261f, scale = new Vector3(0.50f, 0.40f, 1f) },
        new TuftSpec { position = new Vector3(-2.5f, 0.28f, -2.5f), yRotation =  82f, scale = new Vector3(0.55f, 0.56f, 1f) },
        new TuftSpec { position = new Vector3( 4.0f, 0.30f,  4.5f), yRotation = 198f, scale = new Vector3(0.60f, 0.60f, 1f) },
        new TuftSpec { position = new Vector3(-3.5f, 0.25f, -4.0f), yRotation =  19f, scale = new Vector3(0.50f, 0.50f, 1f) },
    };

    [MenuItem("Tools/Quest Setup/Apply Grass Breakup")]
    public static void Apply()
    {
        var mat = GetOrCreateMaterial();
        if (mat == null) return;

        // Wipe previous run's parent so we don't pile up duplicates.
        var oldParent = GameObject.Find(ParentName);
        if (oldParent != null) Object.DestroyImmediate(oldParent);

        var parent = new GameObject(ParentName);
        parent.transform.position = Vector3.zero;

        int created = 0;
        for (int i = 0; i < Tufts.Length; i++)
        {
            var spec = Tufts[i];

            // Empty parent — positions / rotates the whole tuft as one.
            var tuft = new GameObject($"GrassTuft_{i + 1:00}");
            tuft.transform.SetParent(parent.transform, worldPositionStays: false);
            tuft.transform.localPosition = spec.position;
            tuft.transform.localRotation = Quaternion.Euler(0f, spec.yRotation, 0f);
            tuft.transform.localScale = spec.scale;

            // Two crossed Quads at local Y-rotations 0° and 90°. Together they
            // form an "X" shape so at least one front-face is visible from any
            // camera direction — fixes the single-quad disappearing-when-
            // viewed-from-behind problem with Standard shader.
            CreateCrossQuad(tuft.transform, "Card_A", 0f, mat);
            CreateCrossQuad(tuft.transform, "Card_B", 90f, mat);

            EditorUtility.SetDirty(tuft);
            created++;
        }

        EditorUtility.SetDirty(parent);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[GrassBreakupSetup] Created {ParentName} with {created} grass tuft(s).");
    }

    [MenuItem("Tools/Quest Setup/Remove Grass Breakup")]
    public static void Remove()
    {
        var parent = GameObject.Find(ParentName);
        if (parent == null)
        {
            Debug.Log("[GrassBreakupSetup] Nothing to remove.");
            return;
        }
        Object.DestroyImmediate(parent);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[GrassBreakupSetup] Removed {ParentName}.");
    }

    private static void CreateCrossQuad(Transform parent, string name, float yRotation, Material mat)
    {
        var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        card.name = name;
        card.transform.SetParent(parent, worldPositionStays: false);
        card.transform.localPosition = Vector3.zero;
        card.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
        card.transform.localScale = Vector3.one;

        // Drop the auto-added MeshCollider — no physics needed.
        var col = card.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        var mr = card.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        EditorUtility.SetDirty(card);
    }

    private static Material GetOrCreateMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[GrassBreakupSetup] Standard shader not found.");
                return null;
            }
            mat = new Material(shader) { name = "GrassBreakup" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        // Standard shader in Cutout mode = alpha-tested. Quest-cheap, no
        // sorting issues across the scene.
        mat.SetFloat("_Mode", 1f);  // 1 = Cutout
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        mat.SetFloat("_Cutoff", 0.4f);

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath);
        if (albedo == null)
        {
            Debug.LogWarning($"[GrassBreakupSetup] {GrassTexturePath} not loaded.");
        }
        else
        {
            mat.SetTexture("_MainTex", albedo);
        }
        mat.SetColor("_Color", new Color(0.85f, 1f, 0.80f, 1f));  // gentle green-tint
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.05f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
    }
}
