using System;
using FrizzNet.Logging;

namespace FrizzNet.Transport
{
    /// <summary>
    /// Lightweight session API for local TCP testing without Steam lobbies.
    /// Mirrors the basic flow of FrizzLobby for sample compatibility.
    /// </summary>
    public static class FrizzLocalSession
    {
        public static bool InSession =>
            LocalTransport.Instance != null &&
            (LocalTransport.Instance.IsHost || LocalTransport.Instance.IsClient);

        public static bool IsHost => LocalTransport.Instance != null && LocalTransport.Instance.IsHost;
        public static bool IsClient => LocalTransport.Instance != null && LocalTransport.Instance.IsClient;
        public static int Port => LocalTransport.Instance != null ? LocalTransport.Instance.ActivePort : 0;

        public static event Action OnSessionStarted;
        public static event Action OnSessionEnded;
        public static event Action<ulong> OnClientJoined;
        public static event Action<ulong> OnClientLeft;

        /// <summary>
        /// Starts a local host session on the configured port.
        /// </summary>
        public static bool Host(int port = 7777, int maxPlayers = 8)
        {
            LocalTransport transport = GetOrFindTransport();
            if (transport == null)
            {
                FrizzLogger.LogError("[FrizzLocalSession] No LocalTransport found on NetworkManager.");
                return false;
            }

            if (transport.StartHost("127.0.0.1", port, maxPlayers))
            {
                OnSessionStarted?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Connects to a local host. Address may be "7777", "127.0.0.1:7777", or "localhost:7777".
        /// </summary>
        public static bool Join(string address = "127.0.0.1:7777")
        {
            LocalTransport transport = GetOrFindTransport();
            if (transport == null)
            {
                FrizzLogger.LogError("[FrizzLocalSession] No LocalTransport found on NetworkManager.");
                return false;
            }

            if (transport.StartClient(address))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Disconnects and stops the local session.
        /// </summary>
        public static void Leave()
        {
            LocalTransport transport = LocalTransport.Instance;
            if (transport == null) return;

            transport.Disconnect();
            transport.StopHost();
            OnSessionEnded?.Invoke();
        }

        /// <summary>
        /// Returns the host connection ID (always 1 for local sessions).
        /// </summary>
        public static ulong GetHostConnectionId()
        {
            return LocalTransport.HostConnectionId;
        }

        internal static void NotifyClientJoined(ulong clientId)
        {
            OnClientJoined?.Invoke(clientId);
        }

        internal static void NotifyClientLeft(ulong clientId)
        {
            OnClientLeft?.Invoke(clientId);
        }

        internal static void NotifyClientSessionStarted()
        {
            OnSessionStarted?.Invoke();
        }

        private static LocalTransport GetOrFindTransport()
        {
            if (LocalTransport.Instance != null)
            {
                return LocalTransport.Instance;
            }

            LocalTransport transport = UnityEngine.Object.FindAnyObjectByType<LocalTransport>();
            return transport;
        }
    }
}
