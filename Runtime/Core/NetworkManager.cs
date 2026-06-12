using System;
using System.Collections.Generic;
using UnityEngine;
using FrizzNet.Transport;
using FrizzNet.Messaging;
using FrizzNet.Logging;
using FrizzNet.Steam;
using Steamworks;

namespace FrizzNet.Core
{
    /// <summary>
    /// Core NetworkManager that coordinates transport layers, player connections, 
    /// message routing, and dynamic object spawning.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SteamTransport))]
    [FrizzHelp("The core manager coordinating player connection rosters, custom packet handlers routing, and network object spawn synchronization.", "index.html#NetworkManager")]
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("The transport implementation to use. Will automatically search on this GameObject if not set.")]
        [SerializeField] private MonoBehaviour m_TransportComponent;

        [Tooltip("List of prefabs that can be dynamically spawned over the network.")]
        [SerializeField] private List<NetworkIdentity> m_SpawnablePrefabs = new List<NetworkIdentity>();

        [Header("Network Options")]
        [Tooltip("If true, ensures the GameObject is not destroyed when loading a new Scene.")]
        [SerializeField] private bool m_DontDestroyOnLoad = true;

        [Tooltip("If true, keeps the application running when window loses focus (crucial for local testing).")]
        [SerializeField] private bool m_RunInBackground = true;

        [Tooltip("Filters framework log output severity printed to the Unity console.")]
        [SerializeField] private FrizzLogLevel m_LogLevel = FrizzLogLevel.Info;

        // System Reserved Messages (negative values to avoid developer overlap)
        private const short MSG_SPAWN = -10;
        private const short MSG_DESTROY = -11;
        private const short MSG_TRANSFORM = -12;
        private const short MSG_VOICE = -13;
        private const short MSG_ANIMATION = -14;

        private INetworkTransport m_Transport;
        private readonly Dictionary<short, Action<ulong, MessageReader>> m_MessageHandlers = new Dictionary<short, Action<ulong, MessageReader>>();
        
        // Networked Objects Tracking
        private readonly Dictionary<ulong, NetworkIdentity> m_NetworkObjects = new Dictionary<ulong, NetworkIdentity>();
        private readonly Dictionary<string, NetworkIdentity> m_PrefabRegistry = new Dictionary<string, NetworkIdentity>();
        private ulong m_NextNetworkId = 1;

        // Player Tracking
        private readonly HashSet<ulong> m_ConnectedClients = new HashSet<ulong>();

        // Properties
        public INetworkTransport Transport => m_Transport;
        public bool IsHost => m_Transport != null && m_Transport.IsHost;
        public bool IsClient => m_Transport != null && m_Transport.IsClient;
        public IReadOnlyCollection<ulong> ConnectedClients => m_ConnectedClients;
        public IReadOnlyDictionary<ulong, NetworkIdentity> NetworkObjects => m_NetworkObjects;

        // Network Options properties
        public bool DontDestroyOnLoadOnAwake { get => m_DontDestroyOnLoad; set => m_DontDestroyOnLoad = value; }
        public bool RunInBackground { get => m_RunInBackground; set { m_RunInBackground = value; Application.runInBackground = value; } }
        public FrizzLogLevel LogLevel { get => m_LogLevel; set { m_LogLevel = value; FrizzLogger.CurrentLogLevel = value; } }

        // Public Developer Events
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action<ulong> OnClientConnected;
        public static event Action<ulong> OnClientDisconnected;
        public static event Action<CSteamID> OnLobbyCreated;
        public static event Action<CSteamID> OnLobbyJoined;
        public static event Action OnLobbyLeft;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            if (m_DontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (m_RunInBackground)
            {
                Application.runInBackground = true;
            }

            FrizzLogger.CurrentLogLevel = m_LogLevel;

            InitializeTransport();
            BuildPrefabRegistry();
            RegisterSystemHandlers();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (m_TransportComponent == null)
            {
                m_TransportComponent = GetComponent<SteamTransport>();
            }
        }
