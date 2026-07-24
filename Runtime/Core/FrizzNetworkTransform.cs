using UnityEngine;
using FrizzNet.Messaging;

namespace FrizzNet.Core
{
    /// <summary>
    /// Synchronizes the position and rotation of a GameObject across the network.
    /// Uses interpolation for smooth rendering of remote objects.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class FrizzNetworkTransform : NetworkBehaviour
    {

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

        [Header("Scale Sync")]
        [Tooltip("Should local scale also be synchronized across the network?")]
        [SerializeField] private bool m_SyncScale = true;
        [Tooltip("Minimum scale change required to trigger a network update.")]
        [SerializeField] private float m_ScaleThreshold = 0.05f;

        private Vector3 m_LastSentScale;
        private Vector3 m_TargetScale;

        private void Awake()
        {
            m_SendInterval = 1f / m_SendRate;
        }

        private void Start()
        {
            m_TargetPosition = transform.position;
            m_TargetRotation = transform.rotation;
            m_TargetScale = transform.localScale;
            
            m_LastSentPosition = transform.position;
            m_LastSentRotation = transform.rotation;
            m_LastSentScale = transform.localScale;
        }

        private void Update()
        {
            if (NetworkId == 0) return;

            if (HasAuthority)
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
                    if (m_SyncScale)
                    {
                        transform.localScale = Vector3.Lerp(transform.localScale, m_TargetScale, Time.deltaTime * m_LerpSpeed);
                    }
                }
            }
        }

        /// <summary>
        /// Forces an immediate transform broadcast regardless of authority (host-only).
        /// </summary>
        public void ForceBroadcastState()
        {
            if (NetworkId == 0 || NetworkManager.Instance == null) return;
            if (!NetworkManager.Instance.IsHost && !HasAuthority) return;

            m_LastSendTime = Time.time;
            m_LastSentPosition = transform.position;
            m_LastSentRotation = transform.rotation;
            m_LastSentScale = transform.localScale;
            SendTransformUpdate(transform.position, transform.rotation, transform.localScale);
        }

        private void SendTransformUpdate(Vector3 currentPos, Quaternion currentRot, Vector3 currentScale)
        {
            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteLong((long)NetworkId);
                writer.WriteFloat(currentPos.x);
                writer.WriteFloat(currentPos.y);
                writer.WriteFloat(currentPos.z);
                writer.WriteFloat(currentRot.x);
                writer.WriteFloat(currentRot.y);
                writer.WriteFloat(currentRot.z);
                writer.WriteFloat(currentRot.w);
                writer.WriteBool(m_SyncScale);
                if (m_SyncScale)
                {
                    writer.WriteFloat(currentScale.x);
                    writer.WriteFloat(currentScale.y);
                    writer.WriteFloat(currentScale.z);
                }

                if (NetworkManager.Instance.IsHost)
                {
                    NetworkManager.Instance.SendToAll(FrizzSystemMessages.Transform, writer, false);
                }
                else if (NetworkManager.Instance.IsClient)
                {
                    NetworkManager.Instance.SendToServer(FrizzSystemMessages.Transform, writer, false);
                }
            }
        }

        private void CheckAndSendUpdate()
        {
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;
            Vector3 currentScale = transform.localScale;

            bool posChanged = Vector3.Distance(m_LastSentPosition, currentPos) > m_PositionThreshold;
            bool rotChanged = Quaternion.Angle(m_LastSentRotation, currentRot) > m_RotationThreshold;
            bool scaleChanged = m_SyncScale && Vector3.Distance(m_LastSentScale, currentScale) > m_ScaleThreshold;

            if (posChanged || rotChanged || scaleChanged)
            {
                m_LastSendTime = Time.time;
                m_LastSentPosition = currentPos;
                m_LastSentRotation = currentRot;
                m_LastSentScale = currentScale;
                SendTransformUpdate(currentPos, currentRot, currentScale);
            }
        }

        /// <summary>
        /// Update target values received from the network.
        /// Called by the NetworkManager.
        /// </summary>
        public void OnReceiveUpdate(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            m_TargetPosition = position;
            m_TargetRotation = rotation;
            m_TargetScale = scale;

            if (!m_HasReceivedFirstUpdate)
            {
                transform.position = position;
                transform.rotation = rotation;
                if (m_SyncScale)
                {
                    transform.localScale = scale;
                }
                m_HasReceivedFirstUpdate = true;
            }
        }
    }
}
