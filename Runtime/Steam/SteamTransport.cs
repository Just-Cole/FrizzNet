using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Steamworks;
using FrizzNet.Transport;
using FrizzNet.Logging;
using FrizzNet.Core;

namespace FrizzNet.Steam
{
    /// <summary>
    /// Steamworks implementation of the INetworkTransport interface.
    /// Uses Steam Networking Sockets for connection-oriented P2P messaging.
    /// Handles Steam matchmaking lobby callback hooks.
    /// </summary>
    [FrizzHelp("Steamworks.NET transport implementation using raw Steam Networking Sockets. Handles connections, callback queues, and data transmission.", "index.html#SteamTransport")]
    public class SteamTransport : MonoBehaviour, INetworkTransport
    {
        public static SteamTransport Instance { get; private set; }

        [Header("Transport Settings")]
        [Tooltip("Virtual port used for Steam Networking Sockets connections.")]
        [SerializeField] private int m_VirtualPort = 0;

        [Tooltip("If checked, automatically start hosting or client connections when creating or entering lobbies.")]
        [SerializeField] private bool m_AutoConnectToLobbyOwner = true;

        // INetworkTransport Events
        public event Action<TransportConnection> OnClientConnected;
        public event Action<TransportConnection> OnClientDisconnected;
        public event Action<TransportConnection, byte[], int> OnDataReceived;
        public event Action OnConnectedToServer;
        public event Action OnDisconnectedFromServer;

        // Sockets Handles
        private HSteamListenSocket m_ListenSocket = HSteamListenSocket.Invalid;
        private HSteamNetConnection m_ServerConnection = HSteamNetConnection.Invalid;
        private ulong m_ServerConnectionId;

        // Connection Mapping
        private readonly Dictionary<ulong, HSteamNetConnection> m_SteamIdToConnection = new Dictionary<ulong, HSteamNetConnection>();
        private readonly Dictionary<HSteamNetConnection, ulong> m_ConnectionToSteamId = new Dictionary<HSteamNetConnection, ulong>();

        // Callbacks (stored as member variables to prevent GC collection)
        private Callback<SteamNetConnectionStatusChangedCallback_t> m_ConnectionStatusChanged;
        private Callback<LobbyCreated_t> m_LobbyCreated;
        private Callback<LobbyEnter_t> m_LobbyEntered;
        private Callback<LobbyChatUpdate_t> m_LobbyChatUpdate;
        private Callback<GameLobbyJoinRequested_t> m_GameLobbyJoinRequested;
        private Callback<LobbyDataUpdate_t> m_LobbyDataUpdate;

        private CSteamID m_LastLobbyOwner = CSteamID.Nil;

        public bool IsHost => m_ListenSocket != HSteamListenSocket.Invalid;
        public bool IsClient => m_ServerConnection != HSteamNetConnection.Invalid;

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

        private void Start()
        {
            // Ensure Steamworks is initialized
            SteamManager.EnsureInstance();

            if (!SteamManager.Initialized)
            {
                FrizzLogger.LogError("SteamTransport failed: SteamManager is not initialized.");
                return;
            }

            // Register Steam Callbacks
            m_ConnectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
            m_LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            m_LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            m_LobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            m_GameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            m_LobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);

            // Subscribe to Lobby Left events for automatic socket cleanup
            FrizzLobby.OnLobbyLeftEvent += HandleLobbyLeft;

            FrizzLogger.LogInfo("SteamTransport callbacks registered.");
        }

        private void Update()
        {
            // Periodically poll events if active
            PollEvents();
        }

        private void OnDestroy()
        {
            FrizzLobby.OnLobbyLeftEvent -= HandleLobbyLeft;

            if (Instance == this)
            {
                Instance = null;
            }

            Disconnect();
            StopHost();
        }

        #region INetworkTransport Implementation

