using System;

namespace Nomad
{
    public struct DescriptorTag
    {
        int m_value;
        DescriptorType m_type;
        ReferenceType m_refType;

        public static implicit operator int(DescriptorTag desc)
        {
            return desc.m_value;
        }

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
        
        public void WriteTo(BinaryStream stream, int baseOffset = 0)
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
                        var ptr = (int)stream.Position + baseOffset;
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
        
        public static DescriptorTag Read(BinaryStream stream, ReferenceType refType = ReferenceType.None)
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

            return new DescriptorTag(value, type, refType);
        }

        public static DescriptorTag Create(int value)
        {
            var type = DescriptorType.None;

            if (value >= 254u)
                type = DescriptorType.BigValue;

            if (GlobalFlags.HasFlag(DescriptorFlags.Use24Bit))
            {
                if ((value & 0xFFFFFF) != value)
                    throw new InvalidOperationException($"Descriptor value '{value}' too large, cannot fit into 24-bits!");
            }

            return new DescriptorTag(value, type, ReferenceType.None);
        }

        public static DescriptorTag CreateReference(int value, ReferenceType refType)
        {
            if (refType == ReferenceType.None)
                throw new InvalidOperationException("ID:10T error -- why the fuck are you creating a reference with no type?!");

            if (GlobalFlags.HasFlag(DescriptorFlags.Use24Bit))
            {
                if ((value & 0xFFFFFF) != value)
                    throw new InvalidOperationException($"Descriptor offset '{value}' too large, cannot fit into 24-bits!");
            }

            return new DescriptorTag(value, DescriptorType.Reference, refType);
        }
        
        private DescriptorTag(int value, DescriptorType type, ReferenceType refType)
        {
            m_value = value;
            m_type = type;
            m_refType = refType;
        }
    }
}
