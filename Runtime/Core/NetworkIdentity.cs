using UnityEngine;

namespace FrizzNet.Core
{
    /// <summary>
    /// Component placed on GameObjects that need to be synchronized and tracked across the network.
    /// Manages network identity, owner authority, and player association.
    /// </summary>
    [DisallowMultipleComponent]
    [FrizzHelp("Identifies this GameObject uniquely across the network. Tracks authority status, ownership credentials, and links players.", "index.html#NetworkIdentity")]
    public class NetworkIdentity : MonoBehaviour
    {
        [Header("Network Identity")]
        [Tooltip("Unique network identifier assigned dynamically by the NetworkManager.")]
        [ReadOnlyInspector] public ulong NetworkId;

        [Tooltip("The connection ID (SteamID) of the player who owns / has authority over this object.")]
        [ReadOnlyInspector] public ulong OwnerConnectionId;

        [Tooltip("Registered prefab asset name used for late-join and host migration replication.")]
        [ReadOnlyInspector] public string PrefabAssetName;

        [Tooltip("Whether this object is controlled by the local client.")]
        [ReadOnlyInspector] [SerializeField] private bool m_HasAuthority;

        [Tooltip("Whether this object represents the local player character.")]
        [ReadOnlyInspector] [SerializeField] private bool m_IsLocalPlayer;

        private NetworkBehaviour[] m_Behaviours;

        private void Awake()
        {
            InitializeBehaviours();
        }

        /// <summary>
        /// Scans and caches all attached NetworkBehaviour scripts, assigning this identity to them.
        /// </summary>
        public void InitializeBehaviours()
        {
            m_Behaviours = GetComponents<NetworkBehaviour>();
            foreach (var behaviour in m_Behaviours)
            {
                behaviour.NetworkIdentity = this;
            }
        }

        /// <summary>
        /// Gets whether the local client has networking authority over this object.
        /// </summary>
        public bool HasAuthority => m_HasAuthority;

        /// <summary>
        /// Gets whether this object represents the local player.
        /// </summary>
        public bool IsLocalPlayer => m_IsLocalPlayer;

        /// <summary>
        /// Sets the authority and player flags. Intended for use by the NetworkManager.
        /// </summary>
        public void SetAuthority(bool hasAuthority, bool isLocalPlayer)
        {
            bool wasAuthority = m_HasAuthority;
            m_HasAuthority = hasAuthority;
            m_IsLocalPlayer = isLocalPlayer;

            if (NetworkId != 0 && m_Behaviours != null)
            {
                foreach (var behaviour in m_Behaviours)
                {
                    if (behaviour == null) continue;
                    if (hasAuthority && !wasAuthority)
                    {
                        behaviour.OnStartAuthority();
                    }
                    else if (!hasAuthority && wasAuthority)
                    {
                        behaviour.OnStopAuthority();
                    }
                }
            }
        }

        /// <summary>
        /// Invoked by the NetworkManager when this identity is registered on the network.
        /// </summary>
        public void OnSpawn()
        {
            InitializeBehaviours();
            foreach (var behaviour in m_Behaviours)
            {
                if (behaviour == null) continue;
                behaviour.OnNetworkSpawn();
                if (HasAuthority)
                {
                    behaviour.OnStartAuthority();
                }
                if (IsLocalPlayer)
                {
                    behaviour.OnStartLocalPlayer();
                }
            }
        }

        private void OnDestroy()
        {
            if (NetworkId != 0 && m_Behaviours != null)
            {
                foreach (var behaviour in m_Behaviours)
                {
                    if (behaviour != null)
                    {
                        behaviour.OnNetworkDespawn();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Property drawer helper to display properties as read-only in the Unity Inspector.
    /// </summary>
    public class ReadOnlyInspectorAttribute : PropertyAttribute { }
}
