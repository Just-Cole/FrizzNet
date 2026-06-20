using System;
using System.Collections.Generic;
using UnityEngine;
using FrizzNet.Messaging;
using FrizzNet.Logging;

namespace FrizzNet.Core
{
    /// <summary>
    /// Replicated network variable with host-authoritative writes.
    /// Attach to a NetworkBehaviour and call Initialize() from OnNetworkSpawn.
    /// </summary>
    [Serializable]
    public class FrizzNetworkVariable<T>
    {
        [SerializeField] private T m_DefaultValue;

        private NetworkBehaviour m_Owner;
        private short m_SyncId;
        private T m_Value;
        private bool m_Initialized;

        public event Action<T, T> OnValueChanged;

        public T Value
        {
            get => m_Value;
            set => SetValue(value);
        }

        public FrizzNetworkVariable(T defaultValue = default)
        {
            m_DefaultValue = defaultValue;
            m_Value = defaultValue;
        }

        /// <summary>
        /// Initializes the variable with a unique sync ID on the owning NetworkBehaviour.
        /// </summary>
        public void Initialize(NetworkBehaviour owner, short syncId)
        {
            m_Owner = owner;
            m_SyncId = syncId;
            m_Value = m_DefaultValue;
            m_Initialized = true;
        }

        /// <summary>
        /// Sets the value. Host applies immediately and replicates; clients send a request to the host.
        /// </summary>
        public void SetValue(T newValue, bool forceLocal = false)
        {
            if (!m_Initialized || m_Owner == null) return;

            if (EqualityComparer<T>.Default.Equals(m_Value, newValue)) return;

            if (m_Owner.IsServer || forceLocal)
            {
                ApplyValue(newValue, true);
            }
            else if (m_Owner.IsClient)
            {
                using (MessageWriter writer = BuildSyncPayload(newValue))
                {
                    NetworkManager.Instance.SendToServer(FrizzSystemMessages.SyncVar, writer, true);
                }
            }
        }

        internal void ApplyFromNetwork(T newValue)
        {
            if (EqualityComparer<T>.Default.Equals(m_Value, newValue)) return;
            T oldValue = m_Value;
            m_Value = newValue;
            OnValueChanged?.Invoke(oldValue, newValue);
        }

        internal void WritePayload(MessageWriter writer)
        {
            writer.WriteLong((long)m_Owner.NetworkId);
            writer.WriteShort(m_SyncId);
            WriteTypedValue(writer, newValue: m_Value);
        }

        private void ApplyValue(T newValue, bool replicate)
        {
            T oldValue = m_Value;
            m_Value = newValue;
            OnValueChanged?.Invoke(oldValue, newValue);

            if (replicate && m_Owner.IsServer)
            {
                using (MessageWriter writer = BuildSyncPayload(newValue))
                {
                    NetworkManager.Instance.SendToAll(FrizzSystemMessages.SyncVar, writer, true);
                }
            }
        }

        private MessageWriter BuildSyncPayload(T newValue)
        {
            MessageWriter writer = new MessageWriter();
            writer.WriteLong((long)m_Owner.NetworkId);
            writer.WriteShort(m_SyncId);
            WriteTypedValue(writer, newValue);
            return writer;
        }

        private void WriteTypedValue(MessageWriter writer, T newValue)
        {
            Type type = typeof(T);
            if (type == typeof(int)) writer.WriteInt((int)(object)newValue);
            else if (type == typeof(float)) writer.WriteFloat((float)(object)newValue);
            else if (type == typeof(bool)) writer.WriteBool((bool)(object)newValue);
            else if (type == typeof(string)) writer.WriteString((string)(object)newValue);
            else if (type == typeof(long)) writer.WriteLong((long)(object)newValue);
            else if (type == typeof(ulong)) writer.WriteLong((long)(ulong)(object)newValue);
            else if (type == typeof(Vector3))
            {
                Vector3 v = (Vector3)(object)newValue;
                writer.WriteFloat(v.x);
                writer.WriteFloat(v.y);
                writer.WriteFloat(v.z);
            }
            else
            {
                throw new NotSupportedException($"FrizzNetworkVariable type '{type.Name}' is not supported.");
            }
        }

        internal bool TryReadValue(MessageReader reader, out T value)
        {
            Type type = typeof(T);
            if (type == typeof(int)) { value = (T)(object)reader.ReadInt(); return true; }
            if (type == typeof(float)) { value = (T)(object)reader.ReadFloat(); return true; }
            if (type == typeof(bool)) { value = (T)(object)reader.ReadBool(); return true; }
            if (type == typeof(string)) { value = (T)(object)reader.ReadString(); return true; }
            if (type == typeof(long)) { value = (T)(object)reader.ReadLong(); return true; }
            if (type == typeof(ulong)) { value = (T)(object)(ulong)reader.ReadLong(); return true; }
            if (type == typeof(Vector3))
            {
                value = (T)(object)new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                return true;
            }

            value = default;
            return false;
        }
    }
}
