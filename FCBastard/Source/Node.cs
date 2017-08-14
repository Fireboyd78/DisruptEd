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

    public interface IResourceFile
    {
        void LoadBinary(string filename);
        void SaveBinary(string filename);

        void LoadXml(string filename);
        void SaveXml(string fileanme);
    }

    [Flags]
    public enum DescriptorFlags
    {
        None = 0,
        Use24Bit = 1,
    }

    public enum DescriptorType
    {
        None = -1,

        BigValue,
        Reference,
    }
    
    public enum ReferenceType
    {
        None,

        Index,
        Offset,
    }

    public struct NodeDescriptor
    {
        int m_value;
        DescriptorType m_type;
        ReferenceType m_refType;

        public static DescriptorFlags GlobalFlags { get; set; }

        public static DescriptorType GetDescriptorType(int code)
        {
            switch (code)
            {
            case 254: return DescriptorType.Reference;
            case 255: return DescriptorType.BigValue;
            }

            return DescriptorType.None;
        }

        public int Value
        {
            get { return m_value; }
        }

        public DescriptorType Type
        {
            get { return m_type; }
        }

        public ReferenceType ReferenceType
        {
            get { return m_refType; }
        }

        public int Size
        {
            get
            {
                switch (Type)
                {
                case DescriptorType.None:
                    return 1;
                case DescriptorType.BigValue:
                case DescriptorType.Reference:
                    return (GlobalFlags.HasFlag(DescriptorFlags.Use24Bit) ? 4 : 5);
                }

                throw new InvalidOperationException("Unknown descriptor type, cannot determine size!");
            }
        }

        public bool IsOffset
        {
            get
            {
                return (m_type == DescriptorType.Reference) 
                    && (ReferenceType == ReferenceType.Offset);
            }
        }

        public bool IsIndex
        {
            get
            {
                return (m_type == DescriptorType.Reference)
                    && (ReferenceType == ReferenceType.Index);
            }
        }
        
        public void WriteTo(BinaryStream stream)
        {
            var value = Value;

            switch (Type)
            {
            case DescriptorType.None:
                stream.WriteByte(value);
                break;
            case DescriptorType.BigValue:
            case DescriptorType.Reference:
                {
                    var code = (byte)~Type;
                    
                    if (ReferenceType == ReferenceType.Offset)
                    {
                        var ptr = (int)stream.Position;
                        var offset = (ptr - value);

                        if (offset < 0)
                            throw new InvalidOperationException("Cannot write a forward-offset!");

                        value = offset;
                    }

                    if (GlobalFlags.HasFlag(DescriptorFlags.Use24Bit))
                    {
                        if ((value & 0xFFFFFF) != value)
                            throw new InvalidOperationException($"Descriptor value '{value}' too large, cannot fit into 24-bits!");

                        value <<= 8;
                        value |= code;

                        stream.Write(value);
                    }
                    else
                    {
                        stream.WriteByte(code);
                        stream.Write(value);
                    }
                } break;
            }
        }
        
        public static NodeDescriptor Read(BinaryStream stream, ReferenceType refType = ReferenceType.None)
        {
            var ptr = (int)stream.Position;

            var value = stream.ReadByte();
            var type = GetDescriptorType(value);

            if ((type == DescriptorType.Reference) && (refType == ReferenceType.None))
                throw new InvalidOperationException("ID:10T error while reading a descriptor -- consumed a reference with no type defined!");

            var isOffset = (type == DescriptorType.Reference)
                        && (refType == ReferenceType.Offset);
            
            if (type != DescriptorType.None)
            {
                if (GlobalFlags.HasFlag(DescriptorFlags.Use24Bit))
                {
                    // move back
                    stream.Position -= 1;

                    // read in value without control code
                    value = (int)(stream.ReadUInt32() >> 8);
                }
                else
                {
                    value = stream.ReadInt32();
                }

                if (isOffset)
                {
                    // make offset absolute
                    value = (ptr - value);
                }
            }

            return new NodeDescriptor(value, type, refType);
        }

        public static NodeDescriptor Create(int value)
        {
            var type = DescriptorType.None;

            if (value >= 254)
                type = DescriptorType.BigValue;

            if (GlobalFlags.HasFlag(DescriptorFlags.Use24Bit))
            {
                if ((value & 0xFFFFFF) != value)
                    throw new InvalidOperationException($"Descriptor value '{value}' too large, cannot fit into 24-bits!");
            }

            return new NodeDescriptor(value, type, ReferenceType.None);
        }

        public static NodeDescriptor CreateReference(int value, ReferenceType refType)
        {
            if (refType == ReferenceType.None)
                throw new InvalidOperationException("ID:10T error -- why the fuck are you creating a reference with no type?!");

            if (GlobalFlags.HasFlag(DescriptorFlags.Use24Bit))
            {
                if ((value & 0xFFFFFF) != value)
                    throw new InvalidOperationException($"Descriptor offset '{value}' too large, cannot fit into 24-bits!");
            }

            return new NodeDescriptor(value, DescriptorType.Reference, refType);
        }
        
        private NodeDescriptor(int value, DescriptorType type, ReferenceType refType)
        {
            m_value = value;
            m_type = type;
            m_refType = refType;
        }
    }

    public interface IGetAttributes<T>
        where T : NodeAttribute
    {
        List<T> Attributes { get; }
    }

    public interface IGetChildren<T>
        where T : Node
    {
        List<T> Children { get; }
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