        public bool StartHost(int maxPlayers)
        {
            if (!SteamManager.Initialized)
            {
                FrizzLogger.LogError("Cannot start host: Steam not initialized.");
                return false;
            }

            if (IsHost)
            {
                FrizzLogger.LogWarning("Already hosting.");
                return true;
            }

            FrizzLogger.LogNetwork($"Starting Host on virtual port {m_VirtualPort}...");

            // Create Steam listen socket
            m_ListenSocket = SteamNetworkingSockets.CreateListenSocketP2P(m_VirtualPort, 0, null);
            if (m_ListenSocket == HSteamListenSocket.Invalid)
            {
                FrizzLogger.LogError("Failed to create Steam P2P listen socket.");
                return false;
            }

            FrizzLogger.LogInfo($"Host started successfully. ListenSocket: {m_ListenSocket}");
            return true;
        }

        public void StopHost()
        {
            if (!IsHost) return;

            FrizzLogger.LogNetwork("Stopping Host...");

            // Close all client connections
            foreach (var hConn in m_SteamIdToConnection.Values)
            {
                SteamNetworkingSockets.CloseConnection(hConn, 0, "Host Shutdown", false);
            }

            m_SteamIdToConnection.Clear();
            m_ConnectionToSteamId.Clear();

            // Close listen socket
            SteamNetworkingSockets.CloseListenSocket(m_ListenSocket);
            m_ListenSocket = HSteamListenSocket.Invalid;

            FrizzLogger.LogInfo("Host stopped.");
        }

        public bool StartClient(string hostAddress)
        {
            if (!SteamManager.Initialized)
            {
                FrizzLogger.LogError("Cannot connect client: Steam not initialized.");
                return false;
            }

            if (IsClient)
            {
                FrizzLogger.LogWarning("Client is already active or connecting.");
                return true;
            }

            if (!ulong.TryParse(hostAddress, out ulong hostSteamId))
            {
                FrizzLogger.LogError($"Invalid host address '{hostAddress}'. Must be a valid SteamID64.");
                return false;
            }

            CSteamID remoteSteamId = new CSteamID(hostSteamId);
            FrizzLogger.LogNetwork($"Client connecting to host SteamID: {remoteSteamId}...");

            SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
            identity.SetSteamID(remoteSteamId);

            // Connect
            m_ServerConnection = SteamNetworkingSockets.ConnectP2P(ref identity, m_VirtualPort, 0, null);
            if (m_ServerConnection == HSteamNetConnection.Invalid)
            {
                FrizzLogger.LogError("Failed to initiate connection to host.");
                return false;
            }

            m_ServerConnectionId = hostSteamId;
            FrizzLogger.LogInfo($"Client connection initiated. ConnectionHandle: {m_ServerConnection}");
            return true;
        }

        public void Disconnect()
        {
            if (!IsClient) return;

            FrizzLogger.LogNetwork("Disconnecting client from server...");
            SteamNetworkingSockets.CloseConnection(m_ServerConnection, 0, "Client Disconnect", false);
            m_ServerConnection = HSteamNetConnection.Invalid;
            m_ServerConnectionId = 0;

            OnDisconnectedFromServer?.Invoke();
            FrizzLogger.LogInfo("Client disconnected.");
        }

        public bool SendToServer(byte[] data, int size, bool reliable = true)
        {
            if (!IsClient)
            {
                FrizzLogger.LogWarning("Cannot send to server: Client is not active.");
                return false;
            }
            return Send(m_ServerConnection, data, size, reliable);
        }

        public bool SendToClient(ulong connectionId, byte[] data, int size, bool reliable = true)
        {
            if (!IsHost)
            {
                FrizzLogger.LogWarning("Cannot send to client: Server is not hosting.");
                return false;
            }

            if (m_SteamIdToConnection.TryGetValue(connectionId, out HSteamNetConnection hConn))
            {
                return Send(hConn, data, size, reliable);
            }

            FrizzLogger.LogWarning($"Cannot send to client {connectionId}: Connection not found.");
            return false;
        }

        public void PollEvents()
        {
            if (!SteamManager.Initialized) return;

            // Poll incoming server messages
            if (IsClient && m_ServerConnection != HSteamNetConnection.Invalid)
            {
                ReceiveMessages(m_ServerConnection, m_ServerConnectionId);
            }

            // Poll incoming client messages on the host
            if (IsHost && m_SteamIdToConnection.Count > 0)
            {
                foreach (var pair in m_SteamIdToConnection)
                {
                    ReceiveMessages(pair.Value, pair.Key);
                }
            }
        }

