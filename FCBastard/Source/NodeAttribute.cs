using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;

namespace DisruptEd.IO
{
    public struct AttributeData : IBinarySerializer, ICacheableObject
    {
        public byte[] Buffer;
        public DataType Type;

        public int Size
        {
            get { return (Buffer != null) ? Buffer.Length : 0; }
        }

        public bool CanBeCached
        {
            get { return (Size > 2); }
        }

        public bool IsBufferValid()
        {
            // obviously not...
            if (Buffer == null)
                return false;

            var typeSize = Utils.GetAttributeTypeSize(Type);

            // variably-sized, yes it's valid
            if (typeSize == -1)
                return true;

            return (Buffer.Length <= typeSize);
        }

        public void CommitBuffer()
        {
            if (Buffer != null)
                Buffer = Utils.GetAttributeDataMiniBuffer(Buffer, Type);
        }

        private T ConvertTo<T>(Func<byte[], int, T> fnConvert)
        {
            var buffer = Utils.GetAttributeDataBuffer(Buffer, Type);
            return fnConvert(buffer, 0);
        }

        public bool ToBool()
        {
            return (ToByte() == 1);
        }
        
        public byte ToByte()
        {
            return Utils.GetAttributeDataBuffer(Buffer, Type)[0];
        }

        public short ToInt16()
        {
            return ConvertTo(BitConverter.ToInt16);
        }

        public ushort ToUInt16()
        {
            return ConvertTo(BitConverter.ToUInt16);
        }

        public int ToInt32()
        {
            return ConvertTo(BitConverter.ToInt32);
        }

        public uint ToUInt32()
        {
            return ConvertTo(BitConverter.ToUInt32);
        }

        public float ToFloat()
        {
            return ConvertTo(BitConverter.ToSingle);
        }

        public string ToHashString()
        {
            var value = ToInt32();
            var hashStr = StringHasher.ResolveHash(value);

            if (hashStr != null)
                return $"$({hashStr})";

            return (value != 0) ? $"${value:X}" : String.Empty;
        }

        public string ToHexString()
        {
            return Utils.Bytes2HexString(Buffer);
        }

        public override string ToString()
        {
            if (!IsBufferValid())
                return Utils.GetAttributeTypeDefault(Type);

            // neatly retrieve the type :)
            switch (Type)
            {
            case DataType.Bool:
            case DataType.Byte:
                {
                    var value = ToByte();
                    return value.ToString();
                }
            case DataType.Int16:
                {
                    var value = ToInt16();
                    return value.ToString();
                }
            case DataType.UInt16:
                {
                    var value = ToUInt16();
                    return value.ToString();
                }
            case DataType.Int32:
                {
                    var value = ToInt32();
                    return value.ToString();
                }
            case DataType.UInt32:
                {
                    var value = ToUInt32();
                    return value.ToString();
                }
            case DataType.Float:
                {
                    var value = ToFloat();
                    return value.ToString("0.0###########");
                }
            case DataType.String:
                {
                    var buffer = Utils.GetAttributeDataBuffer(Buffer, Type);
                    var value = "";

                    for (int idx = 0; idx < (buffer.Length - 1); idx++)
                    {
                        var c = (char)buffer[idx];

                        if (c != 0)
                            value += c;
                    }

                    return value;
                }
            case DataType.Vector2:
            case DataType.Vector3:
            case DataType.Vector4:
                {
                    var buffer = Utils.GetAttributeDataBuffer(Buffer, Type);

                    var x = BitConverter.ToSingle(buffer, 0);
                    var y = BitConverter.ToSingle(buffer, 4);

                    switch (Type)
                    {
                    case DataType.Vector3:
                    case DataType.Vector4:
                        {
                            var z = BitConverter.ToSingle(buffer, 8);

                            if (Type == DataType.Vector4)
                            {
                                var w = BitConverter.ToSingle(buffer, 12);

                                return Utils.FormatVector(x, y, z, w);
                            }
                            else
                            {
                                return Utils.FormatVector(x, y, z);
                            }
                        }
                    }

                    return Utils.FormatVector(x, y);
                }
            case DataType.StringHash:
                {
                    return ToHashString();
                }
            }

            return ToHexString();
        }

