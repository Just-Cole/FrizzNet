using System;
using System.IO;
using System.Text;

namespace FrizzNet.Messaging
{
    /// <summary>
    /// Helper class for writing structured data into a byte array for network transmission.
    /// </summary>
    public class MessageWriter : IDisposable
    {
        private readonly MemoryStream m_Stream;
        private readonly BinaryWriter m_Writer;

        /// <summary>
        /// Creates a new MessageWriter with an empty buffer.
        /// </summary>
        public MessageWriter()
        {
            m_Stream = new MemoryStream();
            m_Writer = new BinaryWriter(m_Stream, Encoding.UTF8);
        }

        /// <summary>
        /// Write a signed 16-bit integer (short).
        /// </summary>
        public void WriteShort(short value) => m_Writer.Write(value);

        /// <summary>
        /// Write a signed 32-bit integer.
        /// </summary>
        public void WriteInt(int value) => m_Writer.Write(value);

        /// <summary>
        /// Write a signed 64-bit integer.
        /// </summary>
        public void WriteLong(long value) => m_Writer.Write(value);

        /// <summary>
        /// Write a single-precision floating-point number.
        /// </summary>
        public void WriteFloat(float value) => m_Writer.Write(value);

        /// <summary>
        /// Write a double-precision floating-point number.
        /// </summary>
        public void WriteDouble(double value) => m_Writer.Write(value);

        /// <summary>
        /// Write a UTF-8 string prefixed with its length.
        /// </summary>
        public void WriteString(string value) => m_Writer.Write(value ?? string.Empty);

        /// <summary>
        /// Write a boolean value.
        /// </summary>
        public void WriteBool(bool value) => m_Writer.Write(value);

        /// <summary>
        /// Write a byte array prefixed with its length.
        /// </summary>
        public void WriteBytes(byte[] value)
        {
            if (value == null)
            {
                m_Writer.Write(0);
                return;
            }
            m_Writer.Write(value.Length);
            m_Writer.Write(value);
        }

        /// <summary>
        /// Write raw bytes directly to the buffer without any length prefix.
        /// </summary>
        public void WriteRawBytes(byte[] value)
        {
            if (value != null && value.Length > 0)
            {
                m_Writer.Write(value);
            }
        }

        /// <summary>
        /// Write a segment of raw bytes directly to the buffer without any length prefix.
        /// </summary>
        public void WriteRawBytes(byte[] value, int offset, int count)
        {
            if (value == null || count <= 0)
            {
                return;
            }

            m_Writer.Write(value, offset, count);
        }

        /// <summary>
        /// Returns the compiled byte array representation of the written data.
        /// </summary>
        public byte[] ToArray()
        {
            m_Writer.Flush();
            return m_Stream.ToArray();
        }

        /// <summary>
        /// Gets the current length of the written data.
        /// </summary>
        public int Length => (int)m_Stream.Length;

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            m_Writer.Dispose();
            m_Stream.Dispose();
        }
    }
}
