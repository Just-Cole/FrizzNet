using System;
using System.IO;
using System.Text;

namespace FrizzNet.Messaging
{
    /// <summary>
    /// Helper class for reading structured data from a byte array received over the network.
    /// </summary>
    public class MessageReader : IDisposable
    {
        private readonly MemoryStream m_Stream;
        private readonly BinaryReader m_Reader;

        /// <summary>
        /// Creates a new MessageReader targeting the given data buffer.
        /// </summary>
        public MessageReader(byte[] data)
        {
            m_Stream = new MemoryStream(data);
            m_Reader = new BinaryReader(m_Stream, Encoding.UTF8);
        }

        /// <summary>
        /// Creates a MessageReader targeting a segment of a given buffer.
        /// </summary>
        public MessageReader(byte[] data, int index, int count)
        {
            m_Stream = new MemoryStream(data, index, count);
            m_Reader = new BinaryReader(m_Stream, Encoding.UTF8);
        }

        /// <summary>
        /// Read a signed 16-bit integer (short).
        /// </summary>
        public short ReadShort() => m_Reader.ReadInt16();

        /// <summary>
        /// Read a signed 32-bit integer.
        /// </summary>
        public int ReadInt() => m_Reader.ReadInt32();

        /// <summary>
        /// Read a signed 64-bit integer.
        /// </summary>
        public long ReadLong() => m_Reader.ReadInt64();

        /// <summary>
        /// Read a single-precision floating-point number.
        /// </summary>
        public float ReadFloat() => m_Reader.ReadSingle();

        /// <summary>
        /// Read a double-precision floating-point number.
        /// </summary>
        public double ReadDouble() => m_Reader.ReadDouble();

        /// <summary>
        /// Read a UTF-8 string.
        /// </summary>
        public string ReadString() => m_Reader.ReadString();

        /// <summary>
        /// Read a boolean value.
        /// </summary>
        public bool ReadBool() => m_Reader.ReadBoolean();

        /// <summary>
        /// Read a length-prefixed byte array.
        /// </summary>
        public byte[] ReadBytes()
        {
            int length = m_Reader.ReadInt32();
            if (length <= 0) return Array.Empty<byte>();
            return m_Reader.ReadBytes(length);
        }

        /// <summary>
        /// Read a fixed number of raw bytes directly from the stream.
        /// </summary>
        public byte[] ReadRawBytes(int count)
        {
            if (count <= 0) return Array.Empty<byte>();
            return m_Reader.ReadBytes(count);
        }

        /// <summary>
        /// Total remaining bytes available to read.
        /// </summary>
        public int RemainingBytes => (int)(m_Stream.Length - m_Stream.Position);

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            m_Reader.Dispose();
            m_Stream.Dispose();
        }
    }
}