        public static byte[] Parse(string s, DataType type = DataType.BinHex)
        {
            if (s.Length == 0)
            {
                // the following MUST have a null terminator!
                // either due to a bug or 'feature' :P
                switch (type)
                {
                case DataType.String:
                case DataType.StringHash:
                    return new byte[1] { 0 };
                }

                // for others, the resulting buffer can be empty
                return new byte[0];
            }

            switch (type)
            {
            case DataType.Bool:
            case DataType.Byte:
                {
                    var value = byte.Parse(s);
                    return new byte[1] { value };
                }
            case DataType.Int16:
                {
                    var value = short.Parse(s);
                    return BitConverter.GetBytes(value);
                }
            case DataType.UInt16:
                {
                    var value = ushort.Parse(s);
                    return BitConverter.GetBytes(value);
                }
            case DataType.Int32:
                {
                    var value = int.Parse(s);
                    return BitConverter.GetBytes(value);
                }
            case DataType.UInt32:
                {
                    var value = uint.Parse(s);
                    return BitConverter.GetBytes(value);
                }
            case DataType.Float:
                {
                    var value = float.Parse(s);
                    return BitConverter.GetBytes(value);
                }
            case DataType.Vector2:
            case DataType.Vector3:
            case DataType.Vector4:
                {
                    var vals = s.Split(',');

                    if (vals.Length < 2)
                        throw new InvalidOperationException($"Invalid vector value '{s}'");

                    var fVals = new float[4];

                    for (int i = 0; i < vals.Length; i++)
                        fVals[i] = float.Parse(vals[i]);

                    var nVals = 0;

                    if (type == DataType.Vector2)
                        nVals = 2;
                    if (type == DataType.Vector3)
                        nVals = 3;
                    if (type == DataType.Vector4)
                        nVals = 4;

                    var buffer = new byte[nVals * 4];

                    for (int v = 0; v < nVals; v++)
                    {
                        var f = BitConverter.GetBytes(fVals[v]);
                        Array.Copy(f, 0, buffer, (v * 4), 4);
                    }

                    return buffer;
                }

            case DataType.String:
                return Utils.GetStringBuffer(s);

            case DataType.StringHash:
                {
                    var sym = s[0];
                    var isHash = (sym == '$');

                    if (isHash)
                    {
                        var str = s.Substring(1);
                        var isStrHash = ((str.First() == '(') && (str.Last() == ')'));

                        var hash = -1;

                        if (isStrHash)
                        {
                            // weird, but this is how we remove the parenthesis
                            var val = str.Substring(1, str.Length - 2);

                            hash = StringHasher.GetHash(val);
                        }
                        else
                        {
                            hash = int.Parse(str, NumberStyles.HexNumber);
                        }

                        return BitConverter.GetBytes(hash);
                    }
                    else
                    {
                        // get value as string
                        return Utils.GetStringBuffer(s);
                    }
                }
            }

            return Utils.HexString2Bytes(s);
        }

        public void Serialize(BinaryStream stream)
        {
            var ptr = (int)stream.Position;
            var oldSize = Size;

            // make sure buffer is proper size
            CommitBuffer();

            if (Size > 0)
            {
                var writeData = true;

                if (CanBeCached)
                {
                    if (WriteCache.IsCached(this))
                    {
                        var cache = WriteCache.GetData(this);

                        if (cache.Size == Size)
                        {
                            stream.Position = (cache.Offset + 1);
                            var buf = stream.ReadBytes(cache.Size);
                            
                            var key = Memory.GetCRC32(Buffer);
                            var bufKey = Memory.GetCRC32(buf);

                            stream.Position = ptr;

                            // slow as fuck, but there's no room for error
                            if (key == bufKey)
                            {
                                var nD = NodeDescriptor.CreateReference(cache.Offset, ReferenceType.Offset);
                                nD.WriteTo(stream);

                                writeData = false;
                            }
                        }
                    }
                    else
                    {
                        WriteCache.Cache(ptr, this);
                    }
                }

                if (writeData)
                {
                    var nD = NodeDescriptor.Create(Size);
                    nD.WriteTo(stream);

                    stream.Write(Buffer);
                }
            }
            else
            {
                // nothing to write!
                stream.WriteByte(0);
            }
        }

