using System;

namespace FrizzNet.Transport
{
    /// <summary>
    /// Type of network event triggered by the transport layer.
    /// </summary>
    public enum TransportEvent
    {
        ClientConnected,
        ClientDisconnected,
        DataReceived
    }

    /// <summary>
    /// Represents a connection handled by the transport.
    /// </summary>
    public struct TransportConnection
    {
        /// <summary>
        /// Unique identifier for this connection (e.g. SteamID or numerical handle).
        /// </summary>
        public ulong ConnectionId;

        /// <summary>
        /// Display address or remote peer descriptor (e.g. SteamID string).
        /// </summary>
        public string Address;

        public override string ToString()
        {
            return $"ConnId: {ConnectionId} ({Address})";
        }
    }

    /// <summary>
    /// Base interface for all network transport implementations in FrizzNet.
    /// </summary>
    public interface INetworkTransport
    {
        /// <summary>
        /// Event fired on the server when a client connects.
        /// </summary>
        event Action<TransportConnection> OnClientConnected;

        /// <summary>
        /// Event fired on the server when a client disconnects.
        /// </summary>
        event Action<TransportConnection> OnClientDisconnected;

        /// <summary>
        /// Event fired on both client and server when data is received.
        /// Passes Connection, data byte array, and actual byte count.
        /// </summary>
        event Action<TransportConnection, byte[], int> OnDataReceived;

        /// <summary>
        /// Event fired on the client when successfully connected to the host.
        /// </summary>
        event Action OnConnectedToServer;

        /// <summary>
        /// Event fired on the client when disconnected from the host.
        /// </summary>
        event Action OnDisconnectedFromServer;

        /// <summary>
        /// Returns true if this transport is acting as a Host (Server).
        /// </summary>
        bool IsHost { get; }

        /// <summary>
        /// Returns true if this transport is active and connected as a Client.
        /// </summary>
        bool IsClient { get; }

        /// <summary>
        /// Starts hosting a server.
        /// </summary>
        /// <param name="maxPlayers">Maximum allowed connections / lobby size.</param>
        /// <returns>True if hosting started successfully, false otherwise.</returns>
        bool StartHost(int maxPlayers);

        /// <summary>
        /// Stops hosting and disconnects all clients.
        /// </summary>
        void StopHost();

        /// <summary>
        /// Connects to a host server.
        /// </summary>
        /// <param name="hostAddress">The connection string (e.g. Host SteamID).</param>
        /// <returns>True if connection attempt was initiated successfully.</returns>
        bool StartClient(string hostAddress);

        /// <summary>
        /// Disconnects from the host server.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Sends a packet to the server (Client only).
        /// </summary>
        /// <param name="data">Buffer containing the data.</param>
        /// <param name="size">Number of bytes to send.</param>
        /// <param name="reliable">Whether delivery is guaranteed.</param>
        /// <returns>True if the send call succeeded.</returns>
        bool SendToServer(byte[] data, int size, bool reliable = true);

        /// <summary>
        /// Sends a packet to a specific client connection (Host/Server only).
        /// </summary>
        /// <param name="connectionId">Target client connection ID.</param>
        /// <param name="data">Buffer containing the data.</param>
        /// <param name="size">Number of bytes to send.</param>
        /// <param name="reliable">Whether delivery is guaranteed.</param>
        /// <returns>True if the send call succeeded.</returns>
        bool SendToClient(ulong connectionId, byte[] data, int size, bool reliable = true);

        /// <summary>
        /// Authoritatively disconnects a specific client connection (Host/Server only).
        /// </summary>
        /// <param name="connectionId">Target client connection ID.</param>
        /// <returns>True if the client was found and disconnected successfully.</returns>
        bool DisconnectClient(ulong connectionId);

        /// <summary>
        /// Manually polls transport-specific events, callbacks, and incoming messages.
        /// Should be called on update/tick loops.
        /// </summary>
        void PollEvents();
    }
}
