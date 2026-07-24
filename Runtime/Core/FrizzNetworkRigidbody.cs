using UnityEngine;
using FrizzNet.Messaging;

namespace FrizzNet.Core
{
    /// <summary>
    /// Synchronizes Rigidbody physics state across the network using host-authoritative simulation.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(Rigidbody))]
    public class FrizzNetworkRigidbody : NetworkBehaviour
    {
        [Header("Sync Settings")]
        [Range(5, 60)]
        [SerializeField] private int m_SendRate = 20;

        [SerializeField] private float m_PositionThreshold = 0.05f;
        [SerializeField] private float m_VelocityThreshold = 0.1f;

        private Rigidbody m_Rigidbody;
        private float m_SendInterval;
        private float m_LastSendTime;
        private Vector3 m_LastSentPosition;
        private Vector3 m_LastSentVelocity;

        private Vector3 m_TargetPosition;
        private Vector3 m_TargetVelocity;
        private bool m_HasReceivedFirstUpdate;

        private const short MSG_RIGIDBODY = FrizzSystemMessages.Rigidbody;

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_SendInterval = 1f / m_SendRate;
        }

        private void FixedUpdate()
        {
            if (NetworkId == 0 || m_Rigidbody == null) return;

            if (HasAuthority)
            {
                if (Time.time - m_LastSendTime >= m_SendInterval)
                {
                    TrySendUpdate();
                }
            }
            else if (m_HasReceivedFirstUpdate)
            {
                m_Rigidbody.position = Vector3.Lerp(m_Rigidbody.position, m_TargetPosition, Time.fixedDeltaTime * 15f);
                m_Rigidbody.linearVelocity = Vector3.Lerp(m_Rigidbody.linearVelocity, m_TargetVelocity, Time.fixedDeltaTime * 10f);
            }
        }

        public void OnReceiveUpdate(Vector3 position, Vector3 velocity)
        {
            m_TargetPosition = position;
            m_TargetVelocity = velocity;

            if (!m_HasReceivedFirstUpdate)
            {
                m_Rigidbody.position = position;
                m_Rigidbody.linearVelocity = velocity;
                m_HasReceivedFirstUpdate = true;
            }
        }

        private void TrySendUpdate()
        {
            Vector3 pos = m_Rigidbody.position;
            Vector3 vel = m_Rigidbody.linearVelocity;

            if (Vector3.Distance(m_LastSentPosition, pos) <= m_PositionThreshold &&
                Vector3.Distance(m_LastSentVelocity, vel) <= m_VelocityThreshold)
            {
                return;
            }

            m_LastSendTime = Time.time;
            m_LastSentPosition = pos;
            m_LastSentVelocity = vel;

            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteLong((long)NetworkId);
                writer.WriteFloat(pos.x);
                writer.WriteFloat(pos.y);
                writer.WriteFloat(pos.z);
                writer.WriteFloat(vel.x);
                writer.WriteFloat(vel.y);
                writer.WriteFloat(vel.z);

                if (NetworkManager.Instance.IsHost)
                {
                    NetworkManager.Instance.SendToAll(MSG_RIGIDBODY, writer, false);
                }
                else if (NetworkManager.Instance.IsClient)
                {
                    NetworkManager.Instance.SendToServer(MSG_RIGIDBODY, writer, false);
                }
            }
        }
    }
}
