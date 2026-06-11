using UnityEngine;
using FrizzNet.Messaging;

namespace FrizzNet.Core
{
    /// <summary>
    /// Synchronizes the position and rotation of a GameObject across the network.
    /// Uses interpolation for smooth rendering of remote objects.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [FrizzHelp("Synchronizes the position and rotation of this GameObject across the network unreliably. Smoothly interpolates remote positions to eliminate jitter.", "file:///c:/Users/tyjus/Documents/UnityGames/Editortools/Assets/FrizzNet/Documentation/SetupGuide.md")]
    public class FrizzNetworkTransform : MonoBehaviour
    {
        private NetworkIdentity m_Identity;

        [Header("Sync Configuration")]
        [Tooltip("Number of synchronization packets sent per second.")]
        [Range(1, 60)]
        [SerializeField] private int m_SendRate = 20;

        [Tooltip("Minimum distance change required to trigger a network update.")]
        [SerializeField] private float m_PositionThreshold = 0.01f;

        [Tooltip("Minimum angle change (degrees) required to trigger a network update.")]
        [SerializeField] private float m_RotationThreshold = 0.5f;

        [Header("Smoothing")]
        [Tooltip("Speed of interpolation. Higher values mean quicker snap, lower values mean smoother movement.")]
        [SerializeField] private float m_LerpSpeed = 15f;

        private float m_SendInterval;
        private float m_LastSendTime;

        private Vector3 m_LastSentPosition;
        private Quaternion m_LastSentRotation;

        // Remote Target Values for Interpolation
        private Vector3 m_TargetPosition;
        private Quaternion m_TargetRotation;
        private bool m_HasReceivedFirstUpdate;

        private const short MSG_TRANSFORM = -12;

        private void Awake()
        {
            m_Identity = GetComponent<NetworkIdentity>();
            m_SendInterval = 1f / m_SendRate;
        }

        private void Start()
        {
            m_TargetPosition = transform.position;
            m_TargetRotation = transform.rotation;
            
            m_LastSentPosition = transform.position;
            m_LastSentRotation = transform.rotation;
        }

        private void Update()
        {
            if (m_Identity == null || m_Identity.NetworkId == 0) return;

            if (m_Identity.HasAuthority)
            {
                // Send updates if we have moved/rotated past thresholds
                if (Time.time - m_LastSendTime >= m_SendInterval)
                {
                    CheckAndSendUpdate();
                }
            }
            else
            {
                // Smoothly interpolate remote object transform
                if (m_HasReceivedFirstUpdate)
                {
                    transform.position = Vector3.Lerp(transform.position, m_TargetPosition, Time.deltaTime * m_LerpSpeed);
                    transform.rotation = Quaternion.Slerp(transform.rotation, m_TargetRotation, Time.deltaTime * m_LerpSpeed);
                }
            }
        }

        private void CheckAndSendUpdate()
        {
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;

            bool posChanged = Vector3.Distance(m_LastSentPosition, currentPos) > m_PositionThreshold;
            bool rotChanged = Quaternion.Angle(m_LastSentRotation, currentRot) > m_RotationThreshold;

            if (posChanged || rotChanged)
            {
                m_LastSendTime = Time.time;
                m_LastSentPosition = currentPos;
                m_LastSentRotation = currentRot;

                using (MessageWriter writer = new MessageWriter())
                {
                    writer.WriteLong((long)m_Identity.NetworkId);
                    writer.WriteFloat(currentPos.x);
                    writer.WriteFloat(currentPos.y);
                    writer.WriteFloat(currentPos.z);
                    writer.WriteFloat(currentRot.x);
                    writer.WriteFloat(currentRot.y);
                    writer.WriteFloat(currentRot.z);
                    writer.WriteFloat(currentRot.w);

                    // Client sends to server, Server sends to all clients
                    if (NetworkManager.Instance.IsHost)
                    {
                        NetworkManager.Instance.SendToAll(MSG_TRANSFORM, writer, false); // Unreliable for transform sync
                    }
                    else if (NetworkManager.Instance.IsClient)
                    {
                        NetworkManager.Instance.SendToServer(MSG_TRANSFORM, writer, false); // Unreliable for transform sync
                    }
                }
            }
        }

        /// <summary>
        /// Update target values received from the network.
        /// Called by the NetworkManager.
        /// </summary>
        public void OnReceiveUpdate(Vector3 position, Quaternion rotation)
        {
            m_TargetPosition = position;
            m_TargetRotation = rotation;

            if (!m_HasReceivedFirstUpdate)
            {
                transform.position = position;
                transform.rotation = rotation;
                m_HasReceivedFirstUpdate = true;
            }
        }
    }
}
