using UnityEditor;
using UnityEngine;
using FrizzNet.Core;

namespace FrizzNet.Editor.Windows
{
    /// <summary>
    /// Guided FrizzNet session setup for the open scene.
    /// </summary>
    public sealed class FrizzNetSetupWindow : EditorWindow
    {
        private FrizzNetSessionKind _kind = FrizzNetSessionKind.ListenHost;
        private bool _includeServerManager = true;
        private bool _includeSceneManager = true;
        private bool _includeVoice = true;
        private bool _includeHostMigration;
        private bool _includeInterest;
        private NetworkIdentity _playerPrefab;
        private NetworkIdentity _extraPrefab;

        public static void ShowWindow()
        {
            FrizzNetSetupWindow window = GetWindow<FrizzNetSetupWindow>("FrizzNet Setup");
            window.minSize = new Vector2(420, 520);
            window.Show();
        }

        [MenuItem("Tools/FrizzNet/Setup Network Session...", true)]
        private static bool ValidateOpen()
        {
            return !EditorApplication.isPlaying;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("FrizzNet Session Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates or repairs one NetworkManager in the open scene and wires Steam, transport, and optional managers. Register the same spawnable prefabs on every peer.",
                MessageType.Info);

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            _kind = (FrizzNetSessionKind)EditorGUILayout.EnumPopup("Session Type", _kind);
            EditorGUILayout.Space(6);

            if (_kind == FrizzNetSessionKind.ListenHost)
            {
                _includeServerManager = EditorGUILayout.Toggle("Server / Lobby Manager", _includeServerManager);
                _includeVoice = EditorGUILayout.Toggle("Voice Chat", _includeVoice);
                _includeHostMigration = EditorGUILayout.Toggle("Host Migration", _includeHostMigration);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Dedicated sessions use Steam Game Server. Do not add SteamManager or SteamTransport on this process. Clients still use a listen-host style session to join.",
                    MessageType.Warning);
            }

            _includeSceneManager = EditorGUILayout.Toggle("Scene Manager", _includeSceneManager);
            _includeInterest = EditorGUILayout.Toggle("Interest Manager", _includeInterest);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Spawnable Prefabs", EditorStyles.boldLabel);
            _playerPrefab = (NetworkIdentity)EditorGUILayout.ObjectField(
                "Player Prefab",
                _playerPrefab,
                typeof(NetworkIdentity),
                false);
            _extraPrefab = (NetworkIdentity)EditorGUILayout.ObjectField(
                "Additional Prefab",
                _extraPrefab,
                typeof(NetworkIdentity),
                false);

            EditorGUILayout.Space(12);
            string buttonLabel = FrizzNetSessionSetup.FindNetworkManager() != null
                ? "Repair Session In Scene"
                : "Create Session In Scene";

            if (GUILayout.Button(buttonLabel, GUILayout.Height(32)))
                CreateOrRepair();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(16);
            DrawChecklist();
        }

        private void CreateOrRepair()
        {
            FrizzNetSessionOptions options = new FrizzNetSessionOptions
            {
                Kind = _kind,
                IncludeServerManager = _kind == FrizzNetSessionKind.ListenHost && _includeServerManager,
                IncludeSceneManager = _includeSceneManager,
                IncludeVoice = _kind == FrizzNetSessionKind.ListenHost && _includeVoice,
                IncludeHostMigration = _kind == FrizzNetSessionKind.ListenHost && _includeHostMigration,
                IncludeInterest = _includeInterest,
                PlayerPrefab = _playerPrefab,
                ExtraPrefabs = _extraPrefab != null ? new[] { _extraPrefab } : null
            };

            FrizzNetSessionSetup.CreateOrRepair(options);
        }

        private void DrawChecklist()
        {
            EditorGUILayout.LabelField("Scene Checklist", EditorStyles.boldLabel);

            NetworkManager manager = FrizzNetSessionSetup.FindNetworkManager();
            DrawCheck("NetworkManager in scene", manager != null);

            if (manager != null)
            {
                SerializedObject so = new SerializedObject(manager);
                MonoBehaviour transport = so.FindProperty("m_TransportComponent").objectReferenceValue as MonoBehaviour;
                DrawCheck("Transport assigned", transport != null);
                DrawCheck(
                    "Dedicated flag matches transport",
                    manager.IsDedicatedServer == (transport is Steam.SteamGameServerTransport));

                SerializedProperty prefabs = so.FindProperty("m_SpawnablePrefabs");
                DrawCheck("At least one spawnable prefab", prefabs != null && prefabs.arraySize > 0);
            }

            bool listenHost = manager != null && !manager.IsDedicatedServer;
            if (listenHost || manager == null)
                DrawCheck("SteamManager present", Object.FindAnyObjectByType<Steam.SteamManager>(FindObjectsInactive.Include) != null);
            if (manager != null && manager.IsDedicatedServer)
                DrawCheck("SteamGameServerManager present", Object.FindAnyObjectByType<Steam.SteamGameServerManager>(FindObjectsInactive.Include) != null);

            DrawCheck("steam_appid.txt in project root", FrizzNetSessionSetup.HasSteamAppIdFile());

            if (!FrizzNetSessionSetup.HasSteamAppIdFile() && GUILayout.Button("Write steam_appid.txt (480)"))
                FrizzNetSessionSetup.EnsureSteamAppIdFile();
        }

        private static void DrawCheck(string label, bool ok)
        {
            EditorGUILayout.HelpBox((ok ? "Ready — " : "Missing — ") + label, ok ? MessageType.Info : MessageType.Warning);
        }
    }
}
