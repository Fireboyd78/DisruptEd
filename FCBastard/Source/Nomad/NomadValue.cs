using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Nomad
{
    public class NomadValue : NomadData
    {
        public AttributeData Data;
        
        public override bool IsAttribute => true;
        public override bool IsRml => (Data.Type == DataType.RML);

        public override IEnumerator<NomadData> GetEnumerator()
        {
            throw new InvalidOperationException("Cannot enumerate over a value-type!");
        }

        public bool ToBool()        => Data.ToBool();
        public byte ToByte()        => Data.ToByte();
        public short ToInt16()      => Data.ToInt16();
        public ushort ToUInt16()    => Data.ToUInt16();
        public int ToInt32()        => Data.ToInt32();
        public uint ToUInt32()      => Data.ToUInt32();
        public float ToFloat()      => Data.ToFloat();

        public override string ToString()
        {
            return Data.ToString();
        }
        
        public NomadValue(DataType type)
        {
            Data = new AttributeData(type);
        }

        public NomadValue(DataType type, byte[] buffer)
        {
            Data = new AttributeData(type, buffer);
        }

        public NomadValue(DataType type, string value)
        {
            Data = new AttributeData(type, value);
        }

        public NomadValue(StringId id, DataType type)
            : this(type)
        {
            Id = id;
        }

        public NomadValue(StringId id, DataType type, byte[] buffer)
            : this(type, buffer)
        {
            Id = id;
        }

        public NomadValue(StringId id, DataType type, string value)
            : this(type, value)
        {
            Id = id;
        }
        
        public NomadValue()
            : this(DataType.BinHex)
        { }

        public NomadValue(byte[] buffer)
            : this(DataType.BinHex, buffer)
        { }

        public NomadValue(StringId id)
            : this(id, DataType.BinHex)
        { }

        public NomadValue(StringId id, byte[] buffer)
            : this(id, DataType.BinHex, buffer)
        { }

        public NomadValue(StringId id, string value)
            : this(id, DataType.BinHex, value)
        { }
    }
}