        public void Deserialize(BinaryStream stream)
        {
            var ptr = (int)stream.Position;
            var nD = NodeDescriptor.Read(stream, ReferenceType.Offset);

            if (nD.IsOffset)
            {
                stream.Position = nD.Value;
                Deserialize(stream);

                // move past offset
                stream.Position = (ptr + nD.Size);
            }
            else
            {
                var size = nD.Value;

                Buffer = new byte[size];
                stream.Read(Buffer, 0, size);
                
                if (Type != DataType.BinHex)
                {
                    var typeSize = Utils.GetAttributeTypeSize(Type);

                    if (typeSize != -1)
                    {
                        if (Type == DataType.StringHash)
                        {
                            var isStr = (size > 1) || ((size == 1) && Buffer[0] == 0);

                            // string possible
                            if (isStr && (size != 4))
                                Type = DataType.String;
                        }
                        else if (size  > typeSize)
                        {
                            throw new InvalidOperationException($"Data type '{Type.ToString()}' buffer has overflowed (size: 0x{size:X})");
                        }
                    }
                }
            }
        }
        
        public override int GetHashCode()
        {
            // where the hell do I put this
            CommitBuffer();

            if (Size > 0)
                return (int)Memory.GetCRC32(Buffer);

            return 0;
        }

        public AttributeData(DataType type)
        {
            Type = type;
            Buffer = Utils.GetAttributeDataBuffer(null, type);
        }

        public AttributeData(DataType type, byte[] buffer)
        {
            Type = type;
            Buffer = buffer;
        }

        public AttributeData(DataType type, string value)
        {
            Type = type;
            Buffer = Parse(value, type);

            var size = Buffer.Length;

            if (Type == DataType.StringHash)
            {
                var isStr = (size > 1) || ((size == 1) && Buffer[0] == 0);

                // definitely a string hash
                if ((size == 4) && (Buffer[3] != 0))
                    isStr = false;

                // change to string if necessary
                if (isStr)
                    Type = DataType.String;
            }
        }

        public AttributeData(byte[] buffer)
        {
            Type = DataType.BinHex;
            Buffer = buffer;
        }

        public AttributeData(string value)
        {
            Type = DataType.BinHex;
            Buffer = Utils.HexString2Bytes(value);
        }
    }

    public class NodeAttribute : Node
    {
        public AttributeData Data;
        
        public string Class { get; set; }

        public string FullName
        {
            get
            {
                if (String.IsNullOrEmpty(Class))
                    return Name;

                return $"{Class}.{Name}";
            }
        }
        
        public void Serialize(XmlElement xml)
        {
            xml.SetAttribute(Name, Data.ToString());
        }

        public void Deserialize(XmlAttribute xml)
        {
            var name = xml.Name;

            if (name[0] == '_')
            {
                Hash = int.Parse(name.Substring(1), NumberStyles.HexNumber);
            }
            else
            {
                // known attribute :)
                Name = name;
            }
            
            var type = AttributeTypes.GetType(Hash);

            // try resolving the full name, e.g. 'Class.bProperty'
            var fullHash = StringHasher.GetHash(FullName);

            if (fullHash != Hash)
            {
                if (AttributeTypes.IsTypeKnown(fullHash))
                    type = AttributeTypes.GetType(fullHash);
            }
            
            Data = new AttributeData(type, xml.Value);

            // looks to be part of the spec :/
            //if (Data.Type != type)
            //    Debug.WriteLine($"Attribute '{FullName}' was created as a '{type.ToString()}' but was actually a '{Data.Type.ToString()}'!");
        }

