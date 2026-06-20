using UnityEngine;
using FrizzNet.Core;
using FrizzNet.Logging;
using FrizzNet.Transport;

namespace FrizzNet.Samples
{
    /// <summary>
    /// IMGUI panel for testing FrizzNet locally without Steam.
    /// Host one instance, then connect a second instance (ParrelSync clone or standalone build).
    /// </summary>
    [FrizzHelp("Local multiplayer test UI. Host on a TCP port, then connect a second game instance to 127.0.0.1.")]
    public class LocalTestExample : MonoBehaviour
    {
        [Header("Connection Settings")]
        [SerializeField] private int m_Port = 7777;
        [SerializeField] private string m_Address = "127.0.0.1";

        [Header("Scene Settings")]
        [SerializeField] private string m_GameSceneName = "DemoGameScene";

        private string m_StatusMessage = "Ready.";
        private Vector2 m_ScrollPosition;

        private void OnEnable()
        {
            FrizzLocalSession.OnSessionStarted += HandleSessionStarted;
            FrizzLocalSession.OnSessionEnded += HandleSessionEnded;
            NetworkManager.OnClientConnected += HandleClientConnected;
            NetworkManager.OnConnected += HandleConnectedToHost;
        }

        private void OnDisable()
        {
            FrizzLocalSession.OnSessionStarted -= HandleSessionStarted;
            FrizzLocalSession.OnSessionEnded -= HandleSessionEnded;
            NetworkManager.OnClientConnected -= HandleClientConnected;
            NetworkManager.OnConnected -= HandleConnectedToHost;
        }

        private void OnGUI()
        {
            Color defaultBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);

            GUIStyle richLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUIStyle richButton = new GUIStyle(GUI.skin.button) { richText = true, fontStyle = FontStyle.Bold };

            GUILayout.BeginArea(new Rect(10, 10, 340, 420), "FrizzNet Local Test", GUI.skin.window);
            GUILayout.Space(12);

            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition);

            GUILayout.Label("<color=#00FFFF><b>Offline TCP Testing</b></color>", richLabel);
            GUILayout.Label("No Steam required. Run two instances on this PC.", richLabel);
            GUILayout.Space(8);

            GUILayout.Label($"Local Connection ID: <color=#39FF14>{NetworkManager.LocalConnectionId}</color>", richLabel);
            GUILayout.Label($"Status: {m_StatusMessage}", richLabel);
            GUILayout.Space(8);

            if (!FrizzLocalSession.InSession)
            {
                GUILayout.Label("Port:", richLabel);
                string portText = GUILayout.TextField(m_Port.ToString());
                if (int.TryParse(portText, out int parsedPort))
                {
                    m_Port = parsedPort;
                }

                GUILayout.Space(6);

                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                if (GUILayout.Button("HOST LOCAL SESSION", richButton, GUILayout.Height(32)))
                {
                    if (FrizzLocalSession.Host(m_Port))
                    {
                        m_StatusMessage = $"Hosting on 127.0.0.1:{m_Port}. Start a second instance and join.";
                    }
                    else
                    {
                        m_StatusMessage = "Failed to start host. Is the port already in use?";
                    }
                }

                GUILayout.Space(10);
                GUILayout.Label("Join Address:", richLabel);
                m_Address = GUILayout.TextField(m_Address);

                GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
                if (GUILayout.Button("JOIN LOCAL SESSION", richButton, GUILayout.Height(32)))
                {
                    string joinTarget = m_Address.Contains(":") ? m_Address : $"{m_Address}:{m_Port}";
                    if (FrizzLocalSession.Join(joinTarget))
                    {
                        m_StatusMessage = $"Connecting to {joinTarget}...";
                    }
                    else
                    {
                        m_StatusMessage = "Connection failed. Is a host running?";
                    }
                }
            }
            else
            {
                bool hosting = FrizzLocalSession.IsHost;
                GUILayout.Label(hosting
                    ? "<color=#39FF14><b>HOSTING</b></color>"
                    : "<color=#00FFFF><b>CONNECTED AS CLIENT</b></color>", richLabel);

                if (NetworkManager.Instance != null)
                {
                    GUILayout.Label($"Connected clients: {NetworkManager.Instance.ConnectedClients.Count}", richLabel);
                }

                if (hosting)
                {
                    GUILayout.Space(8);
                    GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                    if (GUILayout.Button("START LOCAL MATCH", richButton, GUILayout.Height(30)))
                    {
                        LoadGameScene();
                    }
                }

                GUILayout.Space(8);
                GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f);
                if (GUILayout.Button("DISCONNECT", richButton, GUILayout.Height(26)))
                {
                    FrizzLocalSession.Leave();
                    m_StatusMessage = "Disconnected.";
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUI.backgroundColor = defaultBg;
        }

        private void HandleSessionStarted()
        {
            m_StatusMessage = FrizzLocalSession.IsHost
                ? $"Hosting on port {FrizzLocalSession.Port}."
                : "Session started.";
        }

        private void HandleSessionEnded()
        {
            m_StatusMessage = "Session ended.";
        }

        private void HandleClientConnected(ulong clientId)
        {
            m_StatusMessage = $"Client {clientId} connected.";
        }

        private void HandleConnectedToHost()
        {
            m_StatusMessage = "Connected to local host.";
        }

        private void LoadGameScene()
        {
            if (FrizzNetworkSceneManager.Instance != null)
            {
                FrizzNetworkSceneManager.Instance.LoadScene(m_GameSceneName);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(m_GameSceneName);
            }
        }
    }
}
