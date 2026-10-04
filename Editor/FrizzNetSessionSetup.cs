#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using FrizzNet.Core;
using FrizzNet.Steam;
using FrizzNet.Transport;

namespace FrizzNet.Editor
{
    public enum FrizzNetSessionKind
    {
        ListenHost,
        Dedicated
    }

    public sealed class FrizzNetSessionOptions
    {
        public FrizzNetSessionKind Kind = FrizzNetSessionKind.ListenHost;
        public string ObjectName = "FrizzNet Session";
        public bool IncludeServerManager = true;
        public bool IncludeSceneManager = true;
        public bool IncludeVoice = true;
        public bool IncludeHostMigration;
        public bool IncludeInterest;
        public NetworkIdentity PlayerPrefab;
        public NetworkIdentity[] ExtraPrefabs;
    }

    /// <summary>
    /// Creates or repairs a wired FrizzNet session in the open scene.
    /// </summary>
    public static class FrizzNetSessionSetup
    {
        [MenuItem("Tools/FrizzNet/Setup Network Session...", false, 1)]
        public static void OpenSetupWindow()
        {
            Windows.FrizzNetSetupWindow.ShowWindow();
        }

        [MenuItem("Tools/FrizzNet/Create Listen Host Session", false, 2)]
        [MenuItem("GameObject/FrizzNet/Listen Host Session", false, 10)]
        public static void CreateListenHostSession()
        {
            CreateOrRepair(new FrizzNetSessionOptions
            {
                Kind = FrizzNetSessionKind.ListenHost,
                IncludeServerManager = true,
                IncludeSceneManager = true,
                IncludeVoice = true
            });
        }

        [MenuItem("Tools/FrizzNet/Create Dedicated Session", false, 3)]
        [MenuItem("GameObject/FrizzNet/Dedicated Session", false, 11)]
        public static void CreateDedicatedSession()
        {
            CreateOrRepair(new FrizzNetSessionOptions
            {
                Kind = FrizzNetSessionKind.Dedicated,
                ObjectName = "FrizzNet Dedicated Session",
                IncludeServerManager = false,
                IncludeSceneManager = true,
                IncludeVoice = false,
                IncludeHostMigration = false
            });
        }

        public static GameObject CreateOrRepair(FrizzNetSessionOptions options)
        {
            if (options == null)
                options = new FrizzNetSessionOptions();

            SteamAppIdUtility.EnsureFile();

            GameObject root = FindExistingSession();
            bool created = root == null;
            if (created)
            {
                root = new GameObject(string.IsNullOrWhiteSpace(options.ObjectName)
                    ? "FrizzNet Session"
                    : options.ObjectName);
                Undo.RegisterCreatedObjectUndo(root, "Create FrizzNet Session");
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(root, "Repair FrizzNet Session");
            }

            if (options.Kind == FrizzNetSessionKind.ListenHost)
                ConfigureListenHost(root, options);
            else
                ConfigureDedicated(root, options);

            NetworkManager networkManager = root.GetComponent<NetworkManager>();
            RegisterPrefabs(networkManager, options);
            EditorUtility.SetDirty(root);
            Selection.activeGameObject = root;

            string action = created ? "Created" : "Repaired";
            string kind = options.Kind == FrizzNetSessionKind.ListenHost ? "listen-host" : "dedicated";
            Debug.Log("[FrizzNet] " + action + " " + kind + " session on '" + root.name + "'.");
            return root;
        }

        public static NetworkManager FindNetworkManager()
        {
            return Object.FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
        }

        public static bool HasSteamAppIdFile()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
                return false;

            string path = Path.Combine(projectRoot.FullName, "steam_appid.txt");
            return File.Exists(path);
        }

        public static void EnsureSteamAppIdFile()
        {
            SteamAppIdUtility.EnsureFile();
        }

        private static GameObject FindExistingSession()
        {
            NetworkManager manager = FindNetworkManager();
            if (manager != null)
                return manager.gameObject;

            GameObject selected = Selection.activeGameObject;
            if (selected != null && selected.scene.IsValid() && CanReuseSelected(selected))
                return selected;

            return null;
        }

