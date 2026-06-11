using UnityEngine;
using Steamworks;
using FrizzNet.Steam;
using FrizzNet.Core;
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
        private string m_LobbyToJoinId = "";
        private bool m_IsLocalPlayerReady = false;

        private readonly string[] m_AvailableMaps = { "Alpha Outpost", "Sector 7", "The Void" };
        private readonly string[] m_AvailableModes = { "Co-Op Survival", "Deathmatch", "Capture The Flag" };

        private string m_HostMigrationMessage = "";
        private float m_MigrationMessageTimer = 0f;
        private Vector2 m_ScrollPosition;

        private void Start()
        {
            FrizzLobby.OnLobbyOwnerChangedEvent += HandleLobbyOwnerChanged;
        }

        private void OnDestroy()
        {
            FrizzLobby.OnLobbyOwnerChangedEvent -= HandleLobbyOwnerChanged;
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

        private void OnGUI()
        {
            // Store default GUI colors
            Color defaultBgColor = GUI.backgroundColor;
            Color defaultContentColor = GUI.contentColor;

            // Define custom styles with rich text enabled
            GUIStyle richLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUIStyle richBox = new GUIStyle(GUI.skin.box) { richText = true };
            GUIStyle boldLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = true };
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                richText = true
            };

            // Set background color to dark slate
            GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
            
            // Set up a clean GUI area
            GUILayout.BeginArea(new Rect(10, 10, 350, 530), "FrizzNet Lobby", GUI.skin.window);
            GUILayout.Space(15);

            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

            if (!SteamManager.Initialized)
            {
                GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
                GUILayout.Label("<color=#FFD2D2><b>STEAM API OFFLINE</b></color>", richBox);
                GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
                GUILayout.Label("Ensure the Steam client is open, logged in, and steam_appid.txt is in your project root.", richLabel);
                GUILayout.EndArea();
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
            GUILayout.Space(10);

            if (!FrizzLobby.InLobby)
            {
                GUILayout.Label("Status: <color=#FF3131><b>Not in a Lobby</b></color>", richBox);
                GUILayout.Space(8);

                // Set button color to neon green for primary action
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                if (GUILayout.Button("<b>CREATE NEW LOBBY</b>", new GUIStyle(GUI.skin.button) { richText = true }, GUILayout.Height(30)))
                {
                    FrizzLobby.Create(4, ELobbyType.k_ELobbyTypePublic);
                    m_IsLocalPlayerReady = false;
                }
                
                // Restore window color
                GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);

                GUILayout.Space(15);
                GUILayout.Label("Join Lobby by ID:", boldLabel);
                m_LobbyToJoinId = GUILayout.TextField(m_LobbyToJoinId);

                // Set button color to light blue for secondary actions
                GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
                if (GUILayout.Button("<b>JOIN LOBBY</b>", new GUIStyle(GUI.skin.button) { richText = true }, GUILayout.Height(30)))
                {
                    if (ulong.TryParse(m_LobbyToJoinId, out ulong lobbyId))
                    {
                        FrizzLobby.Join(lobbyId);
                        m_IsLocalPlayerReady = false;
                    }
                    else
                    {
                        Debug.LogError("Invalid Lobby ID format. Must be a numeric SteamID64.");
                    }
                }
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

                GUILayout.Space(8);

                // Game Lobby Settings display
                GUILayout.Label("--- <b>Game Match Setup</b> ---", titleStyle);
                string selectedMap = FrizzLobby.GetMetadata("map");
                string selectedMode = FrizzLobby.GetMetadata("mode");
                string lobbyStatus = FrizzLobby.GetMetadata("status");

                if (string.IsNullOrEmpty(selectedMap)) selectedMap = "<color=#FFA500>Pending...</color>";
                if (string.IsNullOrEmpty(selectedMode)) selectedMode = "<color=#FFA500>Pending...</color>";

                GUILayout.Label($"Selected Map: {selectedMap}", richLabel);
                GUILayout.Label($"Game Mode: {selectedMode}", richLabel);

                if (!string.IsNullOrEmpty(lobbyStatus) && lobbyStatus == "started")
                {
                    GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                    GUILayout.Label("<color=#FFFFFF><b>▶ MATCH STARTING... Loading Scene</b></color>", richBox);
                    GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
                }

                GUILayout.Space(8);

                // Host Control vs Client Controls
                if (isOwner)
                {
                    // Map Selection Buttons
                    GUILayout.Label("Map Selection:", boldLabel);
                    GUILayout.BeginHorizontal();
                    foreach (var map in m_AvailableMaps)
                    {
                        bool isCurrent = selectedMap.Contains(map);
                        GUI.backgroundColor = isCurrent ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.2f, 0.2f, 0.22f);
                        if (GUILayout.Button(map, GUILayout.Height(20)))
                        {
                            FrizzLobby.SetMetadata("map", map);
                        }
                    }
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);

                    // Game Mode Selection Buttons
                    GUILayout.Label("Mode Selection:", boldLabel);
                    GUILayout.BeginHorizontal();
                    foreach (var mode in m_AvailableModes)
                    {
                        bool isCurrent = selectedMode.Contains(mode);
                        GUI.backgroundColor = isCurrent ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.2f, 0.2f, 0.22f);
                        if (GUILayout.Button(mode, GUILayout.Height(20)))
                        {
                            FrizzLobby.SetMetadata("mode", mode);
                        }
                    }
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);

                    GUILayout.Space(10);

                    // Check if all clients are ready
                    bool allClientsReady = true;
                    var membersList = FrizzLobby.GetMembers();
                    foreach (var member in membersList)
                    {
                        if (member != ownerId && !FrizzLobby.IsMemberReady(member))
                        {
                            allClientsReady = false;
                        }
                    }

                    // Green Start Game button
                    bool canStart = allClientsReady && !string.IsNullOrEmpty(FrizzLobby.GetMetadata("map"));
                    GUI.backgroundColor = canStart ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.3f, 0.3f, 0.32f);
                    
                    GUI.enabled = canStart;
                    if (GUILayout.Button("<b>LAUNCH MULTIPLAYER MATCH</b>", new GUIStyle(GUI.skin.button) { richText = true }, GUILayout.Height(30)))
                    {
                        FrizzLobby.SetMetadata("status", "started");
                        Debug.Log("Lobby Host started the game! Loading scenes...");
                    }
                    GUI.enabled = true;
                    GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
                }
                else
                {
                    // Client Ready Button (toggles colors)
                    GUI.backgroundColor = m_IsLocalPlayerReady ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
                    string readyBtnText = m_IsLocalPlayerReady ? "<b>UNREADY</b>" : "<b>READY TO PLAY</b>";
                    if (GUILayout.Button(readyBtnText, new GUIStyle(GUI.skin.button) { richText = true }, GUILayout.Height(28)))
                    {
                        m_IsLocalPlayerReady = !m_IsLocalPlayerReady;
                        FrizzLobby.SetReadyState(m_IsLocalPlayerReady);
                    }
                    GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
                }

                GUILayout.Space(8);

                GUILayout.BeginHorizontal();
                
                GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
                if (GUILayout.Button("Invite Friends", GUILayout.Height(22)))
                {
                    FrizzLobby.InviteFriends();
                }

                GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f);
                if (GUILayout.Button("Leave Lobby", GUILayout.Height(22)))
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
                    
                    string statusTag;
                    if (isMemberOwner)
                    {
                        statusTag = " <color=#39FF14>[Host]</color>";
                    }
                    else
                    {
                        bool isReady = FrizzLobby.IsMemberReady(member);
                        statusTag = isReady ? " <color=#39FF14>[✔ READY]</color>" : " <color=#FF3131>[... WAITING]</color>";
                    }

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

            if (newOwner == SteamUser.GetSteamID())
            {
                m_IsLocalPlayerReady = false;
            }
        }
    }
}
