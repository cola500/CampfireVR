using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Attaches FireVoicePresence to the FireLight GameObject so the campfire
// reacts to the local player's voice. Idempotent: re-running doesn't add
// duplicate components. Reversible: "Remove Fire Voice Presence" tears
// down what we added.
public static class FireVoicePresenceSetup
{
    private const string FireLightName = "FireLight";

    [MenuItem("Tools/Quest Setup/Apply Fire Voice Presence")]
    public static void Apply()
    {
        var fireLight = GameObject.Find(FireLightName);
        if (fireLight == null)
        {
            Debug.LogWarning($"[FireVoicePresenceSetup] No GameObject named '{FireLightName}' found in the active scene.");
            return;
        }

        if (fireLight.GetComponent<FireVoicePresence>() != null)
        {
            Debug.Log("[FireVoicePresenceSetup] FireVoicePresence already present on FireLight — nothing to do.");
            return;
        }

        Undo.AddComponent<FireVoicePresence>(fireLight);
        Debug.Log("[FireVoicePresenceSetup] Added FireVoicePresence to FireLight.");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Quest Setup/Remove Fire Voice Presence")]
    public static void Remove()
    {
        var fireLight = GameObject.Find(FireLightName);
        if (fireLight == null) return;

        var comp = fireLight.GetComponent<FireVoicePresence>();
        if (comp == null)
        {
            Debug.Log("[FireVoicePresenceSetup] No FireVoicePresence on FireLight — nothing to do.");
            return;
        }

        Undo.DestroyObjectImmediate(comp);
        Debug.Log("[FireVoicePresenceSetup] Removed FireVoicePresence from FireLight.");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