        private static bool CanReuseSelected(GameObject selected)
        {
            if (selected.GetComponent<NetworkManager>() != null)
                return true;
            if (selected.GetComponent<SteamManager>() != null)
                return true;
            if (selected.GetComponent<SteamGameServerManager>() != null)
                return true;

            Component[] components = selected.GetComponents<Component>();
            return components.Length <= 1;
        }
        }

        private static void ConfigureListenHost(GameObject root, FrizzNetSessionOptions options)
        {
            RemoveComponent<SteamGameServerManager>(root);
            RemoveComponent<SteamGameServerTransport>(root);

            EnsureComponent<SteamManager>(root);
            SteamTransport transport = EnsureComponent<SteamTransport>(root);
            NetworkManager networkManager = EnsureComponent<NetworkManager>(root);
            AssignTransport(networkManager, transport);
            SetDedicated(networkManager, false);

            if (options.IncludeServerManager)
                EnsureComponent<FrizzServerManager>(root);
            else
                RemoveComponent<FrizzServerManager>(root);

            ConfigureSharedManagers(root, options);
        }

        private static void ConfigureDedicated(GameObject root, FrizzNetSessionOptions options)
        {
            RemoveComponent<SteamManager>(root);
            RemoveComponent<SteamTransport>(root);
            RemoveComponent<FrizzServerManager>(root);
            RemoveComponent<FrizzVoiceManager>(root);
            RemoveComponent<FrizzHostMigration>(root);

            SteamGameServerManager gameServer = EnsureComponent<SteamGameServerManager>(root);
            SteamGameServerTransport transport = EnsureComponent<SteamGameServerTransport>(root);
            NetworkManager networkManager = EnsureComponent<NetworkManager>(root);
            AssignTransport(networkManager, transport);
            SetDedicated(networkManager, true);

            SerializedObject serverSo = new SerializedObject(gameServer);
            if (serverSo.FindProperty("m_AutoInitialize") != null)
                serverSo.FindProperty("m_AutoInitialize").boolValue = true;
            serverSo.ApplyModifiedPropertiesWithoutUndo();

            ConfigureSharedManagers(root, options);
        }

        private static void ConfigureSharedManagers(GameObject root, FrizzNetSessionOptions options)
        {
            if (options.IncludeSceneManager)
                EnsureComponent<FrizzNetworkSceneManager>(root);
            else
                RemoveComponent<FrizzNetworkSceneManager>(root);

            if (options.Kind == FrizzNetSessionKind.ListenHost && options.IncludeVoice)
                EnsureComponent<FrizzVoiceManager>(root);
            else if (options.Kind == FrizzNetSessionKind.ListenHost)
                RemoveComponent<FrizzVoiceManager>(root);

            if (options.Kind == FrizzNetSessionKind.ListenHost && options.IncludeHostMigration)
                EnsureComponent<FrizzHostMigration>(root);
            else if (options.Kind != FrizzNetSessionKind.ListenHost)
                RemoveComponent<FrizzHostMigration>(root);

            if (options.IncludeInterest)
                EnsureComponent<FrizzInterestManager>(root);
            else
                RemoveComponent<FrizzInterestManager>(root);
        }

        private static void RegisterPrefabs(NetworkManager networkManager, FrizzNetSessionOptions options)
        {
            if (networkManager == null)
                return;

            if (options.PlayerPrefab != null)
                networkManager.TryAddSpawnablePrefab(ResolvePrefabIdentity(options.PlayerPrefab));

            if (options.ExtraPrefabs == null)
                return;

            for (int i = 0; i < options.ExtraPrefabs.Length; i++)
            {
                NetworkIdentity identity = ResolvePrefabIdentity(options.ExtraPrefabs[i]);
                if (identity != null)
                    networkManager.TryAddSpawnablePrefab(identity);
            }

            EditorUtility.SetDirty(networkManager);
        }

        public static NetworkIdentity ResolvePrefabIdentity(NetworkIdentity identity)
        {
            if (identity == null)
                return null;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(identity.gameObject);
            if (source != null)
            {
                NetworkIdentity prefabIdentity = source.GetComponent<NetworkIdentity>();
                if (prefabIdentity != null)
                    return prefabIdentity;
            }

            return identity;
        }

        public static void AssignTransport(NetworkManager networkManager, MonoBehaviour transport)
        {
            if (networkManager == null || transport == null)
                return;

            SerializedObject so = new SerializedObject(networkManager);
            so.FindProperty("m_TransportComponent").objectReferenceValue = transport;
            so.ApplyModifiedProperties();
            networkManager.AssignTransport((INetworkTransport)transport);
        }

        public static void SetDedicated(NetworkManager networkManager, bool dedicated)
        {
            if (networkManager == null)
                return;

            SerializedObject so = new SerializedObject(networkManager);
            so.FindProperty("m_IsDedicatedServer").boolValue = dedicated;
            so.ApplyModifiedProperties();
            networkManager.SetDedicatedServer(dedicated);
        }

        private static T EnsureComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component == null)
                component = Undo.AddComponent<T>(root);
            return component;
        }

        private static void RemoveComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component != null)
                Undo.DestroyObjectImmediate(component);
        }
    }
}
#endif
