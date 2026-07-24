using System;
using System.Collections.Generic;
using UnityEngine;
using FrizzNet.Messaging;
using FrizzNet.Logging;

namespace FrizzNet.Core
{
    /// <summary>
    /// Captures and restores networked object state during Steam lobby host migration.
    /// Attach to the same GameObject as NetworkManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class FrizzHostMigration : MonoBehaviour
    {
        public static FrizzHostMigration Instance { get; private set; }

        [Header("Migration Settings")]
        [Tooltip("If true, automatically registers snapshot handlers and listens for lobby owner changes.")]
        [SerializeField] private bool m_AutoHandleMigration = true;

        public static event Action OnMigrationStarted;
        public static event Action OnMigrationCompleted;

        private byte[] m_PendingSnapshot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (!m_AutoHandleMigration) return;

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RegisterHandler(FrizzSystemMessages.HostSnapshot, HandleSnapshotReceived);
            }

            FrizzNet.Steam.FrizzLobby.OnLobbyOwnerChangedEvent += HandleLobbyOwnerChanged;
        }

        private void OnDestroy()
        {
            FrizzNet.Steam.FrizzLobby.OnLobbyOwnerChangedEvent -= HandleLobbyOwnerChanged;

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.UnregisterHandler(FrizzSystemMessages.HostSnapshot);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Serializes all spawned network objects into a byte buffer.
        /// </summary>
        public byte[] CaptureSnapshot()
        {
            if (NetworkManager.Instance == null) return null;

            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteLong((long)NetworkManager.Instance.NextNetworkIdSeed);
                writer.WriteInt(NetworkManager.Instance.NetworkObjects.Count);

                foreach (KeyValuePair<ulong, NetworkIdentity> pair in NetworkManager.Instance.NetworkObjects)
                {
                    NetworkIdentity identity = pair.Value;
                    if (identity == null) continue;

                    writer.WriteLong((long)pair.Key);
                    writer.WriteString(identity.PrefabAssetName);
                    writer.WriteFloat(identity.transform.position.x);
                    writer.WriteFloat(identity.transform.position.y);
                    writer.WriteFloat(identity.transform.position.z);
                    writer.WriteFloat(identity.transform.rotation.x);
                    writer.WriteFloat(identity.transform.rotation.y);
                    writer.WriteFloat(identity.transform.rotation.z);
                    writer.WriteFloat(identity.transform.rotation.w);
                    writer.WriteFloat(identity.transform.localScale.x);
                    writer.WriteFloat(identity.transform.localScale.y);
                    writer.WriteFloat(identity.transform.localScale.z);
                    writer.WriteLong((long)identity.OwnerConnectionId);
                }

                return writer.ToArray();
            }
        }

        /// <summary>
        /// Broadcasts a snapshot to all connected clients before host migration.
        /// </summary>
        public void BroadcastSnapshot()
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost) return;

            byte[] snapshot = CaptureSnapshot();
            if (snapshot == null) return;

            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteInt(snapshot.Length);
                writer.WriteRawBytes(snapshot);
                NetworkManager.Instance.SendToAll(FrizzSystemMessages.HostSnapshot, writer, true);
            }

            OnMigrationStarted?.Invoke();
            FrizzLogger.LogNetwork("[HostMigration] Snapshot broadcast to all clients.");
        }

        /// <summary>
        /// Applies a snapshot on the new host, respawning all tracked objects.
        /// </summary>
        public void ApplySnapshot(byte[] snapshotData)
        {
            if (NetworkManager.Instance == null || snapshotData == null || snapshotData.Length == 0) return;

            using (MessageReader reader = new MessageReader(snapshotData))
            {
                ulong nextId = (ulong)reader.ReadLong();
                int count = reader.ReadInt();

                NetworkManager.Instance.PrepareForHostMigration(nextId);

                for (int i = 0; i < count; i++)
                {
                    ulong networkId = (ulong)reader.ReadLong();
                    string prefabName = reader.ReadString();
                    Vector3 pos = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                    Quaternion rot = new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                    Vector3 scale = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                    ulong ownerId = (ulong)reader.ReadLong();

                    NetworkManager.Instance.SpawnFromSnapshot(networkId, prefabName, pos, rot, scale, ownerId);
                }
            }

            OnMigrationCompleted?.Invoke();
            FrizzLogger.LogNetwork("[HostMigration] Snapshot applied on new host.");
        }

        private void HandleLobbyOwnerChanged(Steamworks.CSteamID lobbyId, Steamworks.CSteamID oldOwner, Steamworks.CSteamID newOwner)
        {
            if (NetworkManager.Instance == null) return;

            ulong localId = NetworkManager.LocalConnectionId;
            if (oldOwner.m_SteamID == localId && NetworkManager.Instance.IsHost)
            {
                BroadcastSnapshot();
            }

            if (newOwner.m_SteamID == localId && m_PendingSnapshot != null)
            {
                ApplySnapshot(m_PendingSnapshot);
                m_PendingSnapshot = null;
            }
        }

        private void HandleSnapshotReceived(ulong connectionId, MessageReader reader)
        {
            int length = reader.ReadInt();
            byte[] data = reader.ReadRawBytes(length);
            m_PendingSnapshot = data;

            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                ApplySnapshot(m_PendingSnapshot);
                m_PendingSnapshot = null;
            }
        }
    }
}