        public void Serialize(BinaryStream stream, bool writeHash)
        {
            if (writeHash)
            {
                stream.Write(Hash);
            }
            else
            {
                Serialize(stream);
            }
        }

        public override void Serialize(BinaryStream stream)
        {
            Offset = (int)stream.Position;
            Data.Serialize(stream);
        }

        public override void Deserialize(BinaryStream stream)
        {
            Offset = (int)stream.Position;

            try
            {    
                Data.Deserialize(stream);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Attribute '{Name}' read error -- {e.Message}", e);
            }

            // pretty sure this is garbage
            if (Data.Type == DataType.BinHex)
            {
                if (Data.IsBufferValid())
                {
                    var guess = Utils.GetAttributeTypeBestGuess(Data);

                    if (guess != DataType.BinHex)
                    {
                        //Debug.WriteLine($"[INFO] Attribute type for '{Name}' (hash={Hash:X8}) in '{Class}' may be '{guess.ToString()}'");
                        Debug.WriteLine($"<!-- GUESS: --><Attribute Name=\"{FullName}\" Type=\"{guess.ToString()}\" />");
                    }
                }
                else
                {
                    // really really slow
                    Debug.WriteLine($"[INFO] Attribute type for '{Name}' (hash={Hash:X8}) in '{Class}' is unknown.");
                }
            }
            else
            {
                if (Utils.IsAttributeBufferStrangeSize(Data))
                    Debug.WriteLine($"Attribute '{Name}' in '{Class}' is defined as a '{Data.Type}' and has a strange size of {Data.Buffer.Length} byte(s).");

                if (Data.Type == DataType.Byte)
                {
                    if ((Data.Buffer.Length == 1) && (Data.Buffer[0] == 0))
                        throw new InvalidOperationException($"VERY BAD: Attribute '{Name}' (hash:{Hash:X8}) defined as a Byte in class '{Class}' but is actually a String.\r\nThis will BREAK the exporter, so please update this attribute immediately!");
                }
            }
        }

        public void Deserialize(BinaryStream stream, bool readHash)
        {
            if (readHash)
            {
                var hash = stream.ReadInt32();
                
                var name = StringHasher.ResolveHash(hash);
                var type = AttributeTypes.GetType(hash);

                // cannot be null or contain spaces
                var nameResolved = ((name != null) && !name.Contains(" "));
                
                if (nameResolved)
                {
                    Name = name;
                }
                else
                {
                    Hash = hash;
                }

                // try resolving the full name, e.g. 'Class.bProperty'
                var fullHash = StringHasher.GetHash(FullName);

                if (fullHash != hash)
                {
                    if (AttributeTypes.IsTypeKnown(fullHash))
                        type = AttributeTypes.GetType(fullHash);
                }
                
                Data = new AttributeData(type);
            }
            else
            {
                Deserialize(stream);
            }
        }

        public NodeAttribute(BinaryStream stream, string className)
        {
            Class = className;
            Deserialize(stream, true);
        }

        public NodeAttribute(XmlAttribute elem)
        {
            Class = elem.OwnerElement.Name;
            Deserialize(elem);
        }

        public NodeAttribute(string name)
            : this(name, DataType.BinHex) { }

        public NodeAttribute(int hash)
            : this(hash, DataType.BinHex) { }

        public NodeAttribute(int hash, string name)
        : this(hash, name, DataType.BinHex) { }

        public NodeAttribute(string name, DataType type)
            : base(name)
        {
            Data = new AttributeData(type);
        }

        public NodeAttribute(int hash, DataType type)
            : base(hash)
        {
            Data = new AttributeData(type);
        }

        public NodeAttribute(int hash, string name, DataType type)
            : base(hash, name)
        {
            Data = new AttributeData(type);
        }
    }
}
