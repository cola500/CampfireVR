using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Applies Terra's DirtA + DirtANorm to the existing Ground plane via a
// new ForestFloor material (Standard shader, no Terrain Engine, no shader
// graph, no scripts at runtime). Idempotent:
//
// - Ensures Assets/Materials/ForestFloor.mat exists with the right shader
//   keywords, textures, and tiling (texture repeats roughly every 1.5 m
//   across the 20×20 m Ground).
// - Assigns it to the Ground GameObject's MeshRenderer.sharedMaterial.
// - If a previous run left a separate "ForestFloor" plane in the scene,
//   removes it so we don't double-stack ground.
//
// Reversibility:
//   1. Re-assign Ground's previous material in the Inspector.
//   2. (Or) delete Assets/Materials/ForestFloor.mat — Ground falls back
//      to whatever asset was the default.
public static class ForestFloorSetup
{
    private const string MaterialPath = "Assets/Materials/ForestFloor.mat";
    private const string DirtAlbedoPath = "Assets/Terra/Example/Textures/DirtA.tga";
    private const string DirtNormalPath = "Assets/Terra/Example/Textures/DirtANorm.tga";
    private const string GroundName = "Ground";
    private const string LegacyPatchName = "ForestFloor";

    // Ground plane is 20×20 m (Unity plane scaled by 2). Tile factor of 13
    // gives ~1.5 m per texture repeat — same perceived scale as the earlier
    // 4× tiling on the 6×6 m patch.
    private const float TileFactor = 13f;

    [MenuItem("Tools/Quest Setup/Apply Forest Floor")]
    public static void Apply()
    {
        var mat = GetOrCreateMaterial();
        if (mat == null) return;

        var ground = GameObject.Find(GroundName);
        if (ground == null)
        {
            Debug.LogError($"[ForestFloorSetup] {GroundName} GameObject not found in active scene.");
            return;
        }

        var mr = ground.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            Debug.LogError($"[ForestFloorSetup] {GroundName} has no MeshRenderer.");
            return;
        }

        mr.sharedMaterial = mat;
        EditorUtility.SetDirty(mr);

        // Remove the previous standalone patch if a prior run created it.
        var legacyPatch = GameObject.Find(LegacyPatchName);
        if (legacyPatch != null)
        {
            Object.DestroyImmediate(legacyPatch);
            Debug.Log($"[ForestFloorSetup] Removed legacy {LegacyPatchName} patch — material is now on {GroundName} directly.");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[ForestFloorSetup] Assigned {mat.name} to {GroundName}.MeshRenderer.sharedMaterial (tiling {TileFactor}×).");
    }

    private static Material GetOrCreateMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[ForestFloorSetup] Standard shader not found.");
                return null;
            }
            mat = new Material(shader) { name = "ForestFloor" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(DirtAlbedoPath);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(DirtNormalPath);
        if (albedo == null) Debug.LogWarning($"[ForestFloorSetup] {DirtAlbedoPath} not loaded.");
        if (normal == null) Debug.LogWarning($"[ForestFloorSetup] {DirtNormalPath} not loaded.");

        mat.SetTexture("_MainTex", albedo);
        mat.SetTextureScale("_MainTex", new Vector2(TileFactor, TileFactor));
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.SetTextureScale("_BumpMap", new Vector2(TileFactor, TileFactor));
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.1f);  // matte
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
