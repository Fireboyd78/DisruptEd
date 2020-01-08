using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nomad
{
    public sealed class BinaryStream : Stream, IDisposable
    {
        Stream m_stream;
        
        #region Abstract implementation
        public override bool CanRead
        {
            get { return m_stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return m_stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return m_stream.CanWrite; }
        }

        public override long Length
        {
            get { return m_stream.Length; }
        }

        public override long Position
        {
            get { return m_stream.Position; }
            set { m_stream.Position = value; }
        }

        public override void Flush()
        {
            m_stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return m_stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return m_stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            m_stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            m_stream.Write(buffer, offset, count);
        }
        #endregion

        #region Virtual overrides
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return m_stream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return m_stream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override bool CanTimeout
        {
            get { return m_stream.CanTimeout; }
        }

        public override void Close()
        {
            m_stream.Close();
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return m_stream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override ObjRef CreateObjRef(Type requestedType)
        {
            return m_stream.CreateObjRef(requestedType);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                m_stream.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return m_stream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            m_stream.EndWrite(asyncResult);
        }

        public override bool Equals(object obj)
        {
            return m_stream.Equals(obj);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return m_stream.FlushAsync(cancellationToken);
        }

        public override int GetHashCode()
        {
            return m_stream.GetHashCode();
        }

        public override object InitializeLifetimeService()
        {
            return m_stream.InitializeLifetimeService();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return m_stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int ReadByte()
        {
            return m_stream.ReadByte();
        }

        public override int ReadTimeout
        {
            get { return m_stream.ReadTimeout; }
            set { m_stream.ReadTimeout = value; }
        }

        public override string ToString()
        {
            return m_stream.ToString();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return m_stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            m_stream.WriteByte(value);
        }

        public override int WriteTimeout
        {
            get { return m_stream.WriteTimeout; }
            set { m_stream.WriteTimeout = value; }
        }
        #endregion

        public bool BigEndian { get; set; }

        public T Read<T>()
        {
            var length = Marshal.SizeOf(typeof(T));

            return Read<T>(length);
        }

        public T Read<T>(int length)
        {
            if (BigEndian)
                throw new NotImplementedException("Cannot perform a Read<T> operation on Big-Endian data.");

            var data = new byte[length];
            var ptr = Marshal.AllocHGlobal(length);

            Read(data, 0, length);
            Marshal.Copy(data, 0, ptr, length);

            var t = (T)Marshal.PtrToStructure(ptr, typeof(T));

            Marshal.FreeHGlobal(ptr);
            return t;
        }

        public void Write<T>(T data)
        {
            var length = Marshal.SizeOf(typeof(T));

            Write<T>(data, length);
        }

        public void Write<T>(T data, int length)
        {
            if (BigEndian)
                throw new NotImplementedException("Cannot perform a Write<T> operation on Big-Endian data.");

            // this might be extremely unsafe to do, but it should work fine
            var buffer = new byte[length];
            var pData = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);

            Marshal.StructureToPtr(data, pData, false);

            Write(buffer, 0, length);
        }
        
        public long Align(int alignment)
        {
            if (alignment == 0)
                return Position;

            return (Position = Memory.Align(Position, alignment));
        }

        #region Read methods
        internal int InternalRead(byte[] buffer, bool checkEndianness = false)
        {
            if (buffer != null)
            {
                var len = Read(buffer, 0, buffer.Length);

                if (checkEndianness)
                {
                    if (BigEndian)
                        Array.Reverse(buffer);
                }

                return len;
            }
            return -1;
        }
        
        public byte[] ReadAllBytes()
        {
            using (var ms = new MemoryStream())
            {
                m_stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public byte[] ReadBytes(int count)
        {
            var buffer = new byte[count];
            Read(buffer, 0, count);
            
            return buffer;
        }

        public char ReadChar()
        {
            return (char)m_stream.ReadByte();
        }

        public char[] ReadChars(int count)
        {
            char[] chars = new char[count];

            for (int i = 0; i < count; i++)
                chars[i] = ReadChar();

            return chars;
        }

        public short ReadInt16()
        {
            byte[] buffer = new byte[sizeof(short)];
            InternalRead(buffer, true);
            
            return BitConverter.ToInt16(buffer, 0);
        }

        public ushort ReadUInt16()
        {
            byte[] buffer = new byte[sizeof(ushort)];
            InternalRead(buffer, true);
            
            return BitConverter.ToUInt16(buffer, 0);
        }

        public int ReadInt32()
        {
            byte[] buffer = new byte[sizeof(int)];
            InternalRead(buffer, true);

            return BitConverter.ToInt32(buffer, 0);
        }

        public uint ReadUInt32()
        {
            byte[] buffer = new byte[sizeof(uint)];
            InternalRead(buffer, true);

            return BitConverter.ToUInt32(buffer, 0);
        }

        public long ReadInt64()
        {
            byte[] buffer = new byte[sizeof(long)];
            InternalRead(buffer, true);

            return BitConverter.ToInt64(buffer, 0);
        }

        public ulong ReadUInt64()
        {
            byte[] buffer = new byte[sizeof(ulong)];
            InternalRead(buffer, true);
            
            return BitConverter.ToUInt64(buffer, 0);
        }

        public float ReadHalf()
        {
            var value = ReadInt16();

            var a = (value & 0x8000) << 16;
            var b = (value & 0x7FFF) << 13;
            var c = (127 - 15) << 23;

            return (float)(a + b + c);
        }

        public double ReadFloat()
        {
            var val = (double)ReadSingle();

            return Math.Round(val, 3);
        }

        public float ReadSingle()
        {
            byte[] buffer = new byte[sizeof(float)];
            InternalRead(buffer, true);

            return BitConverter.ToSingle(buffer, 0);
        }

        public double ReadDouble()
        {
            byte[] buffer = new byte[sizeof(double)];
            InternalRead(buffer, true);

            return BitConverter.ToDouble(buffer, 0);
        }

        public string ReadString()
        {
            string str = "";
            char curChar;

            while ((curChar = ReadChar()) != '\0')
            {
                str += curChar;
            }

            return str;
        }

        public string ReadString(Encoding encoding)
        {
            var size = encoding.GetByteCount("N");

            var idx = -size; // increments each iteration; allows clean breaks
            var len = 1024 * size;
            
            var buffer = new byte[len];
            var result = String.Empty;
            
            do
            {
                // do we need a bigger buffer?
                if ((idx += size) >= len)
                    Array.Resize(ref buffer, len += len);

                // read in the next char
                m_stream.Read(buffer, idx, size);
            } while (encoding.GetString(buffer, idx, size) != "\0");

            if (idx > 0)
                result = encoding.GetString(buffer, 0, idx);

            return result;
        }

        public string ReadString(int length, Encoding encoding)
        {
            return encoding.GetString(ReadBytes(length));
        }

        public string ReadString(int length)
        {
            return ReadString(length, Encoding.UTF8);
        }

        public string ReadUnicodeString(int length)
        {
            return ReadString(length, Encoding.Unicode);
        }
        #endregion

        #region Write methods
        internal void InternalWrite(byte[] buffer, int offset, int length, bool checkEndianness = false)
        {
            if (checkEndianness)
            {
                if (BigEndian)
                    Array.Reverse(buffer);
            }

            Write(buffer, offset, length);
        }
        
        public void WriteByte(int value)
        {
            m_stream.WriteByte((byte)value);
        }

        public void WriteFloat(double value)
        {
            Write((float)value);
        }

        public void Write(byte[] bytes)
        {
            Write(bytes, 0, bytes.Length);
        }
        
        public void Write(byte value)
        {
            InternalWrite(BitConverter.GetBytes(value), 0, sizeof(byte));
        }

        public void Write(char value)
        {
            InternalWrite(BitConverter.GetBytes(value), 0, sizeof(char));
        }

        public void Write(string value)
        {
            Write(value, Encoding.UTF8);
        }

        public void Write(string value, Encoding encoding)
        {
            var buffer = encoding.GetBytes(value);

            InternalWrite(buffer, 0, buffer.Length);
        }

        public void Write(short value)
        {
            InternalWrite(BitConverter.GetBytes(value), 0, sizeof(short), true);
        }

        public void Write(ushort value)
        {
            InternalWrite(BitConverter.GetBytes(value), 0, sizeof(ushort), true);
        }

        public void Write(int value)
        {
            InternalWrite(BitConverter.GetBytes(value), 0, sizeof(int), true);
        }

        public void Write(uint value)
        {
            InternalWrite(BitConverter.GetBytes(value), 0, sizeof(uint), true);
        }

        public void Write(long value)
        {
            InternalWrite(BitConverter.GetBytes(value), 0, sizeof(long), true);
        }

        public void Write(ulong value)
        {
            InternalWrite(BitConverter.GetBytes(value), 0, sizeof(ulong), true);
        }

        public void Write(float value)
        {
            InternalWrite(BitConverter.GetBytes(value), 0, sizeof(float), true);
        }

        public void Write(double value)
        {
            InternalWrite(BitConverter.GetBytes(value), 0, sizeof(double), true);
        }
        #endregion

        public byte[] ToArray()
        {
            if (m_stream is MemoryStream)
                return ((MemoryStream)m_stream).ToArray();

            throw new InvalidOperationException("Cannot use ToArray() on a non-MemoryStream stream!");
        }
        
        public BinaryStream(Stream stream)
        {
            if (stream.GetType() == typeof(BinaryStream))
                throw new InvalidOperationException("Cannot create a new BinaryStream from a BinaryStream!");

            m_stream = stream;
        }

        public BinaryStream(int capacity)
        {
            m_stream = new MemoryStream(capacity);
        }

        public BinaryStream(byte[] buffer)
        {
            m_stream = new MemoryStream(buffer, true);
        }

        public BinaryStream(string filename)
            : this(File.ReadAllBytes(filename))
        {

        }

        public BinaryStream(string filename, FileMode mode, FileAccess access = FileAccess.Read)
        {
            m_stream = new FileStream(filename, mode, access);
        }
    }
}
