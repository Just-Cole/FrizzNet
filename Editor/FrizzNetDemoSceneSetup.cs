#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FrizzNet.Samples;

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

        [MenuItem("Tools/FrizzNet/Setup Demo Scenes")]
        public static void SetupDemoScenes()
        {
            EnsureFolder(ScenesFolder);

            CreateLobbyScene();
            CreateGameScene();
            UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FrizzNet] Demo scenes created at Assets/FrizzNet/Samples/Scenes/.");
        }

        private static void CreateLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            FrizzNetSessionSetup.CreateOrRepair(new FrizzNetSessionOptions
            {
                Kind = FrizzNetSessionKind.ListenHost,
                ObjectName = "FrizzNet",
                IncludeServerManager = true,
                IncludeSceneManager = true,
                IncludeVoice = true,
                IncludeHostMigration = true,
                IncludeInterest = true
            });

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

        private static void UpdateBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
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
