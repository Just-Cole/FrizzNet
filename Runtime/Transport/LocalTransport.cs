using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using FrizzNet.Logging;
using FrizzNet.Core;

namespace FrizzNet.Transport
{
    /// <summary>
    /// TCP localhost transport for offline multiplayer testing without Steam.
    /// Run two game instances on the same machine: one hosts, one connects to 127.0.0.1.
    /// </summary>
    [DisallowMultipleComponent]
    [FrizzHelp("Local TCP transport for testing multiplayer on one machine without Steam. Host on a port, connect a second instance to 127.0.0.1.", "index.html#LocalTransport")]
    public class LocalTransport : MonoBehaviour, INetworkTransport
    {
        public static LocalTransport Instance { get; private set; }

        public const ulong HostConnectionId = 1;

        [Header("Local Transport Settings")]
        [Tooltip("Default port used when hosting or connecting with a bare port number.")]
        [SerializeField] private int m_DefaultPort = 7777;

        [Tooltip("Address clients connect to. Use 127.0.0.1 for same-machine testing.")]
        [SerializeField] private string m_DefaultAddress = "127.0.0.1";

        [Tooltip("Maximum simultaneous client connections when hosting.")]
        [SerializeField] private int m_MaxConnections = 8;

        public event Action<TransportConnection> OnClientConnected;
        public event Action<TransportConnection> OnClientDisconnected;
        public event Action<TransportConnection, byte[], int> OnDataReceived;
        public event Action OnConnectedToServer;
        public event Action OnDisconnectedFromServer;

        private TcpListener m_Listener;
        private readonly Dictionary<ulong, ClientConnection> m_Clients = new Dictionary<ulong, ClientConnection>();
        private ClientConnection m_ServerLink;
        private ulong m_NextClientId = 2;
        private int m_MaxPlayers;
        private bool m_ClientDisconnectNotified;

        private readonly ConcurrentQueue<Action> m_MainThreadQueue = new ConcurrentQueue<Action>();

        public bool IsHost => m_Listener != null;
        public bool IsClient => m_ServerLink != null;
        public int ActivePort { get; private set; }
        public string ActiveAddress { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            DrainMainThreadQueue();
            PollEvents();
        }

