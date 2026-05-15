using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NetworkSetup
{
    private const string PrefabPath = "Assets/Prefabs/PlayerHead.prefab";

    [MenuItem("Tools/Network Setup/Create PlayerHead Prefab")]
    public static void CreatePrefab()
    {
        Directory.CreateDirectory("Assets/Prefabs");
        AssetDatabase.Refresh();

        var root = new GameObject("PlayerHead");
        root.AddComponent<NetworkObject>();
        root.AddComponent<ClientNetworkTransform>();
        root.AddComponent<NetworkHead>();

        AddPrimitiveChild(root.transform, "HeadVisual", PrimitiveType.Sphere, Vector3.one * 0.2f);
        AddPrimitiveChild(root.transform, "LeftHandVisual",  PrimitiveType.Cube, new Vector3(0.07f, 0.04f, 0.12f));
        AddPrimitiveChild(root.transform, "RightHandVisual", PrimitiveType.Cube, new Vector3(0.07f, 0.04f, 0.12f));

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        var nh = prefab.GetComponent<NetworkHead>();
        var so = new SerializedObject(nh);
        so.FindProperty("visual").objectReferenceValue       = prefab.transform.Find("HeadVisual").gameObject;
        so.FindProperty("visualLeft").objectReferenceValue   = prefab.transform.Find("LeftHandVisual").gameObject;
        so.FindProperty("visualRight").objectReferenceValue  = prefab.transform.Find("RightHandVisual").gameObject;
        so.ApplyModifiedProperties();
        PrefabUtility.SavePrefabAsset(prefab);

        Debug.Log($"[NetworkSetup] Prefab saved: {PrefabPath}");
    }

    private static void AddPrimitiveChild(Transform parent, string name, PrimitiveType primitive, Vector3 localScale)
    {
        var go = GameObject.CreatePrimitive(primitive);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = localScale;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
    }

    [MenuItem("Tools/Network Setup/Configure Scene NetworkManager")]
    public static void ConfigureNetworkManager()
    {
        var nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm == null) { Debug.LogError("[NetworkSetup] No NetworkManager in scene"); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[NetworkSetup] PlayerHead prefab missing — run Create PlayerHead Prefab first."); return; }

        nm.NetworkConfig.PlayerPrefab = prefab;

        var transport = nm.GetComponent<UnityTransport>();
        if (transport != null)
        {
            nm.NetworkConfig.NetworkTransport = transport;
            transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
        }

        EditorUtility.SetDirty(nm);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[NetworkSetup] NetworkManager configured.");
    }
}
