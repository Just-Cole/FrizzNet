using UnityEditor;
using UnityEngine;
using FrizzNet.Core;
using FrizzNet.Steam;
using FrizzNet.Transport;

namespace FrizzNet.Editor
{
    [CustomEditor(typeof(NetworkManager))]
    public sealed class NetworkManagerEditor : UnityEditor.Editor
    {
        private NetworkIdentity _prefabToAdd;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            NetworkManager manager = (NetworkManager)target;
            SerializedProperty transportProperty = serializedObject.FindProperty("m_TransportComponent");
            MonoBehaviour transport = transportProperty.objectReferenceValue as MonoBehaviour;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);

            if (transport == null)
                EditorGUILayout.HelpBox("No transport assigned. Use Fix Setup or Tools > FrizzNet > Setup Network Session.", MessageType.Warning);
            else if (manager.IsDedicatedServer && transport is not SteamGameServerTransport)
                EditorGUILayout.HelpBox("Dedicated mode should use SteamGameServerTransport.", MessageType.Warning);
            else if (!manager.IsDedicatedServer && transport is SteamGameServerTransport)
                EditorGUILayout.HelpBox("Listen-host sessions should use SteamTransport. Dedicated transport is assigned.", MessageType.Warning);

            SerializedProperty prefabs = serializedObject.FindProperty("m_SpawnablePrefabs");
            if (prefabs == null || prefabs.arraySize == 0)
                EditorGUILayout.HelpBox("No spawnable prefabs. Register every NetworkIdentity prefab used by Spawn().", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fix Setup"))
            {
                FrizzNetSessionSetup.CreateOrRepair(new FrizzNetSessionOptions
                {
                    Kind = manager.IsDedicatedServer || transport is SteamGameServerTransport
                        ? FrizzNetSessionKind.Dedicated
                        : FrizzNetSessionKind.ListenHost,
                    IncludeServerManager = !manager.IsDedicatedServer,
                    IncludeSceneManager = manager.GetComponent<FrizzNetworkSceneManager>() != null
                        || Object.FindAnyObjectByType<FrizzNetworkSceneManager>(FindObjectsInactive.Include) == null,
                    IncludeVoice = manager.GetComponent<FrizzVoiceManager>() != null,
                    IncludeHostMigration = manager.GetComponent<FrizzHostMigration>() != null,
                    IncludeInterest = manager.GetComponent<FrizzInterestManager>() != null
                });
            }

            if (GUILayout.Button("Open Setup Window"))
                Windows.FrizzNetSetupWindow.ShowWindow();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            _prefabToAdd = (NetworkIdentity)EditorGUILayout.ObjectField(
                "Add Spawnable Prefab",
                _prefabToAdd,
                typeof(NetworkIdentity),
                false);

            EditorGUI.BeginDisabledGroup(_prefabToAdd == null);
            if (GUILayout.Button("Register Prefab"))
            {
                Undo.RecordObject(manager, "Register FrizzNet Prefab");
                manager.TryAddSpawnablePrefab(FrizzNetSessionSetup.ResolvePrefabIdentity(_prefabToAdd));
                EditorUtility.SetDirty(manager);
                serializedObject.Update();
                _prefabToAdd = null;
            }
            EditorGUI.EndDisabledGroup();

            if (transport == null)
            {
                INetworkTransport found = manager.GetComponent<INetworkTransport>();
                if (found is MonoBehaviour foundBehaviour)
                {
                    if (GUILayout.Button("Assign Transport On This Object"))
                        FrizzNetSessionSetup.AssignTransport(manager, foundBehaviour);
                }
            }
        }
    }
}
