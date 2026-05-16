using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Attaches SubtleTreeWind to the N trees closest to the campfire (0, 0, 0).
// Sorted by distance so the prototype affects trees most visible from the
// seated player perspective.
//
// Idempotent: re-running doesn't add duplicate components.
// Reversible: "Remove Tree Wind" tears down everything we added.
public static class TreeWindSetup
{
    private const int PrototypeTreeCount = 5;
    private const string TreeNamePrefix = "tree_01";

    [MenuItem("Tools/Quest Setup/Apply Tree Wind (Prototype, 5 trees)")]
    public static void ApplyPrototype()
    {
        var trees = FindAllTrees();
        // Closest-to-fire first — most visible from seats, best smoke test.
        trees.Sort((a, b) =>
            a.transform.position.sqrMagnitude.CompareTo(b.transform.position.sqrMagnitude));

        int added = 0, skipped = 0;
        foreach (var tree in trees)
        {
            if (added >= PrototypeTreeCount) break;
            if (tree.GetComponent<SubtleTreeWind>() != null) { skipped++; continue; }
            Undo.AddComponent<SubtleTreeWind>(tree);
            added++;
        }

        Debug.Log($"[TreeWindSetup] Added SubtleTreeWind to {added} tree(s) (closest to fire); skipped {skipped} already-windy tree(s).");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Quest Setup/Apply Tree Wind (ALL trees)")]
    public static void ApplyAll()
    {
        int added = 0, skipped = 0;
        foreach (var tree in FindAllTrees())
        {
            if (tree.GetComponent<SubtleTreeWind>() != null) { skipped++; continue; }
            Undo.AddComponent<SubtleTreeWind>(tree);
            added++;
        }

        Debug.Log($"[TreeWindSetup] Added SubtleTreeWind to {added} tree(s); skipped {skipped} already-windy tree(s).");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Quest Setup/Remove Tree Wind (all)")]
    public static void Remove()
    {
        int removed = 0;
        foreach (var comp in Object.FindObjectsByType<SubtleTreeWind>(FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(comp);
            removed++;
        }
        Debug.Log($"[TreeWindSetup] Removed {removed} SubtleTreeWind component(s).");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static List<GameObject> FindAllTrees()
    {
        var list = new List<GameObject>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.scene.IsValid()) continue;
            if (!go.name.StartsWith(TreeNamePrefix)) continue;
            list.Add(go);
        }
        return list;
    }
}
