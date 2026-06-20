using UnityEngine;

using System.Collections.Generic;
using FrizzNet.Messaging;

namespace FrizzNet.Core
{
    /// <summary>
    /// Base class for scripts that need network synchronization, authority checks, and lifecycle callbacks.
    /// Exposes helper properties like HasAuthority and IsLocalPlayer directly.
    /// </summary>
    [FrizzHelp("Base class for components requiring network synchronization, authority check properties, and virtual lifecycle hooks.", "index.html#NetworkBehaviour")]
    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        private readonly Dictionary<short, ISyncVarBinding> m_SyncVarBindings = new Dictionary<short, ISyncVarBinding>();

        private interface ISyncVarBinding
        {
            void ApplyFromReader(MessageReader reader);
        }

        private sealed class SyncVarBinding<T> : ISyncVarBinding
        {
            private readonly FrizzNetworkVariable<T> m_Variable;

            public SyncVarBinding(FrizzNetworkVariable<T> variable)
            {
                m_Variable = variable;
            }

            public void ApplyFromReader(MessageReader reader)
            {
                if (m_Variable.TryReadValue(reader, out T value))
                {
                    m_Variable.ApplyFromNetwork(value);
                }
            }
        }

        /// <summary>
        /// The NetworkIdentity component attached to the same GameObject.
        /// </summary>
        public NetworkIdentity NetworkIdentity { get; internal set; }

        /// <summary>
        /// The unique network identifier assigned to this object by the NetworkManager.
        /// </summary>
        public ulong NetworkId => NetworkIdentity != null ? NetworkIdentity.NetworkId : 0;

        /// <summary>
        /// Returns true if this client has network authority (control) over this object.
        /// </summary>
        public bool HasAuthority => NetworkIdentity != null && NetworkIdentity.HasAuthority;

        /// <summary>
        /// Returns true if this GameObject represents the local player.
        /// </summary>
        public bool IsLocalPlayer => NetworkIdentity != null && NetworkIdentity.IsLocalPlayer;

        /// <summary>
        /// Returns true if the local instance is hosting (acting as a server).
        /// </summary>
        public bool IsServer => NetworkManager.Instance != null && NetworkManager.Instance.IsHost;

        /// <summary>
        /// Returns true if the local instance is a client connected to a host.
        /// </summary>
        public bool IsClient => NetworkManager.Instance != null && NetworkManager.Instance.IsClient;

        /// <summary>
        /// Called when the GameObject is spawned and registered on the network.
        /// </summary>
        public virtual void OnNetworkSpawn() {}

        /// <summary>
        /// Called when the GameObject is despawned or destroyed.
        /// </summary>
        public virtual void OnNetworkDespawn() {}

        /// <summary>
        /// Called when the local client is granted network authority over this object.
        /// </summary>
        public virtual void OnStartAuthority() {}

        /// <summary>
        /// Called when the local client loses network authority over this object.
        /// </summary>
        public virtual void OnStopAuthority() {}

        /// <summary>
        /// Called on the local player's machine when their player avatar object spawns.
        /// </summary>
        public virtual void OnStartLocalPlayer() {}

        /// <summary>
        /// Registers a FrizzNetworkVariable with a unique sync ID for automatic replication.
        /// </summary>
        protected void RegisterSyncVar<T>(short syncId, FrizzNetworkVariable<T> variable)
        {
            variable.Initialize(this, syncId);
            m_SyncVarBindings[syncId] = new SyncVarBinding<T>(variable);
        }

        internal bool TryApplySyncVar(short syncId, MessageReader reader)
        {
            if (m_SyncVarBindings.TryGetValue(syncId, out ISyncVarBinding binding))
            {
                binding.ApplyFromReader(reader);
                return true;
            }
            return false;
        }

        internal void DispatchRpcMessage(MessageReader reader)
        {
            HandleRpcMessage(reader);
        }
    }
}
