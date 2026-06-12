using System;
using System.Collections.Generic;
using UnityEngine;
using FrizzNet.Messaging;
using FrizzNet.Logging;

namespace FrizzNet.Core
{
    /// <summary>
    /// Synchronizes Unity Animator component states, parameters, and triggers across the network.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [FrizzHelp("Synchronizes Unity Animator state, parameters, and triggers smoothly across all network clients.", "index.html#FrizzNetworkAnimator")]
    public class FrizzNetworkAnimator : MonoBehaviour
    {
        private struct TrackedParameter
        {
            public int Hash;
            public AnimatorControllerParameterType Type;
            public object LastValue;
        }

        [Header("References")]
        [Tooltip("The Animator component to synchronize. If unassigned, will search local GameObject.")]
        [SerializeField] private Animator m_Animator;

        [Header("Sync Settings")]
        [Tooltip("Frequency of parameter checks and updates per second.")]
        [Range(1, 60)]
        [SerializeField] private int m_SendRate = 20;

        private NetworkIdentity m_Identity;
        private readonly List<TrackedParameter> m_TrackedParameters = new List<TrackedParameter>();
        private float m_SendInterval;
        private float m_LastSendTime;

        private const short MSG_ANIMATION = -14;

        // Public properties
        public Animator TargetAnimator { get => m_Animator; set => m_Animator = value; }
        public int SendRate { get => m_SendRate; set => m_SendRate = value; }

        private void Awake()
        {
            m_Identity = GetComponent<NetworkIdentity>();
            if (m_Animator == null)
            {
                m_Animator = GetComponent<Animator>();
            }
            m_SendInterval = 1f / m_SendRate;
        }

        private void Start()
        {
            if (m_Animator == null)
            {
                FrizzLogger.LogError($"[FrizzNetworkAnimator] No Animator component found on '{gameObject.name}'.");
                return;
            }

            CacheAnimatorParameters();
        }

        private void CacheAnimatorParameters()
        {
            m_TrackedParameters.Clear();
            foreach (var param in m_Animator.parameters)
            {
                // Triggers are synchronized instantly via SetTrigger API, not polled.
                if (param.type == AnimatorControllerParameterType.Trigger) continue;

                m_TrackedParameters.Add(new TrackedParameter
                {
                    Hash = param.nameHash,
                    Type = param.type,
                    LastValue = GetParameterValue(param.nameHash, param.type)
                });
            }
        }

        private object GetParameterValue(int hash, AnimatorControllerParameterType type)
        {
            switch (type)
            {
                case AnimatorControllerParameterType.Float: return m_Animator.GetFloat(hash);
                case AnimatorControllerParameterType.Int: return m_Animator.GetInteger(hash);
                case AnimatorControllerParameterType.Bool: return m_Animator.GetBool(hash);
                default: return null;
            }
        }

        private void Update()
        {
            if (m_Animator == null || m_Identity == null) return;

            // Only the owner with network authority checks and dispatches parameter updates
            if (m_Identity.HasAuthority && Time.time - m_LastSendTime >= m_SendInterval)
            {
                SendParameterUpdates();
                m_LastSendTime = Time.time;
            }
        }