        private void OnDestroy()
        {
            ShutdownAll();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool StartHost(int maxPlayers)
        {
            return StartHost(m_DefaultAddress, m_DefaultPort, maxPlayers);
        }

        /// <summary>
        /// Starts listening for local TCP connections on the given port.
        /// </summary>
        public bool StartHost(string address, int port, int maxPlayers = -1)
        {
            if (IsHost)
            {
                FrizzLogger.LogWarning("[LocalTransport] Already hosting.");
                return true;
            }

            if (IsClient)
            {
                FrizzLogger.LogError("[LocalTransport] Cannot host while connected as a client.");
                return false;
            }

            m_MaxPlayers = maxPlayers > 0 ? maxPlayers : m_MaxConnections;
            ActiveAddress = address;
            ActivePort = port;

            try
            {
                IPAddress ip = address == "localhost" ? IPAddress.Loopback : IPAddress.Parse(address);
                m_Listener = new TcpListener(ip, port);
                m_Listener.Start();
            }
            catch (Exception e)
            {
                FrizzLogger.LogError($"[LocalTransport] Failed to start host on {address}:{port} - {e.Message}");
                m_Listener = null;
                return false;
            }

            NetworkManager.SetLocalConnectionId(HostConnectionId);
            FrizzLogger.LogNetwork($"[LocalTransport] Hosting on {address}:{port} (max {m_MaxPlayers} clients).");
            return true;
        }

        public void StopHost()
        {
            if (!IsHost) return;

            foreach (ClientConnection client in m_Clients.Values)
            {
                client.Close();
            }
            m_Clients.Clear();

            if (m_Listener != null)
            {
                m_Listener.Stop();
                m_Listener = null;
            }

            m_NextClientId = 2;
            FrizzLogger.LogNetwork("[LocalTransport] Host stopped.");
        }

        public bool StartClient(string hostAddress)
        {
            if (!TryParseAddress(hostAddress, out string address, out int port))
            {
                address = m_DefaultAddress;
                port = m_DefaultPort;
            }

            return StartClient(address, port);
        }

        /// <summary>
        /// Connects to a local host at address:port.
        /// </summary>
        public bool StartClient(string address, int port)
        {
            if (IsClient)
            {
                FrizzLogger.LogWarning("[LocalTransport] Already connected as client.");
                return true;
            }

            if (IsHost)
            {
                FrizzLogger.LogError("[LocalTransport] Cannot connect as client while hosting.");
                return false;
            }

            ActiveAddress = address;
            ActivePort = port;
            m_ClientDisconnectNotified = false;

            try
            {
                TcpClient tcpClient = new TcpClient();
                tcpClient.Connect(address, port);

                m_ServerLink = new ClientConnection(tcpClient, 0, this);
                m_ServerLink.BeginReceive();
            }
            catch (Exception e)
            {
                FrizzLogger.LogError($"[LocalTransport] Failed to connect to {address}:{port} - {e.Message}");
                m_ServerLink = null;
                return false;
            }

            FrizzLogger.LogNetwork($"[LocalTransport] Connected to {address}:{port}. Waiting for host handshake...");
            return true;
        }

        public void Disconnect()
        {
            if (!IsClient) return;

            m_ServerLink?.Close();
            m_ServerLink = null;
            FrizzLogger.LogNetwork("[LocalTransport] Client disconnected.");
        }

        public bool SendToServer(byte[] data, int size, bool reliable = true)
        {
            if (!IsClient || m_ServerLink == null) return false;
            return m_ServerLink.Send(data, size);
        }

        public bool SendToClient(ulong connectionId, byte[] data, int size, bool reliable = true)
        {
            if (!IsHost) return false;

            if (m_Clients.TryGetValue(connectionId, out ClientConnection client))
            {
                return client.Send(data, size);
            }

            FrizzLogger.LogWarning($"[LocalTransport] Client {connectionId} not found.");
            return false;
        }

        public bool DisconnectClient(ulong connectionId)
        {
            if (!IsHost) return false;

            if (m_Clients.TryGetValue(connectionId, out ClientConnection client))
            {
                client.Close();
                m_Clients.Remove(connectionId);
                OnClientDisconnected?.Invoke(new TransportConnection
                {
                    ConnectionId = connectionId,
                    Address = client.Address
                });
                return true;
            }

            return false;
        }

        public void PollEvents()
        {
            if (IsHost && m_Listener != null)
            {
                while (m_Listener.Pending())
                {
                    if (m_Clients.Count >= m_MaxPlayers)
                    {
                        TcpClient rejected = m_Listener.AcceptTcpClient();
                        rejected.Close();
                        FrizzLogger.LogWarning("[LocalTransport] Connection rejected: host is full.");
                        continue;
                    }

                    TcpClient accepted = m_Listener.AcceptTcpClient();
                    ulong clientId = m_NextClientId++;
                    ClientConnection connection = new ClientConnection(accepted, clientId, this);
                    m_Clients.Add(clientId, connection);
                    connection.BeginReceive();
                    SendHandshake(connection);

                    string endpoint = accepted.Client.RemoteEndPoint.ToString();
                    FrizzLogger.LogNetwork($"[LocalTransport] Client {clientId} connected from {endpoint}.");

                    OnClientConnected?.Invoke(new TransportConnection
                    {
                        ConnectionId = clientId,
                        Address = endpoint
                    });
                }
            }

            if (IsHost)
            {
                foreach (ClientConnection client in m_Clients.Values)
                {
                    client.PollReceive();
                }
            }

            if (IsClient && m_ServerLink != null)
            {
                m_ServerLink.PollReceive();
            }
        }

        internal void EnqueueMainThread(Action action)
        {
            if (action != null)
            {
                m_MainThreadQueue.Enqueue(action);
            }
        }

        private void HandleClientDisconnected(ClientConnection connection)
        {
            if (connection == null) return;

            if (IsHost && connection.ConnectionId != 0)
            {
                if (m_Clients.Remove(connection.ConnectionId))
                {
                    OnClientDisconnected?.Invoke(new TransportConnection
                    {
                        ConnectionId = connection.ConnectionId,
                        Address = connection.Address
                    });
                }
            }
            else if (IsClient && connection == m_ServerLink && !m_ClientDisconnectNotified)
            {
                m_ClientDisconnectNotified = true;
                m_ServerLink = null;
                OnDisconnectedFromServer?.Invoke();
            }
        }

        internal void HandleClientConnectedToHost(ulong assignedClientId)
        {
            NetworkManager.SetLocalConnectionId(assignedClientId);
            EnqueueMainThread(() =>
            {
                OnConnectedToServer?.Invoke();
                FrizzLocalSession.NotifyClientSessionStarted();
            });
        }

        internal void HandleDataReceived(TransportConnection connection, byte[] data, int size)
        {
            EnqueueMainThread(() => OnDataReceived?.Invoke(connection, data, size));
        }

        private void DrainMainThreadQueue()
        {
            while (m_MainThreadQueue.TryDequeue(out Action action))
            {
                action?.Invoke();
            }
        }

        private void ShutdownAll()
        {
            Disconnect();
            StopHost();
        }

        private bool TryParseAddress(string hostAddress, out string address, out int port)
        {
            address = m_DefaultAddress;
            port = m_DefaultPort;

            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                return false;
            }

            hostAddress = hostAddress.Trim();

            if (int.TryParse(hostAddress, out int portOnly))
            {
                port = portOnly;
                return true;
            }

            int colonIndex = hostAddress.LastIndexOf(':');
            if (colonIndex > 0 && int.TryParse(hostAddress.Substring(colonIndex + 1), out int parsedPort))
            {
                address = hostAddress.Substring(0, colonIndex);
                port = parsedPort;
                return true;
            }

            address = hostAddress;
            return true;
        }

        private sealed class ClientConnection
        {
            private readonly LocalTransport m_Owner;
            private readonly TcpClient m_Client;
            private readonly NetworkStream m_Stream;
            private readonly byte[] m_ReceiveBuffer = new byte[8192];
            private readonly MemoryStream m_MessageBuffer = new MemoryStream();
            private readonly object m_SendLock = new object();
            private bool m_IsClosed;

