using UnityEditor;
using UnityEngine;
using Steamworks;
using FrizzNet.Core;
using FrizzNet.Steam;
using FrizzNet.Logging;

namespace FrizzNet.Editor.Windows
{
    /// <summary>
    /// Custom Unity Editor window for monitoring Steam and FrizzNet status in real-time.
    /// Features a highly polished premium dark mode design.
    /// </summary>
    public class FrizzNetSettingsWindow : EditorWindow
    {
        private Vector2 m_ScrollPosition;

        // Custom Premium Color Palette
        private readonly Color m_NeonGreen = new Color(0.22f, 1f, 0.08f);
        private readonly Color m_HeaderBg = new Color(0.09f, 0.09f, 0.10f);
        private readonly Color m_SectionBg = new Color(0.14f, 0.14f, 0.15f);
        private readonly Color m_TextMuted = new Color(0.65f, 0.65f, 0.68f);
        private readonly Color m_OnlineGreen = new Color(0.35f, 1f, 0.35f);
        private readonly Color m_OfflineRed = new Color(1f, 0.35f, 0.35f);

        [MenuItem("Tools/FrizzNet", false, 0)]
        public static void ShowWindow()
        {
            FrizzNetSettingsWindow window = GetWindow<FrizzNetSettingsWindow>("FrizzNet Settings");
            window.minSize = new Vector2(420, 520);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            // Set global background color
            DrawWindowBackground();

            DrawHeader();

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(12, 12, 12, 12) });

            DrawSteamStatus();
            GUILayout.Space(12);

            DrawLobbyStatus();
            GUILayout.Space(12);

            DrawTransportStatus();
            GUILayout.Space(12);

            DrawVoiceStatus();
            GUILayout.Space(12);

            DrawSettings();

            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawWindowBackground()
        {
            Rect windowRect = new Rect(0, 0, position.width, position.height);
            EditorGUI.DrawRect(windowRect, new Color(0.11f, 0.11f, 0.12f));
        }

