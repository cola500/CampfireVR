using UnityEditor;
using UnityEngine;

// Polishes CampfireWood.mat so the campfire wood pile reads as real wood,
// not a flat dark-brown shape. Uses Mountain Terrain pack's bark texture +
// matching normalmap — already in the project, already used by tree_01,
// so the campfire and the surrounding trees share a visual family.
//
// Also fixes a publisher bug as a side effect: bark01_normal.png ships
// with textureType = Default (color), not NormalMap. The original
// bark_01.mat uses it incorrectly in the _MetallicGlossMap slot which
// happens to "work" for trees because Quest rendering forgives it. For
// the campfire we want a proper bump in the _BumpMap slot — so we flip
// the importer to NormalMap first. The tree material's wrong-slot
// reference still resolves to the same texture; trees still render fine.
//
// Idempotent: re-running re-applies the same texture refs and color/
// gloss values without duplicating anything.
public static class CampfireMaterialPolish
{
    private const string MaterialPath = "Assets/Materials/CampfireWood.mat";
    private const string BarkAlbedoPath = "Assets/Mountain Terrain rocks and tree/Materials/Trees/tree_01/bark01.png";
    private const string BarkNormalPath = "Assets/Mountain Terrain rocks and tree/Materials/Trees/tree_01/bark01_normal.png";

    // Warmer than the previous (0.18, 0.10, 0.06) flat brown — lets bark
    // detail read through. Still on the dark side so it stays cosy under
    // the fire's warm point light.
    private static readonly Color WoodTint = new Color(0.60f, 0.46f, 0.32f);
    private const float WoodSmoothness = 0.10f;  // matte
    private const float WoodMetallic = 0f;

    [MenuItem("Tools/Quest Setup/Apply Campfire Material Polish")]
    public static void Apply()
    {
        if (!EnsureNormalMapImportType(BarkNormalPath))
        {
            Debug.LogWarning($"[CampfireMaterialPolish] Could not load TextureImporter for {BarkNormalPath} — skipping normal map.");
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Debug.LogError($"[CampfireMaterialPolish] {MaterialPath} not found. Run ForestFloor or campfire setup first.");
            return;
        }

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(BarkAlbedoPath);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(BarkNormalPath);

        if (albedo == null) Debug.LogWarning($"[CampfireMaterialPolish] {BarkAlbedoPath} not loaded.");
        if (normal == null) Debug.LogWarning($"[CampfireMaterialPolish] {BarkNormalPath} not loaded.");

        mat.SetTexture("_MainTex", albedo);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetColor("_Color", WoodTint);
        mat.SetFloat("_Glossiness", WoodSmoothness);
        mat.SetFloat("_Metallic", WoodMetallic);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        Debug.Log($"[CampfireMaterialPolish] Updated {mat.name}: bark albedo + normal applied, tint {WoodTint}, smoothness {WoodSmoothness}.");
    }

    private static bool EnsureNormalMapImportType(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        if (importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
            Debug.Log($"[CampfireMaterialPolish] Switched {path} import type to NormalMap.");
        }
        return true;
    }
}
