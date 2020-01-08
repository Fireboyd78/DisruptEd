using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Nomad
{
    public struct AttributeData : /*IBinarySerializer,*/ ICacheableObject
    {
        public byte[] Buffer;
        public DataType Type;

        public static implicit operator string(AttributeData data)
        {
            return data.ToString();
        }
        
        public int Size
        {
            get { return (Buffer != null) ? Buffer.Length : 0; }
        }

        public bool IsEmpty
        {
            get { return (Buffer == null) || (Buffer.Length == 0); }
        }

        public bool IsValid
        {
            get { return IsValidDataType(Type); }
        }

        public bool CanBeCached
        {
            get { return (WriteCache.Enabled && (Type != DataType.RML) && (Size > 2)); }
        }

        public bool IsValidDataType(DataType type)
        {
            var typeSize = Utils.GetAttributeTypeSize(type);

            if (typeSize == -1)
                return true;

            return (Buffer != null)
                && (Buffer.Length <= typeSize);
        }

        public bool IsBufferValid_LEGACY()
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

        public string ToStringId()
        {
            var value = ToInt32();
            var hashStr = StringHasher.ResolveHash(value);

            if (hashStr != null)
                return String.Concat("$", hashStr);

            return (value != 0)
                ? ToHexString()
                : String.Empty;
        }

        public string ToHexString()
        {
            return Utils.Bytes2HexString(Buffer);
        }

        private string ToArray()
        {
            var count = BitConverter.ToInt32(Buffer, 0);

            var offset = 4;
            var length = Buffer.Length - offset;

            var size = (length / count);

            if ((size * count) != length)
                throw new InvalidDataException("Not an array!");

            var values = new List<string>();

            for (int i = 0; i < count; i++)
            {
                var buffer = new byte[size];

                System.Buffer.BlockCopy(Buffer, offset + (i * size), buffer, 0, size);

                var value = Utils.Bytes2HexString(buffer);
                values.Add(value);
            }

            return $"[{String.Join(",", values)}]";
        }
        
        public override string ToString()
        {
            if (!IsValid)
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
            case DataType.RML:
            case DataType.String:
                {
                    var result = String.Empty;

                    if (Size > 1)
                    {
                        var len = Utils.CheckStringBuffer(Buffer);

                        if (len < 0)
                        {
                            Debug.WriteLine($"Something's wrong with a string of {Size} bytes: '{result}' ({len})");

                            return ToHexString();
                        }
                        else
                        {
                            result = Encoding.UTF8.GetString(Buffer, 0, len);
                        }
                    }

                    return result;
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
            case DataType.StringId:
                    return ToStringId();

            case DataType.Array:
                    return ToArray();
            }

            return ToHexString();
        }

        public static byte[] Parse(string s)
        {
            var type = DataType.BinHex;

            return Parse(s, ref type);
        }

        public static byte[] Parse(string s, ref DataType type)
        {
            if (s.Length == 0)
            {
                // the following MUST have a null terminator!
                switch (type)
                {
                case DataType.String:
                case DataType.RML:
                    return new byte[1] { 0 };
                }

                switch (type)
                {
                case DataType.StringId:
                case DataType.PathId:
                    type = DataType.BinHex;
                    return BitConverter.GetBytes(-1);
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
                        System.Buffer.BlockCopy(f, 0, buffer, (v * 4), 4);
                    }

                    return buffer;
                }

            case DataType.RML:
                return Utils.GetStringBuffer(s);
            }

            switch (s[0])
            {
            // StringId
            case '$':
                var str = s.Substring(1);
                var hash = StringHasher.GetHash(str);
            
                StringHasher.AddToLookup(str);
            
                type = DataType.StringId;
            
                return BitConverter.GetBytes(hash);
            // BinHex
            case '#':
                {
                    if (Utils.IsHexString(s))
                    {
                        type = DataType.BinHex;

                        return Utils.HexString2Bytes(s);
                    }
                } break;
            // Array
            case '[':
                {
                    // strip the braces
                    var aryStr = s.Substring(1, s.Length - 2);
                    var aryVals = aryStr.Split(',');

                    var count = aryVals.Length;

                    var offset = 0;
                    var size = -1;

                    byte[] buffer = null;

                    for (int i = 0; i < count; i++)
                    {
                        var aryVal = aryVals[i];

                        var value = Utils.HexString2Bytes(aryVal);

                        if (buffer == null)
                        {
                            size = value.Length;
                            buffer = new byte[(count * size) + 4];

                            var cBuf = BitConverter.GetBytes(count);

                            System.Buffer.BlockCopy(cBuf, 0, buffer, 0, 4);
                            offset = 4;
                        }
                        else
                        {
                            if (value.Length != size)
                                throw new InvalidDataException("Array element size mismatch!");
                        }

                        System.Buffer.BlockCopy(value, 0, buffer, offset, size);
                        offset += size;
                    }

                    return buffer;
                }
            }

            if (Utils.IsHexString(s))
                return Utils.HexString2Bytes(s);
            
            // return as string
            type = DataType.String;

            // if it's a StringId, require '$' prefix
            if (type == DataType.StringId)
                throw new InvalidDataException("Malformed StringId -- string must have a prefix!");

            return Utils.GetStringBuffer(s);
        }

        public void Serialize(BinaryStream stream, int baseOffset = 0)
        {
            if (Type == DataType.RML)
                throw new InvalidOperationException("Cannot serialize RML data directly!");

            var ptr = (int)stream.Position;
            var oldSize = Size;
            
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
                                var nD = DescriptorTag.CreateReference(cache.Offset, ReferenceType.Offset);
                                nD.WriteTo(stream, baseOffset);

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
                    var nD = DescriptorTag.Create(Size);
                    nD.WriteTo(stream, baseOffset);

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
            var nD = DescriptorTag.Read(stream, ReferenceType.Offset);

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
            }
        }

        public override int GetHashCode()
        {
            if (Size > 0)
                return (int)Memory.GetCRC32(Buffer);

            return 0;
        }
        
        private void SanityCheck()
        {
            switch (Type)
            {
            case DataType.BinHex:
                {
                    var len = Utils.CheckStringBuffer(Buffer);

                    // likely a string
                    if (len > 2)
                        Type = DataType.String;
                } break;
            case DataType.String:
                {
                    var len = Utils.CheckStringBuffer(Buffer);
                    
                    if (len < 0)
                        Type = DataType.BinHex;
                } break;
            }
        }

        public static AttributeData Read(BinaryStream stream, DataType type)
        {
            var data = new AttributeData(type);

            try
            {
                data.Deserialize(stream);
                data.SanityCheck();
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Error reading attribute data type '{data.Type}' -- {e.Message}", e);
            }

            return data;
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
            Buffer = Parse(value, ref type);
            Type = type;
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
}
