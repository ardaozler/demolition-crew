using UnityEditor;
using UnityEngine;
using InteractionSystem;
using InteractionSystem.Tools;
using Unity.Netcode;

public static class SetupSledgehammer
{
    [MenuItem("DestructionCrew/Setup Sledgehammer System")]
    public static void Setup()
    {
        // 1. Create SledgehammerSettings ScriptableObject
        var settings = ScriptableObject.CreateInstance<SledgehammerSettings>();
        string settingsDir = "Assets/Data/ToolSettings";
        if (!AssetDatabase.IsValidFolder(settingsDir))
        {
            AssetDatabase.CreateFolder("Assets/Data", "ToolSettings");
        }
        AssetDatabase.CreateAsset(settings, settingsDir + "/DefaultSledgehammerSettings.asset");

        // 2. Create Sledgehammer GameObject in scene
        var hammer = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hammer.name = "Sledgehammer";
        hammer.transform.position = new Vector3(2f, 0.5f, 2f);
        hammer.transform.localScale = new Vector3(0.15f, 0.15f, 1.2f);

        // Add NetworkObject
        hammer.AddComponent<NetworkObject>();

        // Add Sledgehammer script and assign settings
        var sledge = hammer.AddComponent<Sledgehammer>();
        var so = new SerializedObject(sledge);
        so.FindProperty("settings").objectReferenceValue = settings;
        so.ApplyModifiedProperties();

        // 3. Save as prefab
        string prefabDir = "Assets/Prefabs";
        string prefabPath = prefabDir + "/Sledgehammer.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(hammer, prefabPath, InteractionMode.AutomatedAction);

        // 4. Wire up Player prefab references
        string playerPrefabPath = "Assets/Prefabs/Player.prefab";
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (playerPrefab != null)
        {
            // Open prefab for editing
            var prefabRoot = PrefabUtility.LoadPrefabContents(playerPrefabPath);

            // Wire InteractionDetector.rayOrigin to Main Camera
            var detector = prefabRoot.GetComponent<InteractionDetector>();
            if (detector != null)
            {
                var cam = prefabRoot.transform.Find("Main Camera");
                if (cam != null)
                {
                    var detSo = new SerializedObject(detector);
                    detSo.FindProperty("rayOrigin").objectReferenceValue = cam;
                    detSo.ApplyModifiedProperties();
                }
            }

            // Wire EquipmentHandler.holdPoint to HoldPoint
            var handler = prefabRoot.GetComponent<EquipmentHandler>();
            if (handler != null)
            {
                var holdPoint = prefabRoot.transform.Find("HoldPoint");
                if (holdPoint != null)
                {
                    var handlerSo = new SerializedObject(handler);
                    handlerSo.FindProperty("holdPoint").objectReferenceValue = holdPoint;
                    handlerSo.ApplyModifiedProperties();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, playerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // 5. Register Sledgehammer prefab in NetworkManager's prefab list
        var networkManagerObj = GameObject.FindAnyObjectByType<NetworkManager>();
        if (networkManagerObj != null)
        {
            var hammerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var netObj = hammerPrefab.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                networkManagerObj.AddNetworkPrefab(hammerPrefab);
                Debug.Log("[SetupSledgehammer] Added Sledgehammer to NetworkManager prefab list.");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupSledgehammer] Setup complete! Sledgehammer placed at (2, 0.5, 2).");
    }
}
