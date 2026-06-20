using System;
using System.Collections.Generic;
using FrizzNet.Messaging;
using FrizzNet.Logging;

namespace FrizzNet.Core
{
    /// <summary>
    /// Target audience for a remote procedure call.
    /// </summary>
    public enum FrizzRpcTarget
    {
        Server,
        AllClients,
        Owner,
        OtherClients
    }

    /// <summary>
    /// Marks a method as a network RPC handler. Methods must be registered via RegisterRpcHandlers().
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FrizzRpcAttribute : Attribute
    {
        public short RpcId { get; }
        public FrizzRpcTarget Target { get; }

        public FrizzRpcAttribute(short rpcId, FrizzRpcTarget target = FrizzRpcTarget.Server)
        {
            RpcId = rpcId;
            Target = target;
        }
    }

    /// <summary>
    /// Base RPC dispatch utilities for NetworkBehaviour subclasses.
    /// </summary>
    public abstract partial class NetworkBehaviour
    {
        private readonly Dictionary<short, Action<MessageReader>> m_RpcHandlers = new Dictionary<short, Action<MessageReader>>();

        /// <summary>
        /// Registers RPC handlers declared with FrizzRpcAttribute on this behaviour.
        /// Call from OnNetworkSpawn after base registration.
        /// </summary>
        protected void RegisterRpcHandlers()
        {
            var methods = GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                var attr = (FrizzRpcAttribute)Attribute.GetCustomAttribute(method, typeof(FrizzRpcAttribute));
                if (attr == null) continue;

                short rpcId = attr.RpcId;
                if (m_RpcHandlers.ContainsKey(rpcId))
                {
                    FrizzLogger.LogWarning($"Duplicate RPC ID {rpcId} on {GetType().Name}.{method.Name}");
                    continue;
                }

                m_RpcHandlers.Add(rpcId, reader =>
                {
                    var parameters = method.GetParameters();
                    object[] args = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        args[i] = ReadRpcParameter(reader, parameters[i].ParameterType);
                    }
                    method.Invoke(this, args);
                });
            }
        }

        /// <summary>
        /// Sends an RPC to the host from a client-owned object.
        /// </summary>
        protected void SendServerRpc(short rpcId, MessageWriter writer)
        {
            if (NetworkManager.Instance == null || NetworkId == 0) return;
            SendRpcInternal(FrizzRpcTarget.Server, rpcId, writer, 0);
        }

        /// <summary>
        /// Sends an RPC to all clients from the host.
        /// </summary>
        protected void SendClientRpc(short rpcId, MessageWriter writer)
        {
            if (NetworkManager.Instance == null || NetworkId == 0) return;
            SendRpcInternal(FrizzRpcTarget.AllClients, rpcId, writer, 0);
        }

        /// <summary>
        /// Sends an RPC to the owner client from the host.
        /// </summary>
        protected void SendOwnerRpc(short rpcId, MessageWriter writer)
        {
            if (NetworkManager.Instance == null || NetworkId == 0) return;
            ulong ownerId = NetworkIdentity != null ? NetworkIdentity.OwnerConnectionId : 0;
            SendRpcInternal(FrizzRpcTarget.Owner, rpcId, writer, ownerId);
        }

        internal void HandleRpcMessage(MessageReader reader)
        {
            short rpcId = reader.ReadShort();
            if (m_RpcHandlers.TryGetValue(rpcId, out Action<MessageReader> handler))
            {
                handler?.Invoke(reader);
            }
            else
            {
                FrizzLogger.LogWarning($"No RPC handler for ID {rpcId} on {GetType().Name} (NetworkId {NetworkId})");
            }
        }

        private void SendRpcInternal(FrizzRpcTarget target, short rpcId, MessageWriter payload, ulong targetClientId)
        {
            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteLong((long)NetworkId);
                writer.WriteShort(rpcId);
                writer.WriteRawBytes(payload.ToArray());

                if (target == FrizzRpcTarget.Server)
                {
                    if (NetworkManager.Instance.IsClient)
                    {
                        NetworkManager.Instance.SendToServer(FrizzSystemMessages.Rpc, writer, true);
                    }
                    else if (NetworkManager.Instance.IsHost)
                    {
                        using (MessageReader localReader = new MessageReader(BuildLocalRpcPayload(rpcId, payload)))
                        {
                            HandleRpcMessage(localReader);
                        }
                    }
                }
                else if (NetworkManager.Instance.IsHost)
                {
                    if (target == FrizzRpcTarget.AllClients)
                    {
                        NetworkManager.Instance.SendToAll(FrizzSystemMessages.Rpc, writer, true);
                    }
                    else if (target == FrizzRpcTarget.Owner && targetClientId != 0)
                    {
                        NetworkManager.Instance.SendToClient(targetClientId, FrizzSystemMessages.Rpc, writer, true);
                    }
                    else if (target == FrizzRpcTarget.OtherClients)
                    {
                        foreach (ulong clientId in NetworkManager.Instance.ConnectedClients)
                        {
                            if (clientId != NetworkManager.LocalConnectionId)
                            {
                                NetworkManager.Instance.SendToClient(clientId, FrizzSystemMessages.Rpc, writer, true);
                            }
                        }
                    }

                    HandleRpcMessage(new MessageReader(CombineRpcPayload(rpcId, payload)));
                }
            }
        }

        private static byte[] BuildLocalRpcPayload(short rpcId, MessageWriter payload)
        {
            return CombineRpcPayload(rpcId, payload);
        }

        private static byte[] CombineRpcPayload(short rpcId, MessageWriter payload)
        {
            using (MessageWriter combined = new MessageWriter())
            {
                combined.WriteShort(rpcId);
                combined.WriteRawBytes(payload.ToArray());
                return combined.ToArray();
            }
        }

        private static object ReadRpcParameter(MessageReader reader, Type type)
        {
            if (type == typeof(int)) return reader.ReadInt();
            if (type == typeof(float)) return reader.ReadFloat();
            if (type == typeof(bool)) return reader.ReadBool();
            if (type == typeof(string)) return reader.ReadString();
            if (type == typeof(long)) return reader.ReadLong();
            if (type == typeof(ulong)) return (ulong)reader.ReadLong();
            if (type == typeof(UnityEngine.Vector3))
            {
                return new UnityEngine.Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            }
            if (type == typeof(UnityEngine.Quaternion))
            {
                return new UnityEngine.Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            }
            throw new NotSupportedException($"RPC parameter type '{type.Name}' is not supported.");
        }
    }
}
