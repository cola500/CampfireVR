using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Procedural cozy mittens for the local VR hands. Generates a tiny low-poly
// mesh per hand by combining four Unity primitives (palm sphere, finger
// bulge sphere, thumb sphere, cuff cylinder), bakes the result to a Mesh
// asset, and assigns it to LeftHandMesh + RightHandMesh.
//
// Why mittens (vs realistic gloves / hand-tracking-rigged mesh): fits the
// campfire's warm low-poly aesthetic, keeps poly count tiny (~600 tris per
// hand vs ~5k for the XRI UniversalController), needs no extra package.
// See docs/cozy-mittens-slice.md and docs/controller-visuals-audit.md.
//
// Fallback: the previous HandsControllerMesh / HandController.mat are NOT
// deleted. Re-run Tools/Quest Setup/Apply Hand Visuals to swap back to the
// controller mesh. Tools/Quest Setup/Apply Hand Visuals (Force Sphere) is
// the older minimal fallback.
//
// Local-only: PlayerHead.prefab (the remote avatar) is intentionally left
// on the controller-mesh look for now. Apply remote mittens in a follow-up
// slice once the local look is validated in headset.
//
// Idempotent: re-runs regenerate the mesh assets in place + re-assign refs.
public static class MittenHandsSetup
{
    private const string LeftMeshPath  = "Assets/Models/LeftMittenHand.asset";
    private const string RightMeshPath = "Assets/Models/RightMittenHand.asset";
    private const string MaterialPath  = "Assets/Materials/MittenWarm.mat";

    private const string LeftHandMeshName  = "LeftHandMesh";
    private const string RightHandMeshName = "RightHandMesh";

    // Warm sun-bleached wool tone — between the dog coat (0.55, 0.38, 0.22)
    // and the campfire wood tint. Matte (smoothness 0.04) reads as fabric
    // rather than plastic in firelight.
    private static readonly Color MittenTint = new Color(0.42f, 0.28f, 0.20f);
    private const float MittenSmoothness = 0.04f;

    [MenuItem("Tools/Quest Setup/Apply Mitten Hands")]
    public static void Apply()
    {
        EnsureFolder("Assets/Models");
        EnsureFolder("Assets/Materials");

        var leftMesh  = BuildOrReplaceMesh(LeftMeshPath,  thumbSide: +1f, "LeftMittenHand");
        var rightMesh = BuildOrReplaceMesh(RightMeshPath, thumbSide: -1f, "RightMittenHand");
        var mat       = GetOrCreateMaterial();

        int updated = 0;
        updated += AssignToHand(LeftHandMeshName,  leftMesh,  mat) ? 1 : 0;
        updated += AssignToHand(RightHandMeshName, rightMesh, mat) ? 1 : 0;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[MittenHandsSetup] Applied cozy mittens to {updated} hand mesh(es) — material={mat.name} ({MittenTint}), tris≈{leftMesh.triangles.Length / 3} per hand.");
    }

    // -- mesh generation ------------------------------------------------

    private static Mesh BuildOrReplaceMesh(string assetPath, float thumbSide, string meshName)
    {
        var mesh = BuildMitten(thumbSide);
        mesh.name = meshName;

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssetIfDirty(mesh);
        return mesh;
    }

    // Build a mitten from four primitives. thumbSide = +1 for left hand
    // (thumb on +X), -1 for right hand (thumb on -X). All other geometry
    // is symmetric.
    private static Mesh BuildMitten(float thumbSide)
    {
        // Authored in a coordinate frame where +Z = pointing direction, +Y =
        // back of hand (up at rest), +X = hand's local right. The hand-anchor
        // transform applies the 45° pitch separately.
        var parts = new[]
        {
            // Palm — slightly egg-shaped, flattened on Y for "back of hand".
            new Part(PrimitiveType.Sphere,
                pos: new Vector3(0f, 0f, 0f),
                rot: Quaternion.identity,
                scale: new Vector3(0.060f, 0.045f, 0.075f)),

            // Finger bulge — extends forward of the palm. Slightly narrower
            // and just as flat. Centered low (toward palm side) so the
            // silhouette reads as "knuckles forward".
            new Part(PrimitiveType.Sphere,
                pos: new Vector3(0f, -0.005f, 0.075f),
                rot: Quaternion.identity,
                scale: new Vector3(0.052f, 0.042f, 0.085f)),

            // Thumb — small sphere stuck to the side, slightly forward and
            // angled up + outward. Sphere (not capsule) for the rounded
            // mitten silhouette.
            new Part(PrimitiveType.Sphere,
                pos: new Vector3(thumbSide * 0.045f, 0.005f, 0.020f),
                rot: Quaternion.Euler(15f, thumbSide * 30f, thumbSide * -8f),
                scale: new Vector3(0.030f, 0.028f, 0.045f)),

            // Cuff — flat cylinder at the wrist (-Z side). Cylinder default
            // axis is Y, so rotate 90° on X to lay it across the wrist.
            new Part(PrimitiveType.Cylinder,
                pos: new Vector3(0f, 0f, -0.050f),
                rot: Quaternion.Euler(90f, 0f, 0f),
                scale: new Vector3(0.078f, 0.014f, 0.078f)),
        };

        var combine = new CombineInstance[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            var temp = GameObject.CreatePrimitive(parts[i].type);
            temp.hideFlags = HideFlags.HideAndDontSave;
            // CreatePrimitive adds a Collider — kill it; we only need the mesh.
            var col = temp.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            temp.transform.position = parts[i].pos;
            temp.transform.rotation = parts[i].rot;
            temp.transform.localScale = parts[i].scale;

            combine[i].mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            combine[i].transform = temp.transform.localToWorldMatrix;

            Object.DestroyImmediate(temp);
        }

        var mesh = new Mesh
        {
            indexFormat = IndexFormat.UInt16
        };
        mesh.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private struct Part
    {
        public PrimitiveType type;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;
        public Part(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale)
        { this.type = type; this.pos = pos; this.rot = rot; this.scale = scale; }
    }

    // -- material --------------------------------------------------------

    private static Material GetOrCreateMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[MittenHandsSetup] Standard shader not found.");
                return null;
            }
            mat = new Material(shader) { name = "MittenWarm" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.SetColor("_Color", MittenTint);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", MittenSmoothness);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
    }

    // -- scene assignment ------------------------------------------------

    private static bool AssignToHand(string handObjectName, Mesh mesh, Material mat)
    {
        var go = GameObject.Find(handObjectName);
        if (go == null)
        {
            Debug.LogWarning($"[MittenHandsSetup] {handObjectName} not found in scene — skipping.");
            return false;
        }

        var mf = go.GetComponent<MeshFilter>();
        if (mf != null) { mf.sharedMesh = mesh; EditorUtility.SetDirty(mf); }

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            EditorUtility.SetDirty(mr);
        }

        EditorUtility.SetDirty(go);
        return true;
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
        var parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
        var leaf = Path.GetFileName(assetFolder);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