#endif

        private void Start()
        {
            // Subscribe to Steam Lobby events to mirror them to static NetworkManager events
            FrizzLobby.OnLobbyCreatedEvent += TriggerOnLobbyCreated;
            FrizzLobby.OnLobbyJoinedEvent += TriggerOnLobbyJoined;
            FrizzLobby.OnLobbyLeftEvent += TriggerOnLobbyLeft;
        }

        private void OnDestroy()
        {
            FrizzLobby.OnLobbyCreatedEvent -= TriggerOnLobbyCreated;
            FrizzLobby.OnLobbyJoinedEvent -= TriggerOnLobbyJoined;
            FrizzLobby.OnLobbyLeftEvent -= TriggerOnLobbyLeft;

            if (m_Transport != null)
            {
                m_Transport.OnClientConnected -= HandleClientConnected;
                m_Transport.OnClientDisconnected -= HandleClientDisconnected;
                m_Transport.OnDataReceived -= HandleDataReceived;
                m_Transport.OnConnectedToServer -= HandleConnectedToServer;
                m_Transport.OnDisconnectedFromServer -= HandleDisconnectedFromServer;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void InitializeTransport()
        {
            if (m_TransportComponent == null)
            {
                m_TransportComponent = GetComponent<INetworkTransport>() as MonoBehaviour;
            }

            if (m_TransportComponent is INetworkTransport transport)
            {
                m_Transport = transport;
                m_Transport.OnClientConnected += HandleClientConnected;
                m_Transport.OnClientDisconnected += HandleClientDisconnected;
                m_Transport.OnDataReceived += HandleDataReceived;
                m_Transport.OnConnectedToServer += HandleConnectedToServer;
                m_Transport.OnDisconnectedFromServer += HandleDisconnectedFromServer;

                FrizzLogger.LogInfo($"Transport initialized successfully: {m_Transport.GetType().Name}");
            }
            else
            {
                FrizzLogger.LogError("No component implementing INetworkTransport was assigned or found on the NetworkManager GameObject.");
            }
        }

        private void BuildPrefabRegistry()
        {
            m_PrefabRegistry.Clear();
            foreach (var prefab in m_SpawnablePrefabs)
            {
                if (prefab == null) continue;
                string prefabName = prefab.gameObject.name;
                if (m_PrefabRegistry.ContainsKey(prefabName))
                {
                    FrizzLogger.LogWarning($"Duplicate prefab name in spawnable list: {prefabName}. Only the first will be registered.");
                    continue;
                }
                m_PrefabRegistry.Add(prefabName, prefab);
            }
            FrizzLogger.LogInfo($"Registered {m_PrefabRegistry.Count} spawnable prefabs.");
        }

        /// <summary>
        /// Dynamically registers a prefab at runtime to be spawnable across the network.
        /// </summary>
        public void RegisterSpawnablePrefab(NetworkIdentity prefab)
        {
            if (prefab == null) return;
            string prefabName = prefab.gameObject.name;
            if (!m_PrefabRegistry.ContainsKey(prefabName))
            {
                m_PrefabRegistry.Add(prefabName, prefab);
                FrizzLogger.LogInfo($"Dynamically registered spawnable prefab: {prefabName}");
            }
        }

        #region Message Handlers Registration

        /// <summary>
        /// Registers a handler for a specific message ID.
        /// </summary>
        public void RegisterHandler(short messageId, Action<ulong, MessageReader> handler)
        {
            if (m_MessageHandlers.ContainsKey(messageId))
            {
                FrizzLogger.LogWarning($"Overwriting message handler for ID: {messageId}");
                m_MessageHandlers[messageId] = handler;
            }
            else
            {
                m_MessageHandlers.Add(messageId, handler);
            }
        }

        /// <summary>
        /// Unregisters a handler for a specific message ID.
        /// </summary>
        public void UnregisterHandler(short messageId)
        {
            m_MessageHandlers.Remove(messageId);
        }

        private void RegisterSystemHandlers()
        {
            RegisterHandler(MSG_SPAWN, HandleSystemSpawn);
            RegisterHandler(MSG_DESTROY, HandleSystemDestroy);
            RegisterHandler(MSG_TRANSFORM, HandleSystemTransform);
            RegisterHandler(MSG_VOICE, HandleSystemVoice);
            RegisterHandler(MSG_ANIMATION, HandleSystemAnimation);
        }

        #endregion

        #region Messaging Functions

        /// <summary>
        /// Sends a message from Client to Server.
        /// </summary>
        public void SendToServer(short messageId, MessageWriter writer, bool reliable = true)
        {
            if (m_Transport == null || !m_Transport.IsClient) return;

            byte[] payload = writer.ToArray();
            using (MessageWriter systemWriter = new MessageWriter())
            {
                systemWriter.WriteShort(messageId);
                systemWriter.WriteRawBytes(payload);
                byte[] data = systemWriter.ToArray();
                m_Transport.SendToServer(data, data.Length, reliable);
            }
        }

        /// <summary>
        /// Sends a message from Host to a specific Client.
        /// </summary>
        public void SendToClient(ulong connectionId, short messageId, MessageWriter writer, bool reliable = true)
        {
            if (m_Transport == null || !m_Transport.IsHost) return;

            byte[] payload = writer.ToArray();
            using (MessageWriter systemWriter = new MessageWriter())
            {
                systemWriter.WriteShort(messageId);
                systemWriter.WriteRawBytes(payload);
                byte[] data = systemWriter.ToArray();
                m_Transport.SendToClient(connectionId, data, data.Length, reliable);
            }
        }

        /// <summary>
        /// Sends a message from Host to all connected Clients.
        /// </summary>
        public void SendToAll(short messageId, MessageWriter writer, bool reliable = true)
        {
            if (m_Transport == null || !m_Transport.IsHost) return;

            byte[] payload = writer.ToArray();
            using (MessageWriter systemWriter = new MessageWriter())
            {
                systemWriter.WriteShort(messageId);
                systemWriter.WriteRawBytes(payload);
                byte[] data = systemWriter.ToArray();

                foreach (var clientId in m_ConnectedClients)
                {
                    m_Transport.SendToClient(clientId, data, data.Length, reliable);
                }
            }
        }

        #endregion

        #region Object Spawning

        /// <summary>
        /// Spawns a registered prefab across the network. Only valid on the Host.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, ulong ownerId = 0)
        {
            if (!IsHost)
            {
                FrizzLogger.LogError("Only the host can spawn networked objects.");
                return null;
            }

            if (prefab == null) return null;

            string prefabName = prefab.name;
            if (!m_PrefabRegistry.ContainsKey(prefabName))
            {
                FrizzLogger.LogError($"Prefab '{prefabName}' is not registered on the NetworkManager spawnable prefabs list.");
                return null;
            }

            ulong networkId = m_NextNetworkId++;
            GameObject obj = Instantiate(prefab, position, rotation);
            obj.SetActive(true); // Ensure the cloned instance is activated
            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();

            if (identity == null)
            {
                FrizzLogger.LogWarning($"Spawning GameObject '{prefabName}' without a NetworkIdentity. Adding one dynamically.");
                identity = obj.AddComponent<NetworkIdentity>();
            }

            identity.NetworkId = networkId;
            identity.OwnerConnectionId = ownerId;
            
            // Host is owner?
            bool hasAuthority = (ownerId == 0 || ownerId == SteamUser.GetSteamID().m_SteamID);
            identity.SetAuthority(hasAuthority, ownerId == SteamUser.GetSteamID().m_SteamID);

            m_NetworkObjects.Add(networkId, identity);

            // Notify all clients
            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteLong((long)networkId);
                writer.WriteString(prefabName);
                writer.WriteFloat(position.x);
                writer.WriteFloat(position.y);
                writer.WriteFloat(position.z);
                writer.WriteFloat(rotation.x);
                writer.WriteFloat(rotation.y);
                writer.WriteFloat(rotation.z);
                writer.WriteFloat(rotation.w);
                writer.WriteLong((long)ownerId);

                SendToAll(MSG_SPAWN, writer, true);
            }

            FrizzLogger.LogNetwork($"Host spawned networked object '{prefabName}' with NetworkID {networkId}");
            return obj;
        }

        /// <summary>
        /// Despawns a networked object and destroys it on all clients. Only valid on the Host.
        /// </summary>
        public void Despawn(GameObject obj)
        {
            if (!IsHost)
            {
                FrizzLogger.LogError("Only the host can despawn networked objects.");
                return;
            }

            if (obj == null) return;
            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
            if (identity == null || identity.NetworkId == 0) return;

            ulong networkId = identity.NetworkId;

            // Notify all clients
            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteLong((long)networkId);
                SendToAll(MSG_DESTROY, writer, true);
            }

            m_NetworkObjects.Remove(networkId);
            Destroy(obj);

            FrizzLogger.LogNetwork($"Host despawned networked object with NetworkID {networkId}");
        }

        #endregion

        #region Transport Event Handlers

        private void HandleClientConnected(TransportConnection conn)
        {
            FrizzLogger.LogNetwork($"Server event: Client {conn.ConnectionId} connected.");
            m_ConnectedClients.Add(conn.ConnectionId);

            // Replicate existing spawned objects to the new client
            foreach (var pair in m_NetworkObjects)
            {
                ulong netId = pair.Key;
                NetworkIdentity identity = pair.Value;
                Vector3 pos = identity.transform.position;
                Quaternion rot = identity.transform.rotation;

                using (MessageWriter writer = new MessageWriter())
                {
                    writer.WriteLong((long)netId);
                    writer.WriteString(identity.gameObject.name.Replace("(Clone)", "").Trim());
                    writer.WriteFloat(pos.x);
                    writer.WriteFloat(pos.y);
                    writer.WriteFloat(pos.z);
                    writer.WriteFloat(rot.x);
                    writer.WriteFloat(rot.y);
                    writer.WriteFloat(rot.z);
                    writer.WriteFloat(rot.w);
                    writer.WriteLong((long)identity.OwnerConnectionId);

                    SendToClient(conn.ConnectionId, MSG_SPAWN, writer, true);
                }
            }

            OnClientConnected?.Invoke(conn.ConnectionId);
        }

        private void HandleClientDisconnected(TransportConnection conn)
        {
            FrizzLogger.LogNetwork($"Server event: Client {conn.ConnectionId} disconnected.");
            m_ConnectedClients.Remove(conn.ConnectionId);

            // Clean up any network objects owned by this disconnected client
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (var pair in m_NetworkObjects)
            {
                if (pair.Value.OwnerConnectionId == conn.ConnectionId)
                {
                    toDestroy.Add(pair.Value.gameObject);
                }
            }

            foreach (var obj in toDestroy)
            {
                Despawn(obj);
            }

            OnClientDisconnected?.Invoke(conn.ConnectionId);
        }

        private void HandleConnectedToServer()
        {
            FrizzLogger.LogNetwork("Client event: Connected to server.");
            OnConnected?.Invoke();
        }

        private void HandleDisconnectedFromServer()
        {
            FrizzLogger.LogNetwork("Client event: Disconnected from server.");
            
            // Clean up local spawned objects
            foreach (var pair in m_NetworkObjects)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }
            m_NetworkObjects.Clear();
            m_ConnectedClients.Clear();

            OnDisconnected?.Invoke();
        }

        private void HandleDataReceived(TransportConnection conn, byte[] data, int size)
        {
            using (MessageReader reader = new MessageReader(data, 0, size))
            {
                if (reader.RemainingBytes < 2)
                {
                    FrizzLogger.LogWarning("Malformed package received: smaller than 2 bytes (no Message ID).");
                    return;
                }

                short messageId = reader.ReadShort();

                if (m_MessageHandlers.TryGetValue(messageId, out var handler))
                {
                    handler?.Invoke(conn.ConnectionId, reader);
                }
                else
                {
                    FrizzLogger.LogWarning($"No handler registered for message ID: {messageId}");
                }
            }
        }

        #endregion

        #region System Reserved Message Handlers

        private void HandleSystemSpawn(ulong connectionId, MessageReader reader)
        {
            if (IsHost) return; // Host already generated the spawn locally

            ulong networkId = (ulong)reader.ReadLong();
            string prefabName = reader.ReadString();
            Vector3 pos = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            Quaternion rot = new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            ulong ownerId = (ulong)reader.ReadLong();

            if (!m_PrefabRegistry.TryGetValue(prefabName, out NetworkIdentity prefab))
            {
                FrizzLogger.LogError($"Client failed to spawn: Prefab '{prefabName}' not registered.");
                return;
            }

            GameObject obj = Instantiate(prefab.gameObject, pos, rot);
            obj.SetActive(true); // Ensure the cloned instance is activated
            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                identity = obj.AddComponent<NetworkIdentity>();
            }

            identity.NetworkId = networkId;
            identity.OwnerConnectionId = ownerId;
            
            bool isLocalOwner = (ownerId == SteamUser.GetSteamID().m_SteamID);
            identity.SetAuthority(isLocalOwner, isLocalOwner);

            m_NetworkObjects.Add(networkId, identity);

            FrizzLogger.LogNetwork($"Client spawned networked object '{prefabName}' with NetworkID {networkId}");
        }

        private void HandleSystemDestroy(ulong connectionId, MessageReader reader)
        {
            if (IsHost) return; // Host already handle destruction

            ulong networkId = (ulong)reader.ReadLong();

            if (m_NetworkObjects.TryGetValue(networkId, out NetworkIdentity identity))
            {
                m_NetworkObjects.Remove(networkId);
                if (identity != null)
                {
                    Destroy(identity.gameObject);
                }
                FrizzLogger.LogNetwork($"Client destroyed networked object with NetworkID {networkId}");
            }
        }

        private void HandleSystemTransform(ulong connectionId, MessageReader reader)
        {
            ulong networkId = (ulong)reader.ReadLong();
            Vector3 pos = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            Quaternion rot = new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());

            if (m_NetworkObjects.TryGetValue(networkId, out NetworkIdentity identity))
            {
                // Don't apply updates if the local client has authority over the object
                if (identity.HasAuthority) return;

                FrizzNetworkTransform netTransform = identity.GetComponent<FrizzNetworkTransform>();
                if (netTransform != null)
                {
                    netTransform.OnReceiveUpdate(pos, rot);
                }
            }

            // Host replicates the transform packet to all other clients unreliably
            if (IsHost)
            {
                using (MessageWriter writer = new MessageWriter())
                {
                    writer.WriteLong((long)networkId);
                    writer.WriteFloat(pos.x);
                    writer.WriteFloat(pos.y);
                    writer.WriteFloat(pos.z);
                    writer.WriteFloat(rot.x);
                    writer.WriteFloat(rot.y);
                    writer.WriteFloat(rot.z);
                    writer.WriteFloat(rot.w);

                    byte[] payload = writer.ToArray();
                    using (MessageWriter systemWriter = new MessageWriter())
                    {
                        systemWriter.WriteShort(MSG_TRANSFORM);
                        systemWriter.WriteRawBytes(payload);
                        byte[] data = systemWriter.ToArray();

                        foreach (var clientId in m_ConnectedClients)
                        {
                            if (clientId != connectionId)
                            {
                                m_Transport.SendToClient(clientId, data, data.Length, false); // Unreliable for transform replication
                            }
                        }
                    }
                }
            }
        }

        private void HandleSystemVoice(ulong connectionId, MessageReader reader)
        {
            ulong senderId = (ulong)reader.ReadLong();
            int size = reader.ReadInt();
            byte[] compressedData = reader.ReadRawBytes(size);

            // Host replicates the voice packet to all other clients unreliably
            if (IsHost)
            {
                using (MessageWriter writer = new MessageWriter())
                {
                    writer.WriteLong((long)senderId);
                    writer.WriteInt(size);
                    writer.WriteRawBytes(compressedData);

                    byte[] payload = writer.ToArray();
                    using (MessageWriter systemWriter = new MessageWriter())
                    {
                        systemWriter.WriteShort(MSG_VOICE);
                        systemWriter.WriteRawBytes(payload);
                        byte[] data = systemWriter.ToArray();

                        foreach (var clientId in m_ConnectedClients)
                        {
                            if (clientId != connectionId)
                            {
                                m_Transport.SendToClient(clientId, data, data.Length, false); // Unreliable for voice
                            }
                        }
                    }
                }
            }

            // Distribute to local speaker manager if it's not the local client
            if (senderId != SteamUser.GetSteamID().m_SteamID)
            {
                FrizzVoiceManager.Instance?.ReceiveVoiceData(senderId, compressedData);
            }
        }

        private void HandleSystemAnimation(ulong connectionId, MessageReader reader)
        {
            ulong networkId = (ulong)reader.ReadLong();
            int payloadSize = reader.ReadInt();
            byte[] animationPayload = reader.ReadRawBytes(payloadSize);

            // Replicate this animation update to other clients if we are the Host
            if (IsHost)
            {
                using (MessageWriter writer = new MessageWriter())
                {
                    writer.WriteLong((long)networkId);
                    writer.WriteInt(payloadSize);
                    writer.WriteRawBytes(animationPayload);

                    byte[] payload = writer.ToArray();
                    using (MessageWriter systemWriter = new MessageWriter())
                    {
                        systemWriter.WriteShort(MSG_ANIMATION);
                        systemWriter.WriteRawBytes(payload);
                        byte[] data = systemWriter.ToArray();

                        foreach (var clientId in m_ConnectedClients)
                        {
                            if (clientId != connectionId)
                            {
                                m_Transport.SendToClient(clientId, data, data.Length, true); // Reliable for animator updates
                            }
                        }
                    }
                }
            }

            // Find local object and forward update
            if (m_NetworkObjects.TryGetValue(networkId, out NetworkIdentity identity))
            {
                if (identity != null)
                {
                    // Owners do not apply network animation updates to avoid local prediction conflicts
                    if (!IsHost && identity.HasAuthority) return;

                    FrizzNetworkAnimator netAnimator = identity.GetComponent<FrizzNetworkAnimator>();
                    if (netAnimator != null)
                    {
                        netAnimator.OnReceiveUpdate(animationPayload);
                    }
                }
            }
        }

        #endregion

        #region Static Mirror Event Triggers

        private void TriggerOnLobbyCreated(CSteamID lobbyId)
        {
            OnLobbyCreated?.Invoke(lobbyId);
        }

        private void TriggerOnLobbyJoined(CSteamID lobbyId)
        {
            OnLobbyJoined?.Invoke(lobbyId);
        }

        private void TriggerOnLobbyLeft()
        {
            ResetLobbyState();
            OnLobbyLeft?.Invoke();
        }

        private void ResetLobbyState()
        {
            FrizzLogger.LogNetwork("Resetting NetworkManager lobby state. Destroying all network objects...");
            foreach (var pair in m_NetworkObjects)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }
            m_NetworkObjects.Clear();
            m_ConnectedClients.Clear();
            m_NextNetworkId = 1;
        }

        #endregion
    }
}
