using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;

namespace DisruptEd.IO
{
    public struct AttributeData : IBinarySerializer
    {
        public byte[] Buffer;
        public DataType Type;

        public int Size
        {
            get { return (Buffer != null) ? Buffer.Length : 0; }
        }

        public bool CanBeCached
        {
            get { return (Size > 4); }
        }

        public bool IsBufferValid()
        {
            // obviously not...
            if (Buffer == null)
                return false;

            var typeSize = Utils.GetAttributeTypeSize(Type);

            if (typeSize == -1)
                return (Buffer.Length != 0);

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

            return (hashStr != null) ? $"$({hashStr})" : $"_{value:X8}";
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
                    return value.ToString();
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

                                return $"{x},{y},{z},{w}";
                            }
                            else
                            {
                                return $"{x},{y},{z}";
                            }
                        }
                    }

                    return $"{x},{y}";
                }
            case DataType.StringHash:
                {
                    return ToHashString();
                }
            }

            return ToHexString();
        }
        
        public void Serialize(BinaryStream stream)
        {
            var ptr = (int)stream.Position;

            if (Size > 0)
            {
                var isCached = false;

                if (CanBeCached)
                {
                    if (WriteCache.IsCached(this))
                    {
                        isCached = true;
                        
                        var cache = WriteCache.GetData(this);
                        var nD = new NodeDescriptor(cache.Offset, true);

                        nD.Serialize(stream);
                    }
                    else
                    {
                        WriteCache.Cache(ptr, this);
                    }
                }

                if (!isCached)
                {
                    var nD = new NodeDescriptor(Size, false);

                    nD.Serialize(stream);
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
            var nD = new NodeDescriptor(stream);

            if (nD.IsOffset)
            {
                stream.Position = nD.Value;
                Deserialize(stream);

                // move past offset
                stream.Position = (ptr + 4);
            }
            else
            {
                var size = nD.Value;

                Buffer = new byte[size];
                stream.Read(Buffer, 0, size);

                if (Type != DataType.BinHex)
                {
                    var typeSize = Utils.GetAttributeTypeSize(Type);

                    if ((typeSize != -1) && (size > typeSize))
                    {
                        if (Type == DataType.StringHash)
                        {
                            // is this literally a part of the spec?!
                            Type = DataType.String;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Data type '{Type.ToString()}' buffer has overflowed (size: 0x{size:X})");
                        }
                    }
                }
            }
        }

        public bool Equals(AttributeData data)
        {
            return (GetHashCode() == data.GetHashCode());
        }

        public override bool Equals(object obj)
        {
            var objType = obj.GetType();

            if (objType == typeof(AttributeData))
                return Equals((AttributeData)obj);

            return false;
        }

        public override int GetHashCode()
        {
            if (Buffer != null)
            {
                var size = Buffer.Length;
                var crcKey = 0xFFFFFFFF;

                if (size != 0)
                    crcKey &= (uint)((~(int)Type ^ size) | size);

                return (int)Memory.GetCRC32(Buffer, crcKey);
            }

            return base.GetHashCode();
        }

        public AttributeData(byte[] buffer)
        {
            Type = DataType.BinHex;
            Buffer = buffer;
        }

        public AttributeData(DataType type)
        {
            Type = type;
            Buffer = Utils.GetAttributeDataBuffer(null, type);
        }

        public AttributeData(DataType type, byte[] buffer)
        {
            Buffer = buffer;
            Type = type;
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
        
        public override void Serialize(BinaryStream stream)
        {
            Offset = (int)stream.Position;
            Data.Serialize(stream);
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
        
        public override void Serialize(XmlElement xml)
        {
            xml.SetAttribute(Name, Data.ToString());
        }

        public override void Serialize(XmlDocument xml)
        {
            throw new InvalidOperationException("Node attributes cannot be serialized to an XmlDocument.");
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
            if ((Data.Type == DataType.BinHex) && !AttributeTypes.IsTypeKnown(Hash))
            {
                if (Data.IsBufferValid())
                {
                    var guess = Utils.GetAttributeTypeBestGuess(Data);

                    if (guess != DataType.BinHex)
                        Debug.WriteLine($"Attribute type for '{Name}' (hash={Hash:X8}) in '{Class}' may be '{guess.ToString()}'");
                }
                else
                {
                    // really really slow
                    Debug.WriteLine($"Attribute type for '{Name}' (hash={Hash:X8}) in '{Class}' is unknown.");
                }
            }
            else
            {
                if (Utils.IsAttributeBufferStrangeSize(Data))
                    Debug.WriteLine($"Attribute '{Name}' in '{Class}' is defined as a '{Data.Type}' and has a strange size of {Data.Buffer.Length} byte(s).");
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

        public NodeAttribute(BinaryStream stream)
        {
            Deserialize(stream, true);
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
