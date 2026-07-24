using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using FrizzNet.Core;
using FrizzNet.Messaging;
using FrizzNet.Steam;

namespace FrizzNet.Samples
{
    /// <summary>
    /// Sample component demonstrating how to use FrizzNet packet serialization
    /// to build a lobby-wide text chat system.
    /// Styled in dark mode with neon accenting.
    /// </summary>
    public class ChatExample : MonoBehaviour
    {
        private const short MSG_CHAT = 101;

        private readonly List<string> m_ChatLog = new List<string>();
        private string m_CurrentMessage = "";
        private Vector2 m_ScrollPosition;

        private void Start()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RegisterHandler(MSG_CHAT, OnChatMessageReceived);
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.UnregisterHandler(MSG_CHAT);
            }
        }

        private void OnGUI()
        {
            if (!SteamManager.Initialized || !FrizzLobby.InLobby) return;

            // Store default GUI colors
            Color defaultBgColor = GUI.backgroundColor;
            Color defaultContentColor = GUI.contentColor;

            // Define custom styles with rich text enabled
            GUIStyle richLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUIStyle textInputStyle = new GUIStyle(GUI.skin.textField) { margin = new RectOffset(4, 4, 4, 4) };

            // Set background color to dark slate
            GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);

            // Render right side Chat panel (Height matches Lobby panel)
            GUILayout.BeginArea(new Rect(370, 10, 400, 530), "Lobby Chat Log", GUI.skin.window);
            GUILayout.Space(20);

            // Scrollable Chat log box
            GUI.backgroundColor = new Color(0.09f, 0.09f, 0.10f); // Even darker background for chat log scroll area
            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(430));
            
            GUILayout.BeginVertical(GUI.skin.box);
            foreach (var log in m_ChatLog)
            {
                GUILayout.Label(log, richLabel);
            }
            GUILayout.EndVertical();
            
            GUILayout.EndScrollView();

            // Restore dark slate color
            GUI.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
            GUILayout.Space(8);

            // Message Input bar
            GUILayout.BeginHorizontal();
            
            bool pressSend = false;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                pressSend = true;
            }

            m_CurrentMessage = GUILayout.TextField(m_CurrentMessage, textInputStyle, GUILayout.Width(310));
            
            // Set Send button to neon green
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("<b>SEND</b>", new GUIStyle(GUI.skin.button) { richText = true }, GUILayout.Width(60)) || pressSend)
            {
                SendChatMessage();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            // Restore defaults
            GUI.backgroundColor = defaultBgColor;
            GUI.contentColor = defaultContentColor;
        }

        private void SendChatMessage()
        {
            if (string.IsNullOrWhiteSpace(m_CurrentMessage)) return;

            string myName = SteamFriends.GetPersonaName();
            string msgToSend = m_CurrentMessage.Trim();
            m_CurrentMessage = ""; // Clear input

            // Format locally with a neon green name badge
            string localFormatted = $"<color=#39FF14><b>{myName} (You)</b></color>: {msgToSend}";
            AddLog(localFormatted);

            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteString(myName);
                writer.WriteString(msgToSend);

                if (NetworkManager.Instance.IsHost)
                {
                    // Replicate to all clients
                    NetworkManager.Instance.SendToAll(MSG_CHAT, writer, true);
                }
                else if (NetworkManager.Instance.IsClient)
                {
                    // Client sends to server
                    NetworkManager.Instance.SendToServer(MSG_CHAT, writer, true);
                }
            }
        }

        private void OnChatMessageReceived(ulong senderId, MessageReader reader)
        {
            string senderName = reader.ReadString();
            string message = reader.ReadString();

            // Format message with light blue name badge for other players
            string remoteFormatted = $"<color=#00FFFF><b>{senderName}</b></color>: {message}";
            AddLog(remoteFormatted);

            // Host has to forward this message to other clients
            if (NetworkManager.Instance.IsHost)
            {
                using (MessageWriter writer = new MessageWriter())
                {
                    writer.WriteString(senderName);
                    writer.WriteString(message);

                    foreach (ulong clientConnId in NetworkManager.Instance.ConnectedClients)
                    {
                        if (clientConnId != senderId)
                        {
                            NetworkManager.Instance.SendToClient(clientConnId, MSG_CHAT, writer, true);
                        }
                    }
                }
            }
        }

        private void AddLog(string msg)
        {
            m_ChatLog.Add(msg);
            if (m_ChatLog.Count > 50)
            {
                m_ChatLog.RemoveAt(0);
            }
            m_ScrollPosition.y = float.MaxValue; // Auto scroll down
        }
    }
}
