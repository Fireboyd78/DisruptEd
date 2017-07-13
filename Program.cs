using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Xml;

namespace Disrupt.FCBastard
{
    using HashLookup = Dictionary<int, string>;
    using TypeLookup = Dictionary<int, AttributeType>;

    public enum AttributeType
    {
        BinHex,

        Bool,

        Byte,

        Int16,
        Int32,

        UInt16,
        UInt32,

        Float,

        String,
        StringHash,

        Vector2,
        Vector3,
        Vector4,
    }

    public static class StringHasher
    {
        static HashLookup m_lookup = new HashLookup();
        
        public static void AddToLookup(int hash, string value)
        {
            if (!m_lookup.ContainsKey(hash))
                m_lookup.Add(hash, value);
        }

        public static void AddToLookup(string value)
        {
            var hash = GetHash(value);
            AddToLookup(hash, value);
        }

        public static int GetHash(string value)
        {
            if (value == null)
                return 0;

            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = (int)Memory.GetCRC32(bytes);

            return hash;
        }

        public static bool CanResolveHash(int hash)
        {
            return m_lookup.ContainsKey(hash);
        }

        public static string ResolveHash(int hash)
        {
            if (CanResolveHash(hash))
                return m_lookup[hash];

            return null;
        }

        public static string GetHashString(int hash)
        {
            if (CanResolveHash(hash))
                return m_lookup[hash];

            return $"_{hash:X8}";
        }

