using UnityEngine;
using Steamworks;
using FrizzNet.Steam;
using FrizzNet.Core;
using FrizzNet.Logging;
using System.Collections.Generic;

namespace FrizzNet.Samples
{
    /// <summary>
    /// Self-contained Unity IMGUI sample demonstrating Steam matchmaking lobby usage.
    /// Features a custom stylized dark mode IMGUI interface with neon colors and rich text formatting.
    /// </summary>
    [FrizzHelp("Sample IMGUI dashboard enabling players to create/join lobbies, toggle ready states, configure game options, and invite friends.")]
    public class LobbyExample : MonoBehaviour
    {
        [Header("Scene Settings")]
        [Tooltip("The name of the scene containing the active game arena.")]
        [SerializeField] private string m_GameSceneName = "DemoGameScene";

        private string m_LobbyToJoinId = "";
        private string m_JoinPassword = "";
        private string m_HostMigrationMessage = "";
        private float m_MigrationMessageTimer = 0f;
        private Vector2 m_ScrollPosition;
        private Vector2 m_BrowserScrollPosition;
        private readonly List<FrizzLobbyInfo> m_BrowserResults = new List<FrizzLobbyInfo>();
        private bool m_BrowserLoading;
        private string m_BrowserStatus = "";

        private void Start()
        {
            FrizzLobby.OnLobbyOwnerChangedEvent += HandleLobbyOwnerChanged;
            FrizzLobby.OnLobbyDataUpdatedEvent += HandleLobbyDataUpdated;
        }

        private void OnDestroy()
        {
            FrizzLobby.OnLobbyOwnerChangedEvent -= HandleLobbyOwnerChanged;
            FrizzLobby.OnLobbyDataUpdatedEvent -= HandleLobbyDataUpdated;
        }

        private void Update()
        {
            if (m_MigrationMessageTimer > 0f)
            {
                m_MigrationMessageTimer -= Time.deltaTime;
                if (m_MigrationMessageTimer <= 0f)
                {
                    m_HostMigrationMessage = "";
                }
            }
        }

        private void HandleLobbyDataUpdated(CSteamID lobbyId, CSteamID memberId)
        {
            // Only clients transition when the Host sets the status to started
            CSteamID ownerId = FrizzLobby.GetOwner();
            if (ownerId != SteamUser.GetSteamID())
            {
                string status = FrizzLobby.GetMetadata("status");
                if (status == "started")
                {
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != m_GameSceneName)
                    {
                        FrizzLogger.LogNetwork($"[Lobby] Host started the match. Loading game scene '{m_GameSceneName}'...");
                        LoadGameScene();
                    }
                }
            }
        }

        private void OnGUI()
        {
            // Store default GUI colors
            Color defaultBgColor = GUI.backgroundColor;
            Color defaultContentColor = GUI.contentColor;

            // Define custom styles with rich text enabled
            GUIStyle richLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUIStyle richBox = new GUIStyle(GUI.skin.box) { richText = true };
            GUIStyle boldLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = true };

            // Set background color to dark slate
            GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
            
            // Set up a clean GUI area (more compact now)
            GUILayout.BeginArea(new Rect(10, 10, 320, 520), "FrizzNet Lobby", GUI.skin.window);
            GUILayout.Space(12);

            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

            if (!SteamManager.Initialized)
            {
                GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
                GUILayout.Label("<color=#FFD2D2><b>STEAM API OFFLINE</b></color>", richBox);
                GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
                GUILayout.Label("Ensure the Steam client is open, logged in, and steam_appid.txt is in your project root.", richLabel);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                GUI.backgroundColor = defaultBgColor;
                return;
            }

            // Display active Host Migration notices
            if (!string.IsNullOrEmpty(m_HostMigrationMessage))
            {
                GUI.backgroundColor = new Color(0.9f, 0.6f, 0.1f);
                GUILayout.Label($"<color=#FFFFFF>⚠️ {m_HostMigrationMessage}</color>", richBox);
                GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
                GUILayout.Space(5);
            }

            // Show Local Player Information
            string myName = SteamFriends.GetPersonaName();
            GUILayout.Label($"Logged in as: <color=#39FF14><b>{myName}</b></color>", richLabel);
            GUILayout.Label($"SteamID: <color=#A0A0A5>{SteamUser.GetSteamID().m_SteamID}</color>", richLabel);
            GUILayout.Space(8);

            if (!FrizzLobby.InLobby)
            {
                GUILayout.Label("Status: <color=#FF3131><b>Not in a Lobby</b></color>", richBox);
                GUILayout.Space(6);

                // Set button color to neon green for primary action
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                if (GUILayout.Button("<b>CREATE NEW LOBBY</b>", new GUIStyle(GUI.skin.button) { richText = true }, GUILayout.Height(30)))
                {
                    FrizzLobby.Create(8, ELobbyType.k_ELobbyTypePublic); // Default to max 8 players
                }
                
                // Restore window color
                GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);

                GUILayout.Space(10);
                GUILayout.Label("Join Lobby by ID:", boldLabel);
                m_LobbyToJoinId = GUILayout.TextField(m_LobbyToJoinId);
                GUILayout.Label("Password (if required):", boldLabel);
                m_JoinPassword = GUILayout.PasswordField(m_JoinPassword, '*');

                GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
                if (GUILayout.Button("<b>JOIN LOBBY</b>", new GUIStyle(GUI.skin.button) { richText = true }, GUILayout.Height(28)))
                {
                    if (ulong.TryParse(m_LobbyToJoinId, out ulong lobbyId))
                    {
                        FrizzLobby.Join(lobbyId, m_JoinPassword);
                    }
                    else
                    {
                        Debug.LogError("Invalid Lobby ID format. Must be a numeric SteamID64.");
                    }
                }

                GUILayout.Space(10);
                GUILayout.Label("Public Lobby Browser:", boldLabel);

                GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f);
                if (GUILayout.Button(m_BrowserLoading ? "Searching..." : "<b>REFRESH LOBBY LIST</b>", new GUIStyle(GUI.skin.button) { richText = true }, GUILayout.Height(24)))
                {
                    if (!m_BrowserLoading)
                    {
                        m_BrowserLoading = true;
                        m_BrowserStatus = "Querying Steam...";
                        FrizzLobbyBrowser.RequestLobbyList(
                            results =>
                            {
                                m_BrowserResults.Clear();
                                m_BrowserResults.AddRange(results);
                                m_BrowserStatus = $"Found {results.Count} lobbies.";
                                m_BrowserLoading = false;
                            },
                            error =>
                            {
                                m_BrowserStatus = error;
                                m_BrowserLoading = false;
                            });
                    }
                }

                if (!string.IsNullOrEmpty(m_BrowserStatus))
                {
                    GUILayout.Label(m_BrowserStatus, richLabel);
                }

                m_BrowserScrollPosition = GUILayout.BeginScrollView(m_BrowserScrollPosition, GUILayout.Height(120));
                foreach (FrizzLobbyInfo info in m_BrowserResults)
                {
                    GUILayout.BeginHorizontal();
                    string lockIcon = info.HasPassword ? "🔒" : "";
                    GUILayout.Label($"{lockIcon} {info.Name} ({info.MemberCount}/{info.MaxMembers})", richLabel);
                    if (GUILayout.Button("Join", GUILayout.Width(50)))
                    {
                        FrizzLobby.Join(info.LobbyId.m_SteamID, m_JoinPassword);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();

                GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
            }
            else
            {
                CSteamID lobbyId = FrizzLobby.CurrentLobbyId;
                CSteamID ownerId = FrizzLobby.GetOwner();
                bool isOwner = ownerId == SteamUser.GetSteamID();

                GUILayout.Label("Status: <color=#39FF14><b>LOBBY ACTIVE</b></color>", richBox);
                
                GUILayout.BeginHorizontal();
                GUILayout.Label("Lobby ID:", GUILayout.Width(70));
                GUILayout.TextField($"{lobbyId.m_SteamID}");
                GUILayout.EndHorizontal();

                GUILayout.Label($"Lobby Role: {(isOwner ? "<color=#39FF14><b>Host (Owner)</b></color>" : "<color=#00FFFF><b>Client</b></color>")}", richLabel);
                
                if (isOwner)
                {
                    GUILayout.Space(6);
                    GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                    if (GUILayout.Button("<b>START MULTIPLAYER MATCH</b>", new GUIStyle(GUI.skin.button) { richText = true }, GUILayout.Height(30)))
                    {
                        FrizzLobby.SetMetadata("status", "started");
                        FrizzLogger.LogNetwork($"[Lobby] Host starting match. Loading scene '{m_GameSceneName}'...");
                        LoadGameScene();
                    }
                    GUI.backgroundColor = new Color(0.11f, 0.13f, 0.11f);
                }

                GUILayout.Space(8);

                GUILayout.BeginHorizontal();
                
                GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
                if (GUILayout.Button("Invite Friends", GUILayout.Height(24)))
                {
                    FrizzLobby.InviteFriends();
                }

                GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f);
                if (GUILayout.Button("Leave Lobby", GUILayout.Height(24)))
                {
                    FrizzLobby.Leave();
                }
                GUILayout.EndHorizontal();
                
                GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);

                GUILayout.Space(10);
                GUILayout.Label("Lobby Roster:", boldLabel);

                var members = FrizzLobby.GetMembers();
                foreach (var member in members)
                {
                    string memberName = FrizzLobby.GetMemberName(member);
                    bool isMemberOwner = member == ownerId;
                    string statusTag = isMemberOwner ? " <color=#39FF14>[Host]</color>" : " <color=#00FFFF>[Client]</color>";
                    GUILayout.Label($" • {memberName}{statusTag}", richLabel);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Restore defaults
            GUI.backgroundColor = defaultBgColor;
            GUI.contentColor = defaultContentColor;
        }

        private void HandleLobbyOwnerChanged(CSteamID lobbyId, CSteamID oldOwner, CSteamID newOwner)
        {
            string newOwnerName = FrizzLobby.GetMemberName(newOwner);
            m_HostMigrationMessage = $"HOST MIGRATED! Owner is now: {newOwnerName}";
            m_MigrationMessageTimer = 5f;
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
