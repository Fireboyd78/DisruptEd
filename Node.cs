using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DisruptEd.IO
{
    public interface ISerializer<T>
    {
        void Serialize(T input);
        void Deserialize(T output);
    }

    public interface IBinarySerializer : ISerializer<BinaryStream> { }
    
    public struct NodeDescriptor : IBinarySerializer
    {
        public enum ControlCodeType
        {
            BigValue,
            Offset,

            None = -1,
        }
        
        public int Value { get; set; }

        public bool IsOffset { get; set; }

        public int Size
        {
            get
            {
                // offsets are always 4-bytes long
                if (IsOffset)
                    return 4;

                return (Value < 254) ? 1 : 4;
            }
        }

        public ControlCodeType ControlCode
        {
            get
            {
                if (IsOffset)
                    return ControlCodeType.Offset;

                return (Value >= 254) ? ControlCodeType.BigValue : ControlCodeType.None;
            }
        }

        private ControlCodeType GetControlCodeType(int code)
        {
            switch (code)
            {
            case 254: return ControlCodeType.Offset;
            case 255: return ControlCodeType.BigValue;
            }

            return ControlCodeType.None;
        }
        
        public void Serialize(BinaryStream stream)
        {
            switch (Size)
            {
            case 1:
                stream.WriteByte(Value);
                break;
            case 4:
                {
                    // value is either an offset or count >= 254
                    int value = (Value & 0xFFFFFF);

                    if (value != Value)
                        throw new InvalidOperationException("Node descriptor value too large!");

                    if (IsOffset)
                    {
                        var ptr = (int)stream.Position;
                        var offset = (ptr - value);

                        if (offset < 0)
                            throw new InvalidOperationException("Cannot write a forward-offset!");

                        value = offset;
                    }

                    // insert the control code
                    value <<= 8;
                    value |= (byte)~ControlCode;
                    
                    stream.Write(value);
                } break;
            }
        }

        public void Deserialize(BinaryStream stream)
        {
            var ptr = (int)stream.Position;

            var n = stream.ReadByte();
            var code = GetControlCodeType(n);

            if (code != ControlCodeType.None)
            {
                // move back
                stream.Position -= 1;

                // read in value without control code
                n = (int)(stream.ReadUInt32() >> 8);

                if (code == ControlCodeType.Offset)
                {
                    IsOffset = true;

                    // make offset absolute
                    n = (ptr - n);
                }
            }
            
            Value = n;
        }
        
        public NodeDescriptor(int value, bool offset)
        {
            Value = value;
            IsOffset = offset;
        }

        public NodeDescriptor(BinaryStream bs)
            : this()
        {
            Deserialize(bs);
        }
    }

    public abstract class Node : IBinarySerializer
    {
        private string m_name;
        private int m_hash;

        internal int Offset { get; set; }

        public string Name
        {
            get { return m_name; }
            set
            {
                m_name = value;
                m_hash = (m_name != null) ? StringHasher.GetHash(m_name) : 0;
            }
        }

        public int Hash
        {
            get { return m_hash; }
            set
            {
                m_hash = value;
                m_name = $"_{m_hash:X8}";
            }
        }
        
        public abstract void Serialize(BinaryStream stream);
        public abstract void Serialize(XmlElement xml);

        public virtual void Serialize(XmlDocument xml)
        {
            var elem = xml.CreateElement(Name);
            Serialize(elem);

            xml.AppendChild(elem);
        }

        public abstract void Deserialize(BinaryStream stream);

        public override string ToString()
        {
            return (m_name != null) ? m_name : String.Empty;
        }

        protected Node()
        {
        }

        protected Node(string name)
        {
            Name = name;
        }

        protected Node(int hash)
        {
            Hash = hash;
        }

        protected Node(int hash, string name)
        {
            m_name = name;
            m_hash = hash;
        }
    }
}