        public static void AddLookupsFile(string lookupFile)
        {
            var lines = File.ReadAllLines(lookupFile);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // skip empty lines
                if (line.Length == 0)
                    continue;

                // skip first line if it's a comment
                if ((i == 0) && line[0] == '#')
                    continue;

                AddToLookup(line);
            }       
        }
    }
    
    public static class Utils
    {
        public static string Bytes2HexString(byte[] bytes)
        {
            var str = "";

            for (int i = 0; i < bytes.Length; i++)
            {
                str += $"{bytes[i]:X2}";

                if ((i + 1) != bytes.Length)
                    str += " ";
            }

            return str;
        }

        public static int GetTotalNumberOfNodes(NodeClass node)
        {
            var nChildren = 0;

            if ((node != null) && (node.Children.Count > 0))
            {
                foreach (var subNode in node.Children)
                {
                    nChildren += 1;

                    if (subNode.Children.Count > 0)
                        nChildren += GetTotalNumberOfNodes(subNode);
                }
            }

            return nChildren;
        }

        public static int GetTotalNumberOfAttributes(NodeClass node)
        {
            var nAttributes = 0;

            if (node != null)
            {
                nAttributes = node.Attributes.Count;

                if (node.Children.Count > 0)
                {
                    foreach (var subNode in node.Children)
                        nAttributes += GetTotalNumberOfAttributes(subNode);
                }
            }

            return nAttributes;
        }

        public static bool IsAttributeBufferStrangeSize(AttributeData data)
        {
            var fixedSize = false;
            var size = (data.Buffer != null) ? data.Buffer.Length : 0;

            var zeroOrFullSize = false;

            switch (data.Type)
            {
            // we don't know -- variable-sized buffer :(
            case AttributeType.BinHex:
            case AttributeType.String:
                return false;

            case AttributeType.Float:
                zeroOrFullSize = true;
                break;
                
            case AttributeType.Vector2:
            case AttributeType.Vector3:
            case AttributeType.Vector4:
                fixedSize = true;
                break;
            }
            
            var typeSize = GetAttributeTypeSize(data.Type);

            // if it's a fixed-size type, it must be
            if (fixedSize)
                return ((size != 0) && (size != typeSize));
            
            // zero is never strange!
            if (size == 0)
                return false;

            // biggest type should be 8-bytes long
            // so at this point, it's very strange!
            if ((size & 0xF) != size)
                return true;
            
            var isStrange = ((typeSize % size) == 1);

            // type can either be zero or its full-size
            if (zeroOrFullSize && (size != typeSize))
                isStrange = true;

            return isStrange;
        }

        public static AttributeType GetAttributeTypeBestGuess(AttributeData data)
        {
            var size = (data.Buffer != null) ? data.Buffer.Length : 0;
            return GetAttributeTypeBestGuess(size);
        }

        public static AttributeType GetAttributeTypeBestGuess(int size)
        {
            switch (size)
            {
            case 1:
                return AttributeType.Byte;
            case 2:
                return AttributeType.Int16;
            case 4:
                return AttributeType.Int32;
            case 12:
                return AttributeType.Vector3;
            case 16:
                return AttributeType.Vector4;
            }

            return AttributeType.BinHex;
        }

        // tries to minify the buffer
        public static byte[] GetAttributeDataMiniBuffer(byte[] buffer, AttributeType type)
        {
            // don't even think about it!
            if (type == AttributeType.BinHex)
                return buffer;

            var empty = new byte[0];

            var maxSize = GetAttributeTypeSize(type);

            // you're on your own, dude
            if ((maxSize != -1) && (buffer.Length > maxSize))
                return buffer;

            var attrBuf = GetAttributeDataBuffer(buffer, type);
            var value = 0uL;

            switch (type)
            {
            case AttributeType.Bool:
            case AttributeType.Byte:
                value = attrBuf[0];
                break;

            case AttributeType.Int16:
            case AttributeType.UInt16:
                value = BitConverter.ToUInt16(attrBuf, 0);
                break;

            case AttributeType.Int32:
            case AttributeType.UInt32:
            case AttributeType.StringHash:
                value = BitConverter.ToUInt32(attrBuf, 0);
                break;

            /* these types can only be minified if they're actually zero/empty */
            case AttributeType.String:
                {
                    var str = Encoding.UTF8.GetString(attrBuf);

                    if (String.IsNullOrEmpty(str))
                        return empty;

                    return attrBuf;
                }
            case AttributeType.Float:
                var fVal = BitConverter.ToSingle(attrBuf, 0);

                if (fVal == 0.0f)
                    return empty;

                return attrBuf;

            /* vectors cannot be minified :( */
            case AttributeType.Vector2:
            case AttributeType.Vector3:
            case AttributeType.Vector4:
                return buffer;
            }

            // now that's what I call small :)
            if (value == 0)
                return empty;

#if ALLOW_MINI_SIZE_DATA
            var miniSize = 8;

            // try to make the integer as small as possible
            if ((value & 0xFFFFFFFF) == value)
                miniSize -= 4;
            if ((value & 0xFFFF) == value)
                miniSize -= 2;
            if ((value & 0xFF) == value)
                miniSize -= 1;

            var miniBuf = new byte[miniSize];

            Array.Copy(attrBuf, miniBuf, miniSize);
            return miniBuf;
#else
            return attrBuf;
#endif
        }

        public static int GetAttributeTypeSize(AttributeType type)
        {
            switch (type)
            {
            case AttributeType.Bool:
            case AttributeType.Byte:
                return 1;

            case AttributeType.Int16:
            case AttributeType.UInt16:
                return 2;

            case AttributeType.Int32:
            case AttributeType.UInt32:
            case AttributeType.Float:
            case AttributeType.StringHash:
                return 4;

            /* shared with the other form hash not yet implemented */
            case AttributeType.Vector2:
                return 8;

            case AttributeType.Vector3:
                return 12;

            case AttributeType.Vector4:
                return 16;
            }

            // flexible size
            return -1;
        }

        public static string GetAttributeTypeDefault(AttributeType type)
        {
            switch (type)
            {
            case AttributeType.Bool:
            case AttributeType.Byte:
            case AttributeType.Int16:
            case AttributeType.UInt16:
            case AttributeType.Int32:
            case AttributeType.UInt32:
                return "0";

            case AttributeType.BinHex:
            case AttributeType.String:
            case AttributeType.StringHash:
                return "";

            case AttributeType.Vector2:
            case AttributeType.Vector3:
            case AttributeType.Vector4:
                return "";
            }

            return "???";
        }

        public static byte[] GetAttributeDataBuffer(byte[] buffer, AttributeType type)
        {
            var size = GetAttributeTypeSize(type);
            
            if (size == -1)
            {
                var len = (buffer != null) ? buffer.Length : 0;

                var newBuffer = new byte[len];

                // return a copy of the data if not null
                // otherwise return an empty array
                if (buffer != null)
                    Array.Copy(buffer, newBuffer, buffer.Length);

                return newBuffer;
            }

            var data = new byte[size];

            if (buffer != null)
            {
                var copySize = (buffer.Length > size) ? size : buffer.Length;

                Array.Copy(buffer, data, copySize);
            }

            // return a properly-sized buffer :)
            return data;
        }

        public static byte[] GetCountBuffer(int count)
        {
            if (count >= 254)
            {
                // you're gonna need a bigger buffer ;)
                var largeSize = (0xFF | ((count & 0xFFFFFF) << 8));

                return BitConverter.GetBytes(largeSize);
            }
            else
            {
                return new byte[1] { (byte)count };
            }
        }
    }

    public struct AttributeTypeValue
    {
        public AttributeType Type;

        public static implicit operator AttributeType(AttributeTypeValue typeVal)
        {
            return typeVal.Type;
        }

        public static AttributeTypeValue Parse(string content)
        {
            return new AttributeTypeValue(content);
        }

        public override string ToString()
        {
            return Type.ToString();
        }
        
        private AttributeTypeValue(string content)
        {
            Type = (AttributeType)Enum.Parse(typeof(AttributeType), content);
        }

        public AttributeTypeValue(AttributeType type)
        {
            Type = type;
        }
    }

    public struct AttributeData
    {
        public byte[] Buffer;
        public AttributeType Type;

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
            case AttributeType.Bool:
            case AttributeType.Byte:
                {
                    var value = ToByte();
                    return value.ToString();
                }
            case AttributeType.Int16:
                {
                    var value = ToInt16();
                    return value.ToString();
                }
            case AttributeType.UInt16:
                {
                    var value = ToUInt16();
                    return value.ToString();
                }
            case AttributeType.Int32:
                {
                    var value = ToInt32();
                    return value.ToString();
                }
            case AttributeType.UInt32:
                {
                    var value = ToUInt32();
                    return value.ToString();
                }
            case AttributeType.Float:
                {
                    var value = ToFloat();
                    return value.ToString();
                }
            case AttributeType.String:
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
            case AttributeType.Vector2:
            case AttributeType.Vector3:
            case AttributeType.Vector4:
                {
                    var buffer = Utils.GetAttributeDataBuffer(Buffer, Type);

                    var x = BitConverter.ToSingle(buffer, 0);
                    var y = BitConverter.ToSingle(buffer, 4);

                    switch (Type)
                    {
                    case AttributeType.Vector3:
                    case AttributeType.Vector4:
                        {
                            var z = BitConverter.ToSingle(buffer, 8);

                            if (Type == AttributeType.Vector4)
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
            case AttributeType.StringHash:
                {
                    return ToHashString();   
                }
            }

            return ToHexString();
        }

        public void WriteTo(BinaryStream stream)
        {
            //var size = 0;
            //
            //if (Buffer != null)
            //{
            //    size = Utils.GetAttributeTypeSize(Type);
            //
            //    if (size == -1)
            //    {
            //        // can be any length
            //        size = Buffer.Length;
            //    }
            //    else
            //    {
            //        if (Buffer.Length > size)
            //            throw new InvalidOperationException("Buffer is too large to fit into the specified type");
            //
            //        // cool, it's minified
            //        if (size > Buffer.Length)
            //            size = Buffer.Length;
            //    }
            //}

            if ((Buffer != null) && (Buffer.Length > 0))
            {
                var buffer = Utils.GetAttributeDataMiniBuffer(Buffer, Type);

                stream.Write(Utils.GetCountBuffer(buffer.Length));
                stream.Write(buffer);
            }
            else
            {
                // nothing to write!
                stream.WriteByte(0);
            }
        }

        public AttributeData(byte[] buffer)
        {
            Type = AttributeType.BinHex;
            Buffer = buffer;
        }

        public AttributeData(AttributeType type)
        {
            Type = type;
            Buffer = Utils.GetAttributeDataBuffer(null, type);
        }
        
        public AttributeData(AttributeType type, byte[] buffer)
        {
            Buffer = buffer;
            Type = type;
        }
    }

    public abstract class NodeBase
    {
        private string m_name;
        private int m_hash;

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

        public virtual void WriteTo(BinaryStream stream)
        {
            stream.Write(Hash);
        }
        
        public override string ToString()
        {
            return (m_name != null) ? m_name : String.Empty;
        }
        
        protected NodeBase(string name)
        {
            Name = name;
        }

        protected NodeBase(int hash)
        {
            Hash = hash;
        }

        protected NodeBase(int hash, string name)
        {
            m_name = name;
            m_hash = hash;
        }
    }
    
    public class NodeAttribute : NodeBase
    {
        public AttributeData Data { get; set; }
        
        public void WriteTo(BinaryStream stream, bool writeHash)
        {
            if (writeHash)
            {
                base.WriteTo(stream);
            }
            else
            {
                Data.WriteTo(stream);
            }
        }

        public override void WriteTo(BinaryStream stream)
        {
            Data.WriteTo(stream);
        }

        public NodeAttribute(string name)
            : this(name, AttributeType.BinHex) { }

        public NodeAttribute(int hash)
            : this(hash, AttributeType.BinHex) { }

        public NodeAttribute(int hash, string name)
        : this(hash, name, AttributeType.BinHex) { }

        public NodeAttribute(string name, AttributeType type)
            : base(name)
        {
            Data = new AttributeData(type);
        }
        
        public NodeAttribute(int hash, AttributeType type)
            : base(hash)
        {
            Data = new AttributeData(type);
        }
        
        public NodeAttribute(int hash, string name, AttributeType type)
            : base(hash, name)
        {
            Data = new AttributeData(type);
        }
    }

    public class NodeClass : NodeBase
    {
        public List<NodeAttribute> Attributes { get; set; }
        public List<NodeClass> Children { get; set; }

        public override void WriteTo(BinaryStream stream)
        {
            var nChildren = Children.Count;
            var nAttributes = Attributes.Count;

            stream.Write(Utils.GetCountBuffer(nChildren));
            stream.Write(Hash);
            
            var attrsPtr = stream.Position;
            stream.Position += 2;

            stream.Write(Utils.GetCountBuffer(nAttributes));

            // step 1: write hashes
            foreach (var attribute in Attributes)
                attribute.WriteTo(stream, true);
            // step 2: write data
            foreach (var attribute in Attributes)
                attribute.WriteTo(stream);

            var attrsSize = (int)(stream.Position - (attrsPtr + 2));

            if (attrsSize > 65535)
                throw new InvalidOperationException("Attribute data too large.");

            var childrenPtr = stream.Position;
            
            stream.Position = attrsPtr;
            stream.Write((short)attrsSize);
            
            stream.Position = childrenPtr;

            // now write the children out
            foreach (var child in Children)
                child.WriteTo(stream);
        }

        public NodeClass(int hash)
            : this(hash, -1, -1) { }
        public NodeClass(string name)
            : this(name, -1, -1) { }
        
        public NodeClass(int hash, int nChildren, int nAttributes)
            : base(hash)
        {
            Children = (nChildren == -1) ? new List<NodeClass>() : new List<NodeClass>(nChildren);
            Attributes = (nAttributes == -1) ? new List<NodeAttribute>() : new List<NodeAttribute>(nAttributes);
        }

        public NodeClass(string name, int nChildren, int nAttributes)
            : base(name)
        {
            Children = (nChildren == -1) ? new List<NodeClass>() : new List<NodeClass>(nChildren);
            Attributes = (nAttributes == -1) ? new List<NodeAttribute>() : new List<NodeAttribute>(nAttributes);
        }
    }

    public class NodeLibrary
    {
        static readonly MagicNumber Magic = "nbCF";
        static readonly int Type = 0x4005;

        public class InfoEntry
        {
            public long ID { get; set; }

            public int Unknown { get; set; }

            public int Count1 { get; set; }
            public int Count2 { get; set; }

            public void WriteTo(BinaryStream stream)
            {
                stream.Write(ID);
                stream.Write(Unknown);
                stream.Write((short)Count1);
                stream.Write((short)Count2);
            }
        }

        public int Count1 { get; set; }
        public int Count2 { get; set; }

        public NodeClass Root { get; set; }

        public List<InfoEntry> Infos { get; set; }

        public void WriteTo(BinaryStream stream)
        {
            var nInfos = Infos.Count;

            // we need to write the offset to our infos here
            var ptr = stream.Position;
            stream.Position += 4;
            
            stream.Write(nInfos);
            stream.Write((int)Magic);
            stream.Write((int)Type);

            stream.Write(Count1);
            stream.Write(Count2);

            Root.WriteTo(stream);

            var infosOffset = (int)(Memory.Align(stream.Position, 8) - ptr);
            
            // write the infos offset
            stream.Position = ptr;
            stream.Write(infosOffset);

            // write the infos
            stream.Position = infosOffset;

            foreach (var info in Infos)
                info.WriteTo(stream);
        }

        public NodeLibrary()
        {
            Root = new NodeClass("EntityLibraries");
            Infos = new List<InfoEntry>();
        }
    }

    class Program
    {
        static readonly MagicNumber FCBMagic = "nbCF";
        
        static readonly int LibraryType = 0x4005;

        static List<int> m_hints = new List<int>();

        static void WriteUniqueHint(string value)
        {
            var hash = StringHasher.GetHash(value);

            if (!m_hints.Contains(hash))
            {
                m_hints.Add(hash);
                Console.WriteLine(value);
            }
        }
        
        static int ReadOffset(BinaryStream bs)
        {
            var buffer = new byte[4];
            bs.Read(buffer, 0, 3);

            return BitConverter.ToInt32(buffer, 0);
        }
        
        static int[] ReadAttributeHashes(BinaryStream bs, out int ptr)
        {
            ptr = (int)bs.Position;

            var nHashes = bs.ReadByte();
            var isOffset = (nHashes == 254);

            if (nHashes >= 254)
                nHashes = ReadOffset(bs);

            if (isOffset)
            {
                // adjust ptr
                ptr += 4;

                // throw this away
                var deadPtr = 0;

                bs.Position = ((ptr - 4) - nHashes);
                return ReadAttributeHashes(bs, out deadPtr);
            }
            else
            {
                var hashes = new int[nHashes];

                for (int i = 0; i < nHashes; i++)
                    hashes[i] = bs.ReadInt32();

                // inline attributes, no need to adjust
                ptr = (int)bs.Position;

                return hashes;
            }
        }

        static AttributeData ReadAttribute(BinaryStream bs, string name, AttributeType type = AttributeType.BinHex)
        {
            var ptr = (int)bs.Position;
            var nC = bs.ReadByte();

            var isOffset = (nC == 254);

            // will either be an offset or >=255 count
            if (nC >= 254)
                nC = ReadOffset(bs);

            if (isOffset)
            {
                bs.Position = (ptr - nC);
                var attr = ReadAttribute(bs, name, type);

                // move past offset
                bs.Position = (ptr + 4);

                return attr;
            }
            else
            {
                var typeSize = Utils.GetAttributeTypeSize(type);

                if ((typeSize != -1) && (nC > typeSize))
                {
                    if (type != AttributeType.StringHash)
                    {
                        WriteUniqueHint($"WARNING: '{name}' is defined as a '{type.ToString()}' but the buffer overflowed (size: 0x{nC:X}) -- forcing BinHex");
                        type = AttributeType.BinHex;
                    }
                    else
                    {
                        // is this literally a part of the spec?!
                        type = AttributeType.String;
                    }
                }

                var buffer = new byte[nC];
                bs.Read(buffer, 0, nC);

                return new AttributeData(type, buffer);
            }
        }

        static StringBuilder fcbBuilder = new StringBuilder();
        static StringBuilder typesBuilder = new StringBuilder();
        
        static XmlWriter fcbLog = XmlWriter.Create(fcbBuilder, new XmlWriterSettings() {
            Indent = true,
            IndentChars = "\t",

            NewLineOnAttributes = true,

            // gotta write my own :/
            OmitXmlDeclaration = true,
        });

        static XmlReaderSettings xmlAttrTypeSettings = new XmlReaderSettings() {
            ConformanceLevel                = ConformanceLevel.Document,
            DtdProcessing                   = DtdProcessing.Parse,
            IgnoreComments                  = true,
            IgnoreProcessingInstructions    = true,
            IgnoreWhitespace                = true,
        };
        
        static TypeLookup m_attrTypes = new TypeLookup();
        static TypeLookup m_userTypes = new TypeLookup();

        static readonly string DefaultTypesName = "types.default.xml";
        static readonly string UserTypesName    = "types.user.xml";
        
        static TypeLookup GetAttributesLookup(string name)
        {
            switch (name)
            {
            case "UserTypes":
                return m_userTypes;
            case "VerifiedTypes":
                return m_attrTypes;
            }
            return null;
        }

        static bool IsAttributeTypeKnown(int hash)
        {
            return (m_attrTypes.ContainsKey(hash) || m_userTypes.ContainsKey(hash));
        }

        static AttributeType GetAttributeType(int hash)
        {
            if (m_attrTypes.ContainsKey(hash))
                return m_attrTypes[hash];
            if (m_userTypes.ContainsKey(hash))
                return m_userTypes[hash];

            return AttributeType.BinHex;
        }

        static void RegisterAttributeType(string name, AttributeType type)
        {
            var hash = StringHasher.GetHash(name);

            if (!m_userTypes.ContainsKey(hash))
                m_userTypes.Add(hash, type);
        }

        static void LoadAttributeTypesXml(string xmlName)
        {
            var file = Path.Combine(Environment.CurrentDirectory, xmlName);

            using (var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var xml = XmlReader.Create(fs, xmlAttrTypeSettings))
            {
                if (!xml.ReadToFollowing("AttributeTypes"))
                    throw new XmlException("Attribute types data corrupted!!");

                var kind = xml.GetAttribute("Kind");
                var lookup = GetAttributesLookup(kind);

                if (lookup == null)
                    throw new XmlException("Cannot load attribute types due to unknown 'Kind' parameter!");

                var groupType = AttributeType.BinHex;
                var inGroup = false;

                while (xml.Read())
                {
                    switch (xml.Name)
                    {
                    case "AttributeGroup":
                        {
                            // reset current group type
                            if (inGroup)
                                groupType = AttributeType.BinHex;
                            
                            var type = xml.GetAttribute("Type");

                            if (type != null)
                                groupType = AttributeTypeValue.Parse(xml.GetAttribute("Type"));

                            inGroup = true;
                        } continue;
                    case "Attribute":
                        {
                            var name = xml.GetAttribute("Name");
                            var hash = xml.GetAttribute("Hash");
                            var type = xml.GetAttribute("Type");

                            var attrType = AttributeType.BinHex;
                            var attrHash = (hash != null) ? int.Parse(hash, NumberStyles.HexNumber) : -1;

                            if (inGroup)
                                attrType = groupType;
                            if (type != null)
                                attrType = AttributeTypeValue.Parse(type);

                            if (name != null)
                            {
                                if (attrHash != -1)
                                {
                                    // add manual lookup
                                    StringHasher.AddToLookup(attrHash, name);
                                }
                                else
                                {
                                    attrHash = StringHasher.GetHash(name);

                                    // try adding this to the lookup
                                    if (!StringHasher.CanResolveHash(attrHash))
                                    {
                                        Debug.WriteLine($"- Adding '{name}' to lookup");
                                        StringHasher.AddToLookup(name);
                                    }
                                }
                            }
                            else
                            {
                                // attribute can't be assigned to anything, just skip it
                                if (attrHash == -1)
                                    continue;

                                var canResolve = StringHasher.CanResolveHash(attrHash);

                                name = (canResolve) ? StringHasher.ResolveHash(attrHash) : $"_{attrHash:X8}";
                                
                                if (canResolve)
                                {
                                    if (IsAttributeTypeKnown(attrHash))
                                    {
                                        var knownType = GetAttributeType(attrHash);

                                        //WriteUniqueHint($"<!-- Remove: --><Attribute Hash=\"{attrHash:X8}\" Type=\"{attrType.ToString()}\" /><!-- SameAs --><Attribute Name=\"{name}\" Type=\"{knownType.ToString()}\" />");
                                    }
                                    else
                                    {
                                        //WriteUniqueHint($"<!-- Rename: --><Attribute Hash=\"{attrHash:X8}\" Type=\"{attrType.ToString()}\" /><!-- EqualTo --><Attribute Name=\"{name}\" Type=\"{attrType.ToString()}\" />");
                                    }
                                }
                            }
                            
                            if (!lookup.ContainsKey(attrHash))
                                lookup.Add(attrHash, attrType);
                        } continue;
                    }
                } 
            }
        }
        
        static NodeClass ReadNode(BinaryStream bs, bool logStuff = false)
        {
            var ptrA = (int)bs.Position;
            var nChildren = bs.ReadByte();
            
            // probably not an offset, but rather a tri-byte
            // allows for children > 253
            if (nChildren >= 254)
                nChildren = ReadOffset(bs);
            
            var hash = bs.ReadInt32();
            var size = bs.ReadInt16();
            
            var name = StringHasher.ResolveHash(hash);

            if (name == null)
                name = $"_{hash:X6}";

            if (logStuff)
                Console.WriteLine($"{ptrA:X8}: [Node : {nChildren:X4}] ; {name}");

            fcbLog.WriteStartElement(name);

            var node = new NodeClass(hash);
            var next = ((int)bs.Position + size);
            
            if (size != 0)
            {
                var attrsPtr = (int)bs.Position;
                var attrs = ReadAttributeHashes(bs, out attrsPtr);
                
                // move to beginning of attributes
                bs.Position = attrsPtr;

                if (logStuff)
                    Console.WriteLine($"{attrsPtr:X8}:   [Attributes : {attrs.Length:X4}]");

                for (int i = 0; i < attrs.Length; i++)
                {
                    var attrPtr = bs.Position;
                    var attrHash = attrs[i];

                    var attrName = StringHasher.ResolveHash(attrHash);
                    var attrType = GetAttributeType(attrHash);

                    var isResolved = (attrName != null);

                    // cannot be null or contain spaces
                    if (!isResolved || attrName.Contains(" "))
                        attrName = $"_{attrHash:X8}";

                    var attrClassName = $"{name}.{attrName}";
                    var attrClassHash = StringHasher.GetHash(attrClassName);

                    if (IsAttributeTypeKnown(attrClassHash))
                        attrType = GetAttributeType(attrClassHash);
                    
                    //var buffer = ReadAttribute(bs);
                    //var attrValue = new AttributeData(attrType, buffer);

                    var attrValue = ReadAttribute(bs, attrName, attrType);

                    if (logStuff)
                        Console.WriteLine($"{attrPtr:X8}:     [Attribute : {attrValue.Buffer.Length:X4}] ; {attrClassName}");

                    if (!IsAttributeTypeKnown(attrHash))
                    {
                        if (attrValue.IsBufferValid())
                        {
                            var guess = Utils.GetAttributeTypeBestGuess(attrValue);

                            if (guess != AttributeType.BinHex)
                            {
                                if (isResolved)
                                {
                                    WriteUniqueHint($"<!-- Add: --><Attribute Name=\"{attrName}\" Type=\"{guess.ToString()}\" />");
                                }
                                else
                                {
                                    WriteUniqueHint($"<!-- MaybeAdd: --><Attribute Hash=\"{attrHash:X8}\" Type=\"{guess.ToString()}\" />");
                                }
                            }
                        }
                        else
                        {
                            WriteUniqueHint($"<!-- UnknownType: --><Attribute Hash=\"{attrHash:X8}\" Type=\"BinHex\" />");
                        }
                    }
                    else
                    {
                        if (Utils.IsAttributeBufferStrangeSize(attrValue))
                            WriteUniqueHint($"INFO: Attribute '{attrClassName}' is defined as a '{attrType}' and has a strange size of {attrValue.Buffer.Length} byte(s).");
                    }

                    fcbLog.WriteAttributeString(attrName, attrValue.ToString());

                    var attr = new NodeAttribute(attrHash, attrType) {
                        Data = attrValue,
                    };

                    node.Attributes.Add(attr);
                }
            }
            else
            {
                throw new NotImplementedException("Zero-length nodes are not covered under TrumpCare™.");
            }
            
            if (bs.Position != next)
                throw new InvalidOperationException("You dun fucked up, son!");
            
            // read children
            for (int n = 0; n < nChildren; n++)
            {
                var nC = bs.ReadByte();
                var isOffset = (nC == 254);

                if (nC >= 254)
                    nC = ReadOffset(bs);

                NodeClass subNode;

                if (isOffset)
                {
                    bs.Position -= (nC + 4);

                    if (logStuff)
                        Console.WriteLine($"{next:X8}: [Reference : {nC:X4}] -> {bs.Position:X8}");

                    subNode = ReadNode(bs);

                    bs.Position = (next += 4);
                }
                else
                {
                    bs.Position = next;
                    subNode = ReadNode(bs, logStuff);

                    next = (int)bs.Position;
                }
                
                node.Children.Add(subNode);
            }

            if (logStuff)
                Console.WriteLine($"{bs.Position:X8}: [End] ; {name}");
            
            fcbLog.WriteEndElement();
            return node;
        }
        
        static async Task<NodeClass> AsyncRead(BinaryStream bs, int maxAddress)
        {
            NodeClass ret = null;

            await Task.Run(() => {
                lock (bs)
                {
                    ret = ReadNode(bs);
                }
            });

            return ret;
        }

        static NodeLibrary Library { get; set; }
        
        static void LoadLibrary(BinaryStream bs, string logFile)
        {
            var datOffset = bs.ReadInt32();
            var datCount = bs.ReadInt32();

            var magic = bs.ReadInt32();

            if (magic != FCBMagic)
                throw new InvalidOperationException("Bad magic, no FCB data to parse!");

            var type = bs.ReadInt32();

            if (type != LibraryType)
                throw new InvalidOperationException("FCB library reported the incorrect type?!");
            
            var count1 = bs.ReadInt32(); // * 3
            var count2 = bs.ReadInt32(); // * 4
            
            var memSize = ((count1 * 3) + count2) * 4;
            var memSizeAlign = Memory.Align(memSize, 16);

            var fcbHeader = (int)bs.Position;
            
            // read the unknown data
            bs.Position = datOffset;

            // what the actual F$%^!!!!
            // WHY DO I HAVE TO DO THIS?!
            fcbLog.WriteRaw("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n");
            
            Library = new NodeLibrary() {
                Count1 = count1,
                Count2 = count2,
            };

            var nInfosCount1 = 0;
            var nInfosCount2 = 0;

            for (int i = 0; i < datCount; i++)
            {
                var field1 = bs.ReadInt64();
                var field2 = bs.ReadInt32();
                var field3 = bs.ReadInt16();
                var field4 = bs.ReadInt16();

                var hexVal = Utils.Bytes2HexString(BitConverter.GetBytes(field1));
                
                var info = new NodeLibrary.InfoEntry() {
                    ID = field1,
                    Unknown = field2,
                    Count1 = field3,
                    Count2 = field4,
                };

                nInfosCount1 += info.Count1;
                nInfosCount2 += info.Count2;

                Library.Infos.Add(info);
            }

            var count1Diff = (count1 - nInfosCount1);
            var count2Diff = (count2 - nInfosCount2);
            
            Console.WriteLine("[Library.Header]");
            Console.WriteLine($"  Count1: {count1} ({count1:X8})");
            Console.WriteLine($"  Count2: {count2} ({count2:X8})");
            Console.WriteLine($"  SizeTally: {memSize:X8}");
            Console.WriteLine("[Library.Infos]");
            Console.WriteLine($"  Count1Total: {nInfosCount1} ({nInfosCount1:X8})");
            Console.WriteLine($"  Count2Total: {nInfosCount2} ({nInfosCount2:X8})");
            Console.WriteLine("[Library.Logging]");
            Console.WriteLine($"  Count1Diff: {count1Diff} ({count1Diff:X8})");
            Console.WriteLine($"  Count2Diff: {count2Diff} ({count2Diff:X8})");

            // read fcb data
            bs.Position = fcbHeader;

            var root = new NodeClass("EntityLibraries");
            fcbLog.WriteStartElement(root.Name);

            try
            {
                foreach (var info in Library.Infos)
                {
                    fcbLog.WriteStartElement("EntityLibrary");
                    fcbLog.WriteAttributeString("UID", Utils.Bytes2HexString(BitConverter.GetBytes(info.ID)));
                    
                    // relative to 'nbCF' header (DOH!)
                    var infoPtr = (info.Unknown + 8);
                    bs.Position = infoPtr;
                    
                    var nodePtr = bs.Position;

                    var node = ReadNode(bs, false);
                    var nodeTotal = Utils.GetTotalNumberOfNodes(node);
                    
                    root.Children.Add(node);

                    fcbLog.WriteEndElement();
                }

                Console.WriteLine($"Finished reading {root.Children.Count} infos. Collected {Utils.GetTotalNumberOfNodes(root)} nodes in total.");

                Library.Root = root;
            }
            //catch (Exception e)
            //{
            //    throw new ApplicationException("Fatal error while reading data!", e);
            //}
            finally
            {
                if (fcbLog.WriteState != WriteState.Error)
                    fcbLog.WriteEndElement();

                File.WriteAllText(logFile, fcbBuilder.ToString());
            }
        }
        
        static void Main(string[] args)
        {
            var filename = (args.Length >= 1) ? args[0] : @"C:\Dev\Research\WD2\entitylibrary_rt.fcb";
            var xmlFile = (args.Length >= 2) ? args[1] : Path.ChangeExtension(filename, ".xml");

            StringHasher.AddLookupsFile(Path.Combine(Environment.CurrentDirectory, "strings.txt"));
            StringHasher.AddLookupsFile(Path.Combine(Environment.CurrentDirectory, "strings.user.txt"));

            LoadAttributeTypesXml(DefaultTypesName);
            LoadAttributeTypesXml(UserTypesName);

            using (var bs = new BinaryStream(filename))
            {
                LoadLibrary(bs, xmlFile);

                var writeTest = false;

                if (writeTest)
                {
                    byte[] buffer;

                    using (var tmp = new BinaryStream(4096 * 1024))
                    {
                        // lol
                        //Library.Count1 *= 32;
                        //Library.Count2 *= 32;

                        Library.WriteTo(tmp);

                        var size = (int)tmp.Position;
                        buffer = new byte[size];

                        tmp.Position = 0;
                        tmp.Read(buffer, 0, size);
                    }

                    File.WriteAllBytes(Path.ChangeExtension(filename, ".out"), buffer);
                }
            }
        }
    }
}
