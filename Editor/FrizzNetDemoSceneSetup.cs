#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FrizzNet.Core;
using FrizzNet.Steam;
using FrizzNet.Samples;
using FrizzNet.Transport;

namespace FrizzNet.Editor
{
    /// <summary>
    /// Creates FrizzNet demo lobby and game scenes with all required components pre-configured.
    /// </summary>
    public static class FrizzNetDemoSceneSetup
    {
        private const string ScenesFolder = "Assets/FrizzNet/Samples/Scenes";
        private const string LobbyScenePath = ScenesFolder + "/DemoLobbyScene.unity";
        private const string GameScenePath = ScenesFolder + "/DemoGameScene.unity";

        private const string LocalTestScenePath = ScenesFolder + "/DemoLocalTestScene.unity";

        [MenuItem("Tools/FrizzNet/Setup Demo Scenes")]
        public static void SetupDemoScenes()
        {
            EnsureFolder(ScenesFolder);

            CreateLobbyScene();
            CreateGameScene();
            CreateLocalTestScene();
            UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FrizzNet] Demo scenes created at Assets/FrizzNet/Samples/Scenes/.");
        }

        [MenuItem("Tools/FrizzNet/Setup Local Test Scene")]
        public static void SetupLocalTestSceneOnly()
        {
            EnsureFolder(ScenesFolder);
            CreateLocalTestScene();
            CreateGameScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FrizzNet] Local test scene created. Open DemoLocalTestScene and run two instances.");
        }

        private static void CreateLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject networkRoot = new GameObject("FrizzNet");
            networkRoot.AddComponent<NetworkManager>();
            networkRoot.AddComponent<SteamTransport>();
            networkRoot.AddComponent<LocalTransport>().enabled = false;
            networkRoot.AddComponent<FrizzServerManager>();
            networkRoot.AddComponent<FrizzVoiceManager>();
            networkRoot.AddComponent<FrizzNetworkSceneManager>();
            networkRoot.AddComponent<FrizzHostMigration>();
            networkRoot.AddComponent<FrizzInterestManager>();

            GameObject steamManager = new GameObject("SteamManager");
            steamManager.AddComponent<SteamManager>();

            GameObject sampleUi = new GameObject("SampleUI");
            sampleUi.AddComponent<LobbyExample>();
            sampleUi.AddComponent<ChatExample>();

            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        private static void CreateGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject gameManager = new GameObject("DemoGameManager");
            gameManager.AddComponent<DemoSpawnManager>();

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void CreateLocalTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject networkRoot = new GameObject("FrizzNet");
            networkRoot.AddComponent<LocalTransport>();
            networkRoot.AddComponent<NetworkManager>();
            SteamTransport steamTransport = networkRoot.AddComponent<SteamTransport>();
            steamTransport.enabled = false;
            networkRoot.AddComponent<FrizzNetworkSceneManager>();
            networkRoot.AddComponent<FrizzHostMigration>();
            networkRoot.AddComponent<FrizzInterestManager>();

            GameObject sampleUi = new GameObject("SampleUI");
            sampleUi.AddComponent<LocalTestExample>();

            EditorSceneManager.SaveScene(scene, LocalTestScenePath);
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(LocalTestScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
            EditorBuildSettings.scenes = scenes;
        }

        private static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
#endif
