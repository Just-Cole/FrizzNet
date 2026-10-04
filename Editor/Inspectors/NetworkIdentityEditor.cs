using UnityEditor;
using UnityEngine;
using FrizzNet.Core;

namespace FrizzNet.Editor
{
    [CustomEditor(typeof(NetworkIdentity))]
    public sealed class NetworkIdentityEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            NetworkIdentity identity = (NetworkIdentity)target;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);

            NetworkIdentity prefabIdentity = FrizzNetSessionSetup.ResolvePrefabIdentity(identity);
            bool persistent = EditorUtility.IsPersistent(prefabIdentity.gameObject);
            if (!persistent)
                EditorGUILayout.HelpBox("Save this object as a prefab, then register that prefab on NetworkManager.", MessageType.Warning);

            NetworkManager manager = FrizzNetSessionSetup.FindNetworkManager();
            if (manager == null)
            {
                EditorGUILayout.HelpBox("No NetworkManager in the open scene. Use Tools > FrizzNet > Setup Network Session.", MessageType.Info);
                if (GUILayout.Button("Create Listen Host Session"))
                    FrizzNetSessionSetup.CreateListenHostSession();
                return;
            }

            EditorGUI.BeginDisabledGroup(!persistent);
            if (GUILayout.Button("Register On Scene NetworkManager"))
            {
                Undo.RecordObject(manager, "Register FrizzNet Prefab");
                manager.TryAddSpawnablePrefab(prefabIdentity);
                EditorUtility.SetDirty(manager);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Sync Components", EditorStyles.miniBoldLabel);
            DrawAddButton<FrizzNetworkTransform>(identity, "Add Network Transform");
            DrawAddButton<FrizzNetworkAnimator>(identity, "Add Network Animator");
            DrawAddButton<FrizzNetworkRigidbody>(identity, "Add Network Rigidbody");
        }

        private static void DrawAddButton<T>(NetworkIdentity identity, string label) where T : Component
        {
            if (identity.GetComponent<T>() != null)
                return;

            if (GUILayout.Button(label))
                Undo.AddComponent<T>(identity.gameObject);
        }
    }
}