            public ulong ConnectionId { get; }
            public string Address { get; }

            public ClientConnection(TcpClient client, ulong connectionId, LocalTransport owner)
            {
                m_Client = client;
                m_Owner = owner;
                ConnectionId = connectionId;
                m_Stream = client.GetStream();
                Address = client.Client.RemoteEndPoint != null
                    ? client.Client.RemoteEndPoint.ToString()
                    : "local";
            }

            public void BeginReceive()
            {
                if (m_IsClosed || !m_Stream.CanRead) return;

                try
                {
                    m_Stream.BeginRead(m_ReceiveBuffer, 0, m_ReceiveBuffer.Length, OnReceive, null);
                }
                catch (Exception e)
                {
                    FrizzLogger.LogWarning($"[LocalTransport] Receive error: {e.Message}");
                    Close();
                }
            }

            public void PollReceive()
            {
                // Async receive handles most work; PollEvents keeps accept loop responsive.
            }

            public bool Send(byte[] data, int size)
            {
                if (m_IsClosed || data == null || size <= 0) return false;

                lock (m_SendLock)
                {
                    try
                    {
                        byte[] lengthPrefix = BitConverter.GetBytes(size);
                        m_Stream.Write(lengthPrefix, 0, sizeof(int));
                        m_Stream.Write(data, 0, size);
                        m_Stream.Flush();
                        return true;
                    }
                    catch (Exception e)
                    {
                        FrizzLogger.LogWarning($"[LocalTransport] Send failed: {e.Message}");
                        Close();
                        return false;
                    }
                }
            }

            public void Close()
            {
                if (m_IsClosed) return;
                m_IsClosed = true;

                try
                {
                    m_Stream?.Close();
                    m_Client?.Close();
                }
                catch
                {
                    // ignored during shutdown
                }

                m_Owner.HandleClientDisconnected(this);
            }

            private void OnReceive(IAsyncResult result)
            {
                if (m_IsClosed) return;

                int bytesRead;
                try
                {
                    bytesRead = m_Stream.EndRead(result);
                }
                catch (Exception)
                {
                    Close();
                    return;
                }

                if (bytesRead <= 0)
                {
                    Close();
                    return;
                }

                ProcessReceivedBytes(m_ReceiveBuffer, bytesRead);
                BeginReceive();
            }

            private void ProcessReceivedBytes(byte[] buffer, int count)
            {
                m_MessageBuffer.Write(buffer, 0, count);

                while (true)
                {
                    if (m_MessageBuffer.Length < sizeof(int)) break;

                    byte[] allBytes = m_MessageBuffer.ToArray();
                    int messageSize = BitConverter.ToInt32(allBytes, 0);
                    if (messageSize <= 0 || messageSize > 1024 * 1024)
                    {
                        FrizzLogger.LogError("[LocalTransport] Invalid message size. Closing connection.");
                        Close();
                        return;
                    }

                    if (allBytes.Length < sizeof(int) + messageSize) break;

                    byte[] payload = new byte[messageSize];
                    Buffer.BlockCopy(allBytes, sizeof(int), payload, 0, messageSize);

                    int remaining = allBytes.Length - sizeof(int) - messageSize;
                    m_MessageBuffer.SetLength(0);
                    if (remaining > 0)
                    {
                        m_MessageBuffer.Write(allBytes, sizeof(int) + messageSize, remaining);
                    }

                    DeliverMessage(payload, messageSize);
                }
            }

            private void DeliverMessage(byte[] payload, int size)
            {
                TransportConnection connection = new TransportConnection
                {
                    ConnectionId = ConnectionId,
                    Address = Address
                };

                if (m_Owner.IsHost && ConnectionId != 0)
                {
                    m_Owner.HandleDataReceived(connection, payload, size);
                    return;
                }

                if (m_Owner.IsClient && ConnectionId == 0)
                {
                    if (TryHandleHandshake(payload, size))
                    {
                        return;
                    }

                    connection.ConnectionId = NetworkManager.LocalConnectionId;
                    m_Owner.HandleDataReceived(connection, payload, size);
                }
            }

            private bool TryHandleHandshake(byte[] payload, int size)
            {
                if (size < 1 || payload[0] != LocalSessionProtocol.HandshakeByte)
                {
                    return false;
                }

                if (size < 9)
                {
                    return false;
                }

                ulong assignedId = BitConverter.ToUInt64(payload, 1);
                m_Owner.HandleClientConnectedToHost(assignedId);
                return true;
            }
        }

        private void SendHandshake(ClientConnection connection)
        {
            byte[] payload = new byte[9];
            payload[0] = LocalSessionProtocol.HandshakeByte;
            Buffer.BlockCopy(BitConverter.GetBytes(connection.ConnectionId), 0, payload, 1, sizeof(ulong));
            connection.Send(payload, payload.Length);
        }
    }

    internal static class LocalSessionProtocol
    {
        public const byte HandshakeByte = 0xF1;
    }
}
