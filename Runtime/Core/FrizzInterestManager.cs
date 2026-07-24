using System;
using System.Collections.Generic;
using UnityEngine;
using FrizzNet.Logging;

namespace FrizzNet.Core
{
    /// <summary>
    /// Optional distance-based interest management. Filters transform/voice replication
    /// to clients that are within range of the object or its owner.
    /// </summary>
    [DisallowMultipleComponent]
    public class FrizzInterestManager : MonoBehaviour
    {
        public static FrizzInterestManager Instance { get; private set; }

        [Header("Interest Settings")]
        [Tooltip("Maximum distance at which a client receives updates for a networked object.")]
        [SerializeField] private float m_InterestRadius = 50f;

        [Tooltip("If true, interest checks are performed for transform and voice replication.")]
        [SerializeField] private bool m_Enabled = true;

        public float InterestRadius { get => m_InterestRadius; set => m_InterestRadius = value; }
        public bool InterestEnabled { get => m_Enabled; set => m_Enabled = value; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Returns true if the given client should receive updates for the target object.
        /// </summary>
        public bool IsClientInterested(ulong clientId, NetworkIdentity target)
        {
            if (!m_Enabled || target == null || NetworkManager.Instance == null)
            {
                return true;
            }

            if (clientId == target.OwnerConnectionId)
            {
                return true;
            }

            Vector3? clientPosition = GetClientPosition(clientId);
            if (!clientPosition.HasValue)
            {
                return true;
            }

            float distance = Vector3.Distance(clientPosition.Value, target.transform.position);
            return distance <= m_InterestRadius;
        }

        /// <summary>
        /// Filters a client list to only those interested in the target object.
        /// </summary>
        public IEnumerable<ulong> FilterInterestedClients(NetworkIdentity target)
        {
            if (NetworkManager.Instance == null) yield break;

            foreach (ulong clientId in NetworkManager.Instance.ConnectedClients)
            {
                if (IsClientInterested(clientId, target))
                {
                    yield return clientId;
                }
            }
        }

        private Vector3? GetClientPosition(ulong clientId)
        {
            if (NetworkManager.Instance == null) return null;

            foreach (NetworkIdentity identity in NetworkManager.Instance.NetworkObjects.Values)
            {
                if (identity != null && identity.OwnerConnectionId == clientId && identity.IsLocalPlayer == false)
                {
                    return identity.transform.position;
                }
            }

            foreach (NetworkIdentity identity in NetworkManager.Instance.NetworkObjects.Values)
            {
                if (identity != null && identity.OwnerConnectionId == clientId)
                {
                    return identity.transform.position;
                }
            }

            return null;
        }
    }
}
