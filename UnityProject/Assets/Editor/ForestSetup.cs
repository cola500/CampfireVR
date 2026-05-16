using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Tidies the forest-atmosphere slice's nature objects after they've been
// instantiated. Specifically:
//   - Scales rock_set_* down to a cozy-fire-pit size (0.4) — the Mountain
//     Terrain pack's rocks are authored at landscape scale (~2m).
//   - Turns off shadow casting on every tree_01 and rock_set_* — Quest
//     shadow budget is tight with realtime point-light + directional. The
//     cozy fire stays dramatic without trees & rocks casting hard shadows.
//   - Disables shadow receiving on the rocks (small props near the
//     pit don't need detailed shadowing).
//
// Idempotent: re-running re-applies the same transforms and shadow flags
// without duplicating anything. Safe to invoke any time after instantiation.
public static class ForestSetup
{
    private const float RockScale = 0.4f;

    [MenuItem("Tools/Quest Setup/Apply Forest Setup")]
    public static void Apply()
    {
        int treesTouched = 0;
        int rocksTouched = 0;

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.scene.IsValid()) continue; // skip assets, only scene objects
            string n = go.name;

            if (n == "tree_01")
            {
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    EditorUtility.SetDirty(mr);
                }
                treesTouched++;
            }
            else if (n.StartsWith("rock_set_"))
            {
                go.transform.localScale = Vector3.one * RockScale;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    EditorUtility.SetDirty(mr);
                }
                EditorUtility.SetDirty(go);
                rocksTouched++;
            }
        }

        Debug.Log($"[ForestSetup] Configured {treesTouched} tree(s) and {rocksTouched} rock(s) — shadows off, rocks scaled to {RockScale}x.");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
