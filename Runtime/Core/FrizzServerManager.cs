using System;
using UnityEngine;
using FrizzNet.Logging;
using FrizzNet.Steam;
using Steamworks;

namespace FrizzNet.Core
{
    /// <summary>
    /// Component that manages hosting parameters, lobby types, names, passwords, and player kicking.
    /// Acts as a central host/session manager.
    /// </summary>
    [DisallowMultipleComponent]
    public class FrizzServerManager : MonoBehaviour
    {
        public static FrizzServerManager Instance { get; private set; }

        [Header("Server / Session Settings")]
        [Tooltip("Maximum players allowed in the lobby.")]
        [Range(2, 64)]
        [SerializeField] private int m_MaxPlayers = 4;

        [Tooltip("The accessibility of the Steam lobby.")]
        [SerializeField] private ELobbyType m_LobbyType = ELobbyType.k_ELobbyTypePublic;

        [Tooltip("The public display name of the matchmaking lobby.")]
        [SerializeField] private string m_LobbyName = "FrizzNet Session";

        [Tooltip("Optional password required to join the session.")]
        [SerializeField] private string m_LobbyPassword = "";

        // Public properties
        public int MaxPlayers { get => m_MaxPlayers; set => m_MaxPlayers = value; }
        public ELobbyType LobbyType { get => m_LobbyType; set => m_LobbyType = value; }
        public string LobbyName { get => m_LobbyName; set => m_LobbyName = value; }
        public string LobbyPassword { get => m_LobbyPassword; set => m_LobbyPassword = value; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Starts hosting a new server session and creates a corresponding Steam lobby.
        /// </summary>
        public void StartServer()
        {
            FrizzLogger.LogNetwork($"[FrizzServerManager] Starting server lobby '{m_LobbyName}' with max players: {m_MaxPlayers}");
            
            // Hook into lobby creation to apply lobby settings like name and password.
            FrizzLobby.OnLobbyCreatedEvent += SetLobbyMetadata;

            // Create Steam lobby using the static FrizzLobby API
            FrizzLobby.Create(m_MaxPlayers, m_LobbyType);
        }

        private void SetLobbyMetadata(CSteamID lobbyId)
        {
            FrizzLobby.OnLobbyCreatedEvent -= SetLobbyMetadata;

            if (lobbyId != CSteamID.Nil)
            {
                FrizzLogger.LogNetwork($"[FrizzServerManager] Setting lobby metadata: Name='{m_LobbyName}'");
                SteamMatchmaking.SetLobbyData(lobbyId, "name", m_LobbyName);
                
                if (!string.IsNullOrEmpty(m_LobbyPassword))
                {
                    SteamMatchmaking.SetLobbyData(lobbyId, "password", m_LobbyPassword);
                }
            }
        }

        /// <summary>
        /// Stops hosting, leaves the Steam lobby, and disconnects all clients.
        /// </summary>
        public void StopServer()
        {
            FrizzLogger.LogNetwork("[FrizzServerManager] Stopping server and leaving lobby.");
            FrizzLobby.Leave();
        }

        /// <summary>
        /// Authoritatively kicks a player from the server session by Steam ID.
        /// Only valid if the local client is the Host.
        /// </summary>
        /// <param name="steamId">Steam ID64 of the client to kick.</param>
        /// <returns>True if the kick operation succeeded, false otherwise.</returns>
        public bool KickPlayer(ulong steamId)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost)
            {
                FrizzLogger.LogError("[FrizzServerManager] Kicking players is only valid on the Host.");
                return false;
            }

            FrizzLogger.LogNetwork($"[FrizzServerManager] Kicking client with SteamID: {steamId}");
            
            // Call transport level authoritative disconnect
            if (NetworkManager.Instance.Transport != null)
            {
                return NetworkManager.Instance.Transport.DisconnectClient(steamId);
            }

            return false;
        }
    }
}