        #endregion

        #region Internal Messaging Logic

        private bool Send(HSteamNetConnection hConn, byte[] data, int size, bool reliable)
        {
            if (hConn == HSteamNetConnection.Invalid) return false;

            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(data, 0, ptr, size);
                int flags = reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable;

                EResult result = SteamNetworkingSockets.SendMessageToConnection(hConn, ptr, (uint)size, flags, out _);
                return result == EResult.k_EResultOK;
            }
            catch (Exception e)
            {
                FrizzLogger.LogError($"Exception during Send: {e.Message}");
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private void ReceiveMessages(HSteamNetConnection hConn, ulong remoteId)
        {
            const int maxMessages = 32;
            IntPtr[] ptrBuffer = new IntPtr[maxMessages];

            int messageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(hConn, ptrBuffer, maxMessages);
            if (messageCount <= 0) return;

            for (int i = 0; i < messageCount; i++)
            {
                try
                {
                    SteamNetworkingMessage_t netMessage = Marshal.PtrToStructure<SteamNetworkingMessage_t>(ptrBuffer[i]);
                    byte[] data = new byte[netMessage.m_cbSize];
                    Marshal.Copy(netMessage.m_pData, data, 0, netMessage.m_cbSize);

                    TransportConnection conn = new TransportConnection
                    {
                        ConnectionId = remoteId,
                        Address = remoteId.ToString()
                    };

                    OnDataReceived?.Invoke(conn, data, data.Length);
                }
                catch (Exception e)
                {
                    FrizzLogger.LogError($"Error reading networking message: {e.Message}");
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(ptrBuffer[i]);
                }
            }
        }

        #endregion

        #region Steam Callbacks

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t callback)
        {
            HSteamNetConnection hConn = callback.m_hConn;
            SteamNetConnectionInfo_t info = callback.m_info;
            ESteamNetworkingConnectionState state = info.m_eState;

            CSteamID remoteSteamId = info.m_identityRemote.GetSteamID();
            ulong remoteId = remoteSteamId.m_SteamID;

            FrizzLogger.LogNetwork($"Connection {hConn} state changed: {callback.m_eOldState} -> {state}");

            // Server Logic
            if (IsHost && m_ListenSocket != HSteamListenSocket.Invalid)
            {
                switch (state)
                {
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                        FrizzLogger.LogNetwork($"Incoming connection request from {remoteSteamId}. Accepting...");
                        EResult result = SteamNetworkingSockets.AcceptConnection(hConn);
                        if (result != EResult.k_EResultOK)
                        {
                            FrizzLogger.LogError($"Failed to accept connection: {result}");
                            SteamNetworkingSockets.CloseConnection(hConn, 0, "Accept failed", false);
                        }
                        break;

                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                        FrizzLogger.LogNetwork($"Client {remoteSteamId} successfully connected.");
                        m_SteamIdToConnection[remoteId] = hConn;
                        m_ConnectionToSteamId[hConn] = remoteId;

                        OnClientConnected?.Invoke(new TransportConnection
                        {
                            ConnectionId = remoteId,
                            Address = remoteId.ToString()
                        });
                        break;

                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                        FrizzLogger.LogNetwork($"Client {remoteSteamId} disconnected. (State: {state})");
                        m_SteamIdToConnection.Remove(remoteId);
                        m_ConnectionToSteamId.Remove(hConn);

                        SteamNetworkingSockets.CloseConnection(hConn, 0, "Closed by peer/locally", false);

                        OnClientDisconnected?.Invoke(new TransportConnection
                        {
                            ConnectionId = remoteId,
                            Address = remoteId.ToString()
                        });
                        break;
                }
            }
            // Client Logic
            else if (hConn == m_ServerConnection)
            {
                switch (state)
                {
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                        FrizzLogger.LogNetwork($"Successfully connected to Host: {remoteSteamId}");
                        OnConnectedToServer?.Invoke();
                        break;

                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                        FrizzLogger.LogNetwork($"Disconnected from Host. (State: {state}, Reason: {info.m_eEndReason})");
                        SteamNetworkingSockets.CloseConnection(m_ServerConnection, 0, "Closed by peer/locally", false);
                        m_ServerConnection = HSteamNetConnection.Invalid;
                        m_ServerConnectionId = 0;

                        OnDisconnectedFromServer?.Invoke();
                        break;
                }
            }
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult == EResult.k_EResultOK)
            {
                CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
                FrizzLogger.LogNetwork($"Lobby successfully created on Steam: {lobbyId}");

                m_LastLobbyOwner = SteamUser.GetSteamID();
                FrizzLobby.TriggerLobbyCreated(lobbyId);

                // Auto connect/host
                if (m_AutoConnectToLobbyOwner)
                {
                    StartHost(8); // Default size 8
                }
            }
            else
            {
                FrizzLogger.LogError($"Failed to create lobby. EResult: {callback.m_eResult}");
            }
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            FrizzLogger.LogNetwork($"Entered lobby: {lobbyId}");

            CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobbyId);
            m_LastLobbyOwner = owner;

            FrizzLobby.TriggerLobbyJoined(lobbyId);

            // Auto connect/host
            if (m_AutoConnectToLobbyOwner)
            {
                CSteamID mySteamId = SteamUser.GetSteamID();

                if (owner != mySteamId)
                {
                    // If we are joining someone else's lobby, connect to them
                    StartClient(owner.m_SteamID.ToString());
                }
            }
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            CSteamID userChanged = new CSteamID(callback.m_ulSteamIDUserChanged);
            EChatMemberStateChange stateChange = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;

            bool joined = stateChange == EChatMemberStateChange.k_EChatMemberStateChangeEntered;
            FrizzLogger.LogNetwork($"Lobby member update: {userChanged} state changed to {stateChange}");

            FrizzLobby.TriggerLobbyMemberChanged(lobbyId, userChanged, joined);
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            FrizzLogger.LogNetwork($"Received Steam game join request for lobby: {callback.m_steamIDLobby}");
            FrizzLobby.Join(callback.m_steamIDLobby);
        }

        private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
        {
            CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            CSteamID memberId = new CSteamID(callback.m_ulSteamIDMember);

            // Notify FrizzLobby that data changed so the UI can update
            FrizzLobby.TriggerLobbyDataUpdate(lobbyId, memberId);

            // Check if lobby owner has changed
            CSteamID currentOwner = SteamMatchmaking.GetLobbyOwner(lobbyId);
            if (m_LastLobbyOwner != currentOwner && currentOwner != CSteamID.Nil)
            {
                CSteamID oldOwner = m_LastLobbyOwner;
                m_LastLobbyOwner = currentOwner;

                FrizzLogger.LogNetwork($"Lobby owner changed: {oldOwner} -> {currentOwner}");
                FrizzLobby.TriggerLobbyOwnerChanged(lobbyId, oldOwner, currentOwner);

                // Host Migration Check
                // If the new owner is the local player, and we are not currently hosting, start hosting!
                if (currentOwner == SteamUser.GetSteamID())
                {
                    if (!IsHost)
                    {
                        FrizzLogger.LogNetwork("Local player is now the Lobby Owner. Initiating Host Migration...");
                        StartHost(8); // Start P2P listen socket
                    }
                }
                else
                {
                    // If the owner changed to someone else, and we were hosting, stop hosting and connect to the new owner
                    if (IsHost)
                    {
                        StopHost();
                    }
                    if (m_AutoConnectToLobbyOwner)
                    {
                        Disconnect();
                        StartClient(currentOwner.m_SteamID.ToString());
                    }
                }
            }
        }

        private void HandleLobbyLeft()
        {
            FrizzLogger.LogNetwork("Lobby left event detected in Transport. Cleaning up socket connections...");
            StopHost();
            Disconnect();
        }

        #endregion
    }
}
