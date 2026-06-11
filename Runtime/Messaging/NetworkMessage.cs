namespace FrizzNet.Messaging
{
    /// <summary>
    /// Represents a raw network message package received from or sent to a connection.
    /// </summary>
    public struct NetworkMessage
    {
        /// <summary>
        /// The connection ID associated with this message.
        /// </summary>
        public ulong ConnectionId;

        /// <summary>
        /// The serialized packet content.
        /// </summary>
        public byte[] Content;
    }
}