        private void DrawHeader()
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 65);
            EditorGUI.DrawRect(headerRect, m_HeaderBg);

            // Draw Neon Accent line
            Rect accentRect = new Rect(headerRect.x, headerRect.yMax - 3, headerRect.width, 3);
            EditorGUI.DrawRect(accentRect, m_NeonGreen);

            // Subtitle
            GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = m_TextMuted },
                fontStyle = FontStyle.Bold
            };
            Rect subRect = new Rect(headerRect.x, headerRect.y + 40, headerRect.width, 15);
            EditorGUI.LabelField(subRect, "STEAMWORKS MULTIPLAYER FRAMEWORK", subStyle);

            // Title
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20
            };
            titleStyle.normal.textColor = m_NeonGreen;
            Rect titleRect = new Rect(headerRect.x, headerRect.y + 12, headerRect.width, 26);
            EditorGUI.LabelField(titleRect, "FRIZZNET", titleStyle);
        }

        private void DrawSteamStatus()
        {
            BeginSection("Steamworks SDK");

            bool initialized = SteamManager.Initialized;
            DrawStatusRow("API Connection", initialized);

            if (initialized)
            {
                CSteamID mySteamId = SteamUser.GetSteamID();
                string name = SteamFriends.GetPersonaName();

                DrawDataRow("Username", name, true);
                DrawDataRow("Steam ID64", mySteamId.m_SteamID.ToString(), false);
            }
            else
            {
                GUILayout.Space(6);
                DrawNotificationBox("Steam API is inactive. Make sure the Steam client is open and logged in.", MessageType.Warning);
            }

            EndSection();
        }

        private void DrawLobbyStatus()
        {
            BeginSection("Matchmaking Lobby");

            if (!SteamManager.Initialized)
            {
                GUILayout.Label("Lobby features unavailable while Steam is offline.", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = m_TextMuted } });
                EndSection();
                return;
            }

            bool inLobby = FrizzLobby.InLobby;
            DrawStatusRow("Lobby Status", inLobby);

            if (inLobby)
            {
                CSteamID lobbyId = FrizzLobby.CurrentLobbyId;
                CSteamID ownerId = FrizzLobby.GetOwner();
                int membersCount = FrizzLobby.GetMembers().Count;

                DrawDataRow("Lobby SteamID", lobbyId.m_SteamID.ToString(), false);
                DrawDataRow("Total Members", membersCount.ToString(), false);

                bool isOwner = ownerId == SteamUser.GetSteamID();
                DrawDataRow("Your Role", isOwner ? "Host (Lobby Owner)" : "Guest Client", true);

                // Settings set by host
                string map = FrizzLobby.GetMetadata("map");
                string mode = FrizzLobby.GetMetadata("mode");
                DrawDataRow("Active Map", string.IsNullOrEmpty(map) ? "Selecting..." : map, false);
                DrawDataRow("Active Mode", string.IsNullOrEmpty(mode) ? "Selecting..." : mode, false);

                GUILayout.Space(8);
                if (GUILayout.Button("LEAVE LOBBY", GUILayout.Height(26)))
                {
                    FrizzLobby.Leave();
                }

                GUILayout.Space(10);
                GUILayout.Label("LOBBY MEMBER ROSTER:", new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = m_NeonGreen } });
                
                var members = FrizzLobby.GetMembers();
                foreach (var member in members)
                {
                    string memberName = FrizzLobby.GetMemberName(member);
                    bool isMemberOwner = member == ownerId;
                    bool isReady = FrizzLobby.IsMemberReady(member);
                    
                    string readyTag = isMemberOwner ? "[HOST]" : (isReady ? "[READY]" : "[NOT READY]");
                    Color tagColor = isMemberOwner ? m_NeonGreen : (isReady ? m_OnlineGreen : Color.yellow);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($" • {memberName}", GUILayout.Width(200));
                    
                    GUIStyle tagStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight };
                    tagStyle.normal.textColor = tagColor;
                    GUILayout.Label(readyTag, tagStyle);
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Space(6);
                if (EditorApplication.isPlaying)
                {
                    if (GUILayout.Button("CREATE NEW LOBBY", GUILayout.Height(28)))
                    {
                        FrizzLobby.Create();
                    }
                }
                else
                {
                    DrawNotificationBox("Start Play Mode to enable lobby actions.", MessageType.Info);
                }
            }

            EndSection();
        }

        private void DrawTransportStatus()
        {
            BeginSection("Network Transport Engine");

            NetworkManager netManager = NetworkManager.Instance;

            if (netManager == null)
            {
                DrawNotificationBox("NetworkManager Instance is currently inactive. Run scene to connect.", MessageType.Info);
                EndSection();
                return;
            }

            DrawStatusRow("Local Host (Server)", netManager.IsHost);
            DrawStatusRow("Local Client (Active)", netManager.IsClient);

            if (netManager.IsHost)
            {
                GUILayout.Space(6);
                GUILayout.Label($"CONNECTED CLIENTS ({netManager.ConnectedClients.Count})", new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = m_NeonGreen } });
                foreach (var clientId in netManager.ConnectedClients)
                {
                    string name = FrizzLobby.InLobby ? FrizzLobby.GetMemberName(new CSteamID(clientId)) : "Standalone Player";
                    EditorGUILayout.LabelField($" - {name}", $"({clientId})");
                }
            }

            if (netManager.NetworkObjects.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("REPLICATED OBJECTS REGISTRY", new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = m_NeonGreen } });
                foreach (var pair in netManager.NetworkObjects)
                {
                    string owner = pair.Value.OwnerConnectionId == 0 ? "Global" : pair.Value.OwnerConnectionId.ToString();
                    EditorGUILayout.LabelField($" • {pair.Value.gameObject.name} [ID: {pair.Key}]", $"Owner: {owner}");
                }
            }

            EndSection();
        }

        private void DrawVoiceStatus()
        {
            BeginSection("Voice Chat Monitor");

            FrizzVoiceManager voiceManager = FrizzVoiceManager.Instance;

            if (voiceManager == null)
            {
                DrawNotificationBox("FrizzVoiceManager is inactive. Attach FrizzVoiceManager component to your scene to monitor.", MessageType.Info);
                EndSection();
                return;
            }

            DrawStatusRow("Local Recording", voiceManager.IsRecording);

            voiceManager.EnableVoice = EditorGUILayout.Toggle("Enable Voice Chat", voiceManager.EnableVoice);
            voiceManager.UsePushToTalk = EditorGUILayout.Toggle("Use Push-To-Talk", voiceManager.UsePushToTalk);

            if (voiceManager.UsePushToTalk)
            {
                voiceManager.PushToTalkKey = (KeyCode)EditorGUILayout.EnumPopup("PTT Key", voiceManager.PushToTalkKey);
            }

            voiceManager.SpatialAudio = EditorGUILayout.Toggle("Spatial 3D Audio", voiceManager.SpatialAudio);

            if (voiceManager.SpatialAudio)
            {
                voiceManager.MaxAudioDistance = EditorGUILayout.Slider("Max Audio Distance", voiceManager.MaxAudioDistance, 5f, 100f);
            }

            voiceManager.VolumeMultiplier = EditorGUILayout.Slider("Volume Multiplier", voiceManager.VolumeMultiplier, 0f, 2f);

            var speakers = voiceManager.ActiveSpeakers;
            if (speakers.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("ACTIVE VOICE STREAMS", new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = m_NeonGreen } });
                foreach (var pair in speakers)
                {
                    if (pair.Value != null)
                    {
                        string name = FrizzLobby.InLobby ? FrizzLobby.GetMemberName(new CSteamID(pair.Key)) : "Remote Client";
                        string activity = pair.Value.IsPlaying ? "<color=#35FF35>SPEAKING</color>" : "<color=#AAAAAA>SILENT</color>";

                        GUIStyle activeStyle = new GUIStyle(EditorStyles.label) { richText = true };
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label($" • {name}", activeStyle);
                        GUILayout.Label(activity, new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight, richText = true });
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EndSection();
        }

        private void DrawSettings()
        {
            BeginSection("Global Configurations");

            FrizzLogger.Enabled = EditorGUILayout.Toggle("Console Logging", FrizzLogger.Enabled);

            EndSection();
        }

        private void DrawFooter()
        {
            Rect footerRect = EditorGUILayout.GetControlRect(false, 25);
            EditorGUI.DrawRect(footerRect, m_HeaderBg);

            // Draw line above footer
            Rect lineRect = new Rect(footerRect.x, footerRect.y, footerRect.width, 1);
            EditorGUI.DrawRect(lineRect, new Color(0.2f, 0.2f, 0.22f));

            GUIStyle footerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            footerStyle.normal.textColor = m_TextMuted;
            EditorGUI.LabelField(footerRect, "FrizzNet v1.0.0 Stable • Developed in Planning Mode", footerStyle);
        }

        #region UI Component Helpers

        private void BeginSection(string title)
        {
            // Draw custom card background and border
            GUILayout.BeginVertical("box");
            GUILayout.Space(2);
            GUILayout.Label(title.ToUpper(), new GUIStyle(EditorStyles.boldLabel) 
            { 
                fontSize = 11, 
                normal = { textColor = m_NeonGreen } 
            });
            GUILayout.Space(6);
        }

        private void EndSection()
        {
            GUILayout.Space(2);
            GUILayout.EndVertical();
        }

        private void DrawStatusRow(string label, bool status)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(200));
            
            GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };
            statusStyle.normal.textColor = status ? m_OnlineGreen : m_OfflineRed;
            GUILayout.Label(status ? "■ ONLINE" : "□ OFFLINE", statusStyle);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDataRow(string label, string value, bool highlighted)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150));
            
            GUIStyle valStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
            if (highlighted)
            {
                valStyle.fontStyle = FontStyle.Bold;
                valStyle.normal.textColor = m_NeonGreen;
            }
            else
            {
                valStyle.normal.textColor = m_TextMuted;
            }
            GUILayout.Label(value, valStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNotificationBox(string message, MessageType type)
        {
            EditorGUILayout.HelpBox(message, type);
        }

        #endregion
    }
}