        private void SendParameterUpdates()
        {
            if (NetworkManager.Instance == null) return;

            bool hasChanges = false;
            using (MessageWriter payloadWriter = new MessageWriter())
            {
                int changeCount = 0;
                // Leave room for changeCount (int) at start of payload
                payloadWriter.WriteInt(0);

                for (int i = 0; i < m_TrackedParameters.Count; i++)
                {
                    var param = m_TrackedParameters[i];
                    bool changed = false;
                    object currentValue = null;

                    switch (param.Type)
                    {
                        case AnimatorControllerParameterType.Float:
                            float fVal = m_Animator.GetFloat(param.Hash);
                            currentValue = fVal;
                            if (param.LastValue == null || Math.Abs((float)param.LastValue - fVal) > 0.0001f)
                            {
                                changed = true;
                            }
                            break;
                        case AnimatorControllerParameterType.Int:
                            int iVal = m_Animator.GetInteger(param.Hash);
                            currentValue = iVal;
                            if (param.LastValue == null || (int)param.LastValue != iVal)
                            {
                                changed = true;
                            }
                            break;
                        case AnimatorControllerParameterType.Bool:
                            bool bVal = m_Animator.GetBool(param.Hash);
                            currentValue = bVal;
                            if (param.LastValue == null || (bool)param.LastValue != bVal)
                            {
                                changed = true;
                            }
                            break;
                    }

                    if (changed)
                    {
                        payloadWriter.WriteInt(param.Hash);
                        payloadWriter.WriteInt((int)param.Type);
                        switch (param.Type)
                        {
                            case AnimatorControllerParameterType.Float:
                                payloadWriter.WriteFloat((float)currentValue);
                                break;
                            case AnimatorControllerParameterType.Int:
                                payloadWriter.WriteInt((int)currentValue);
                                break;
                            case AnimatorControllerParameterType.Bool:
                                payloadWriter.WriteBool((bool)currentValue);
                                break;
                        }

                        // Update tracked state
                        param.LastValue = currentValue;
                        m_TrackedParameters[i] = param;
                        changeCount++;
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    byte[] payloadBytes = payloadWriter.ToArray();
                    
                    // Write back the final change count at the beginning of payload
                    byte[] countBytes = BitConverter.GetBytes(changeCount);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(countBytes);
                    Array.Copy(countBytes, 0, payloadBytes, 0, 4);

                    using (MessageWriter systemWriter = new MessageWriter())
                    {
                        systemWriter.WriteLong((long)m_Identity.NetworkId);
                        systemWriter.WriteInt(payloadBytes.Length);
                        systemWriter.WriteRawBytes(payloadBytes);

                        if (NetworkManager.Instance.IsHost)
                        {
                            NetworkManager.Instance.SendToAll(MSG_ANIMATION, systemWriter, true);
                        }
                        else
                        {
                            NetworkManager.Instance.SendToServer(MSG_ANIMATION, systemWriter, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets the trigger in the animator and replicates it across the network.
        /// </summary>
        public void SetTrigger(string name)
        {
            SetTrigger(Animator.StringToHash(name));
        }

        /// <summary>
        /// Sets the trigger in the animator using its hash and replicates it across the network.
        /// </summary>
        public void SetTrigger(int hash)
        {
            if (m_Animator == null) return;
            m_Animator.SetTrigger(hash);

            if (NetworkManager.Instance == null || m_Identity == null) return;
            if (!m_Identity.HasAuthority) return;

            // Instantly send a trigger packet over the network
            using (MessageWriter payloadWriter = new MessageWriter())
            {
                payloadWriter.WriteInt(1); // changeCount = 1
                payloadWriter.WriteInt(hash);
                payloadWriter.WriteInt((int)AnimatorControllerParameterType.Trigger);

                byte[] payloadBytes = payloadWriter.ToArray();
                using (MessageWriter systemWriter = new MessageWriter())
                {
                    systemWriter.WriteLong((long)m_Identity.NetworkId);
                    systemWriter.WriteInt(payloadBytes.Length);
                    systemWriter.WriteRawBytes(payloadBytes);

                    if (NetworkManager.Instance.IsHost)
                    {
                        NetworkManager.Instance.SendToAll(MSG_ANIMATION, systemWriter, true);
                    }
                    else
                    {
                        NetworkManager.Instance.SendToServer(MSG_ANIMATION, systemWriter, true);
                    }
                }
            }
        }

        /// <summary>
        /// Applies animation parameter updates received from the network.
        /// </summary>
        public void OnReceiveUpdate(byte[] payload)
        {
            if (m_Animator == null) return;

            using (MessageReader reader = new MessageReader(payload, 0, payload.Length))
            {
                if (reader.RemainingBytes < 4) return;
                int changeCount = reader.ReadInt();

                for (int i = 0; i < changeCount; i++)
                {
                    if (reader.RemainingBytes < 5) break;
                    int hash = reader.ReadInt();
                    int typeByte = reader.ReadInt();
                    AnimatorControllerParameterType type = (AnimatorControllerParameterType)typeByte;

                    switch (type)
                    {
                        case AnimatorControllerParameterType.Float:
                            if (reader.RemainingBytes >= 4)
                            {
                                float val = reader.ReadFloat();
                                m_Animator.SetFloat(hash, val);
                            }
                            break;
                        case AnimatorControllerParameterType.Int:
                            if (reader.RemainingBytes >= 4)
                            {
                                int val = reader.ReadInt();
                                m_Animator.SetInteger(hash, val);
                            }
                            break;
                        case AnimatorControllerParameterType.Bool:
                            if (reader.RemainingBytes >= 1)
                            {
                                bool val = reader.ReadBool();
                                m_Animator.SetBool(hash, val);
                            }
                            break;
                        case AnimatorControllerParameterType.Trigger:
                            m_Animator.SetTrigger(hash);
                            break;
                    }
                }
            }
        }
    }
}
