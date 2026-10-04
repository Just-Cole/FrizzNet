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
    /// Dedicated-authority Steam transport using GameServerNetworkingSockets.
    /// StartClient is unused; players connect with SteamTransport.ConnectP2P to the game server Steam ID.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FrizzNet/Steam Game Server Transport")]
    public class SteamGameServerTransport : MonoBehaviour, INetworkTransport
    {
        public static SteamGameServerTransport Instance { get; private set; }

        [Header("Transport Settings")]
        [Tooltip("Virtual port used for Game Server P2P listen sockets.")]
        [SerializeField] private int m_VirtualPort;

        public event Action<TransportConnection> OnClientConnected;
        public event Action<TransportConnection> OnClientDisconnected;
        public event Action<TransportConnection, byte[], int> OnDataReceived;
        public event Action OnConnectedToServer;
        public event Action OnDisconnectedFromServer;

        private HSteamListenSocket m_ListenSocket = HSteamListenSocket.Invalid;
        private readonly Dictionary<ulong, HSteamNetConnection> m_SteamIdToConnection = new Dictionary<ulong, HSteamNetConnection>();
        private readonly Dictionary<HSteamNetConnection, ulong> m_ConnectionToSteamId = new Dictionary<HSteamNetConnection, ulong>();
        private Callback<SteamNetConnectionStatusChangedCallback_t> m_ConnectionStatusChanged;

        public int VirtualPort { get => m_VirtualPort; set => m_VirtualPort = value; }
        public bool IsHost => m_ListenSocket != HSteamListenSocket.Invalid;
        public bool IsClient => false;

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
            if (!SteamGameServerManager.Initialized)
                SteamGameServerManager.EnsureInstance();

            if (!SteamGameServerManager.Initialized)
            {
                FrizzLogger.LogError("SteamGameServerTransport failed: SteamGameServerManager is not initialized.");
                return;
            }

            m_ConnectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.CreateGameServer(OnConnectionStatusChanged);
            ApplyLocalSteamId();
            SteamGameServerManager.LoggedOnChanged += HandleLoggedOnChanged;
            FrizzLogger.LogInfo("SteamGameServerTransport Game Server callbacks registered.");
        }

        private void Update()
        {
            PollEvents();
        }

        private void OnDestroy()
        {
            SteamGameServerManager.LoggedOnChanged -= HandleLoggedOnChanged;

            if (Instance == this)
                Instance = null;

            StopHost();
        }

        public bool StartHost(int maxPlayers)
        {
            if (!SteamGameServerManager.Initialized || !SteamGameServerManager.LoggedOn)
            {
                FrizzLogger.LogError("Cannot start dedicated host: Steam Game Server is not logged on.");
                return false;
            }

            if (IsHost)
            {
                FrizzLogger.LogWarning("Dedicated host is already listening.");
                return true;
            }

            ApplyLocalSteamId();
            SteamGameServer.SetMaxPlayerCount(Mathf.Clamp(maxPlayers, 1, 64));

            FrizzLogger.LogNetwork("Starting dedicated Game Server listen socket on virtual port " + m_VirtualPort + "...");
            m_ListenSocket = SteamGameServerNetworkingSockets.CreateListenSocketP2P(m_VirtualPort, 0, null);
            if (m_ListenSocket == HSteamListenSocket.Invalid)
            {
                FrizzLogger.LogError("Failed to create Steam Game Server P2P listen socket.");
                return false;
            }

            SteamGameServer.SetAdvertiseServerActive(true);
            FrizzLogger.LogInfo("Dedicated host started. ListenSocket: " + m_ListenSocket +
                                " Steam ID: " + SteamGameServerManager.SteamId.m_SteamID);
            return true;
        }

        public void StopHost()
        {
            if (!IsHost)
                return;

            FrizzLogger.LogNetwork("Stopping dedicated Game Server host...");
            foreach (HSteamNetConnection hConn in m_SteamIdToConnection.Values)
                SteamGameServerNetworkingSockets.CloseConnection(hConn, 0, "Host Shutdown", false);

            m_SteamIdToConnection.Clear();
            m_ConnectionToSteamId.Clear();
            SteamGameServerNetworkingSockets.CloseListenSocket(m_ListenSocket);
            m_ListenSocket = HSteamListenSocket.Invalid;
            FrizzLogger.LogInfo("Dedicated host stopped.");
        }

        public bool StartClient(string hostAddress)
        {
            FrizzLogger.LogWarning("SteamGameServerTransport is a dedicated host transport. Clients must use SteamTransport.");
            return false;
        }

        public void Disconnect()
        {
        }

        public bool SendToServer(byte[] data, int size, bool reliable = true)
        {
            FrizzLogger.LogWarning("Cannot send to server: SteamGameServerTransport is host-only.");
            return false;
        }

        public bool SendToClient(ulong connectionId, byte[] data, int size, bool reliable = true)
        {
            if (!IsHost)
            {
                FrizzLogger.LogWarning("Cannot send to client: Dedicated server is not hosting.");
                return false;
            }

            if (m_SteamIdToConnection.TryGetValue(connectionId, out HSteamNetConnection hConn))
                return Send(hConn, data, size, reliable);

            FrizzLogger.LogWarning("Cannot send to client " + connectionId + ": Connection not found.");
            return false;
        }

        public bool DisconnectClient(ulong connectionId)
        {
            if (!IsHost)
            {
                FrizzLogger.LogWarning("Cannot disconnect client: Not hosting.");
                return false;
            }

            if (!m_SteamIdToConnection.TryGetValue(connectionId, out HSteamNetConnection hConn))
            {
                FrizzLogger.LogWarning("DisconnectClient failed: Connection ID " + connectionId + " not found.");
                return false;
            }

            FrizzLogger.LogNetwork("Disconnecting client " + connectionId + " (Kicked by Host)...");
            bool success = SteamGameServerNetworkingSockets.CloseConnection(hConn, 0, "Kicked by Host", false);
            if (success)
            {
                m_SteamIdToConnection.Remove(connectionId);
                m_ConnectionToSteamId.Remove(hConn);
                OnClientDisconnected?.Invoke(new TransportConnection
                {
                    ConnectionId = connectionId,
                    Address = connectionId.ToString()
                });
            }

            return success;
        }

        public void PollEvents()
        {
            if (!SteamGameServerManager.Initialized || m_SteamIdToConnection.Count == 0)
                return;

            foreach (KeyValuePair<ulong, HSteamNetConnection> pair in m_SteamIdToConnection)
                ReceiveMessages(pair.Value, pair.Key);
        }

        private void HandleLoggedOnChanged(bool loggedOn)
        {
            if (loggedOn)
                ApplyLocalSteamId();
        }

        private static void ApplyLocalSteamId()
        {
            if (!SteamGameServerManager.LoggedOn)
                return;

            NetworkManager.SetLocalConnectionId(SteamGameServerManager.SteamId.m_SteamID);
        }

        private static bool Send(HSteamNetConnection hConn, byte[] data, int size, bool reliable)
        {
            if (hConn == HSteamNetConnection.Invalid)
                return false;

            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(data, 0, ptr, size);
                int flags = reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable;
                EResult result = SteamGameServerNetworkingSockets.SendMessageToConnection(hConn, ptr, (uint)size, flags, out _);
                return result == EResult.k_EResultOK;
            }
            catch (Exception e)
            {
                FrizzLogger.LogError("Exception during Game Server Send: " + e.Message);
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
            int messageCount = SteamGameServerNetworkingSockets.ReceiveMessagesOnConnection(hConn, ptrBuffer, maxMessages);
            if (messageCount <= 0)
                return;

            for (int i = 0; i < messageCount; i++)
            {
                try
                {
                    SteamNetworkingMessage_t netMessage = Marshal.PtrToStructure<SteamNetworkingMessage_t>(ptrBuffer[i]);
                    byte[] data = new byte[netMessage.m_cbSize];
                    Marshal.Copy(netMessage.m_pData, data, 0, netMessage.m_cbSize);

                    OnDataReceived?.Invoke(new TransportConnection
                    {
                        ConnectionId = remoteId,
                        Address = remoteId.ToString()
                    }, data, data.Length);
                }
                catch (Exception e)
                {
                    FrizzLogger.LogError("Error reading Game Server networking message: " + e.Message);
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(ptrBuffer[i]);
                }
            }
        }

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t callback)
        {
            HSteamNetConnection hConn = callback.m_hConn;
            SteamNetConnectionInfo_t info = callback.m_info;
            ESteamNetworkingConnectionState state = info.m_eState;
            CSteamID remoteSteamId = info.m_identityRemote.GetSteamID();
            ulong remoteId = remoteSteamId.m_SteamID;

            FrizzLogger.LogNetwork("Game Server connection " + hConn + " state changed: " + callback.m_eOldState + " -> " + state);

            if (!IsHost || m_ListenSocket == HSteamListenSocket.Invalid)
                return;

            switch (state)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    FrizzLogger.LogNetwork("Incoming dedicated connection from " + remoteSteamId + ". Accepting...");
                    EResult result = SteamGameServerNetworkingSockets.AcceptConnection(hConn);
                    if (result != EResult.k_EResultOK)
                    {
                        FrizzLogger.LogError("Failed to accept Game Server connection: " + result);
                        SteamGameServerNetworkingSockets.CloseConnection(hConn, 0, "Accept failed", false);
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    FrizzLogger.LogNetwork("Client " + remoteSteamId + " connected to dedicated server.");
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
                    FrizzLogger.LogNetwork("Client " + remoteSteamId + " disconnected from dedicated server. (State: " + state + ")");
                    m_SteamIdToConnection.Remove(remoteId);
                    m_ConnectionToSteamId.Remove(hConn);
                    SteamGameServerNetworkingSockets.CloseConnection(hConn, 0, "Closed by peer/locally", false);
                    OnClientDisconnected?.Invoke(new TransportConnection
                    {
                        ConnectionId = remoteId,
                        Address = remoteId.ToString()
                    });
                    break;
            }
        }
    }
}
