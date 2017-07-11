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
        Reserved,

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
            case AttributeType.StringHash:
                return "0";

            case AttributeType.BinHex:
            case AttributeType.String:
                return "";

            case AttributeType.Vector2:
            case AttributeType.Vector3:
            case AttributeType.Vector4:
                return "[]";
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
    }

    public struct AttributeTypeValue
    {
        AttributeType Type;

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
        byte[] Buffer;
        AttributeType Type;

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

                                return $"[{x},{y},{z},{w}]";
                            }
                            else
                            {
                                return $"[{x},{y},{z}]";
                            }
                        }
                    }

                    return $"[{x},{y}]";
                }
            case AttributeType.StringHash:
                {
                    return ToHashString();   
                }
            }

            return ToHexString();
        }
        
        public AttributeData(byte[] buffer, AttributeType type = AttributeType.BinHex)
        {
            Buffer = buffer;
            Type = type;
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

        static byte[] ReadAttribute(BinaryStream bs)
        {
            byte[] buffer;

            var ptr = (int)bs.Position;
            var nC = bs.ReadByte();

            if (nC >= 254)
            {
                // ????
                if (nC == 255)
                    throw new InvalidOperationException("Unknown attribute, cannot process data!");
                
                nC = ReadOffset(bs);

                bs.Position = (ptr - nC);
                buffer = ReadAttribute(bs);
                
                // move past offset
                bs.Position = (ptr + 4);
            }
            else
            {
                buffer = new byte[nC];
                bs.Read(buffer, 0, nC);
            }

            return buffer;
        }

        static StringBuilder fcbBuilder = new StringBuilder();
        static StringBuilder typesBuilder = new StringBuilder();
        
        static XmlWriter fcbLog = XmlWriter.Create(fcbBuilder, new XmlWriterSettings() {
            Indent = true,
            IndentChars = "\t",

            NewLineOnAttributes = true,
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
                                        Console.WriteLine($"- Adding '{name}' to lookup");
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

                                        WriteUniqueHint($"<Attribute Hash=\"{attrHash:X8}\" Type=\"{attrType.ToString()}\" /> <!-- EqualTo --> <Attribute Name=\"{name}\" Type=\"{knownType.ToString()}\" />");
                                    }
                                    else
                                    {
                                        WriteUniqueHint($"<Attribute Hash=\"{attrHash:X8}\" Type=\"{attrType.ToString()}\" /> <!-- CanEqual --> <Attribute Name=\"{name}\" Type=\"{attrType.ToString()}\" />");
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
        
        static void ReadEntry(BinaryStream bs/*, int level*/)
        {
            var ptrA = (int)bs.Position;
            
            var nChildren = bs.ReadByte();

            // not actually an offset,
            // but rather a tri-byte (because there's so many children)
            if (nChildren >= 254)
                nChildren = ReadOffset(bs);
            
            var hash = bs.ReadInt32();
            var size = bs.ReadInt16();

            var name = StringHasher.ResolveHash(hash);

            if (name == null)
                name = $"_{hash:X6}";

            fcbLog.WriteStartElement(name);
            
            var next = ((int)bs.Position + size);
            
            if (size != 0)
            {
                var attrsPtr = (int)bs.Position;
                var attrs = ReadAttributeHashes(bs, out attrsPtr);
                
                // move to beginning of attributes
                bs.Position = attrsPtr;
                
                for (int i = 0; i < attrs.Length; i++)
                {
                    var attrHash = attrs[i];

                    var attrName = StringHasher.ResolveHash(attrHash);
                    var attrType = GetAttributeType(attrHash);

                    var isResolved = (attrName != null);

                    // cannot be null or contain spaces
                    if (!isResolved || attrName.Contains(" "))
                    {
                        attrName = $"_{attrHash:X8}";
                    }

                    var attrClassName = $"{name}.{attrName}";
                    var attrClassHash = StringHasher.GetHash(attrClassName);

                    if (IsAttributeTypeKnown(attrClassHash))
                        attrType = GetAttributeType(attrClassHash);
                    
                    var buffer = ReadAttribute(bs);
                    var attrValue = new AttributeData(buffer, attrType);

                    if (!IsAttributeTypeKnown(attrHash))
                    {
                        if (attrValue.IsBufferValid())
                        {
                            var guess = Utils.GetAttributeTypeBestGuess(buffer.Length);

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
                    
                    fcbLog.WriteAttributeString(attrName, attrValue.ToString());
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

                if (isOffset)
                {
                    bs.Position -= (nC + 4);
                    ReadEntry(bs);

                    bs.Position = (next += 4);
                }
                else
                {
                    bs.Position = next;
                    ReadEntry(bs);

                    next = (int)bs.Position;
                }
            }
            
            fcbLog.WriteEndElement();
        }
        
        static async Task AsyncRead(BinaryStream bs, int maxAddress)
        {
            await Task.Run(() => {
                lock (bs)
                {
                    while ((bs.Position + 7) < maxAddress)
                        ReadEntry(bs/*, 0*/);
                }
            });
        }
        
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
            
            try
            {
                var readTask = AsyncRead(bs, datOffset);
                readTask.Wait();
            }
            catch (Exception e)
            {
                throw new ApplicationException("Fatal error while reading data!", e);
            }
            finally
            {
                File.WriteAllText(logFile, fcbBuilder.ToString());
            }
        }
        
        static void Main(string[] args)
        {
            var filename = (args.Length >= 1) ? args[0] : @"C:\Dev\Research\WD2\entitylibrary.fcb";
            var xmlFile = (args.Length >= 2) ? args[1] : Path.ChangeExtension(filename, ".xml");

            StringHasher.AddLookupsFile(Path.Combine(Environment.CurrentDirectory, "strings.txt"));
            StringHasher.AddLookupsFile(Path.Combine(Environment.CurrentDirectory, "strings.user.txt"));

            LoadAttributeTypesXml(DefaultTypesName);
            LoadAttributeTypesXml(UserTypesName);

            using (var bs = new BinaryStream(filename))
            {
                LoadLibrary(bs, xmlFile);
            }
        }
    }
}
