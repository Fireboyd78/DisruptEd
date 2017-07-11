using System;
using System.Collections.Generic;
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
    using TypeLookup = SortedDictionary<string, AttributeType>;

    enum AttributeType
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

        Vector3,
        Vector4,
    }

    class Program
    {
        static readonly MagicNumber FCBMagic = "nbCF";
        
        static readonly int LibraryType = 0x4005;
        static int indentLevel = 0;
        
        static int ReadOffset(BinaryStream bs)
        {
            var buffer = new byte[4];
            bs.Read(buffer, 0, 3);

            return BitConverter.ToInt32(buffer, 0);
        }
        
        public static HashLookup LookupTable;

        static void PrepareLookupTable()
        {
            if (LookupTable == null)
            {
                LookupTable = new HashLookup();

                var lookups = File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, "strings.txt"));
                var lookupsFix = new List<String>();
                
                foreach (var lookup in lookups)
                {
                    var bytes = Encoding.UTF8.GetBytes(lookup);
                    var hash = (int)Memory.GetCRC32(bytes);

                    if (!LookupTable.ContainsKey(hash))
                    {
                        lookupsFix.Add(lookup);
                        LookupTable.Add(hash, lookup);
                    }
                }

                File.WriteAllLines(Path.Combine(Environment.CurrentDirectory, "gen_strings.txt"), lookupsFix);
            }
        }

        static string GetStringByHash(int hash)
        {
            PrepareLookupTable();

            if (LookupTable.ContainsKey(hash))
                return LookupTable[hash];

            return null;
        }

        static string Bytes2HexString(byte[] bytes)
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

        static string IndentString(string str)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < indentLevel; i++)
                sb.Append("\t");

            sb.Append(str);

            return sb.ToString();
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
        
        static TypeLookup m_attrTypes = new TypeLookup(StringComparer.Ordinal) {
            { "Value", AttributeType.Reserved },
        };

static void DumpTypesLookup(string file)
        {
            var xmlDoc = new XmlDocument();
            var rootElem = xmlDoc.CreateElement("AttributeTypes");
            
            var types = m_attrTypes.OrderBy(kv => {
                // forcefully put hashes at the end
                // kinda hacky but should work fine
                if (kv.Key[0] == '_')
                    return '~' + kv.Key;

                return kv.Key;
            }, StringComparer.Ordinal);

            foreach (var type in types)
            {
                var elem = xmlDoc.CreateElement("Attribute");

                elem.SetAttribute("Name", type.Key);
                elem.SetAttribute("Type", type.Value.ToString());

                rootElem.AppendChild(elem);
            }

            xmlDoc.AppendChild(rootElem);

            using (var xmlFile = XmlWriter.Create(file, new XmlWriterSettings() { Indent = true }))
            {
                xmlDoc.WriteTo(xmlFile);
            }
        }

        static bool IsAttributeTypeKnown(string name)
        {
            return m_attrTypes.ContainsKey(name);
        }

        static AttributeType GetAttributeType(string name)
        {
            if (m_attrTypes.ContainsKey(name))
                return m_attrTypes[name];

            return AttributeType.BinHex;
        }

        static int GetAttributeTypeSize(AttributeType type)
        {
            switch (type)
            {
            case AttributeType.Bool:
            case AttributeType.Byte:
                return 1;

            case AttributeType.Int16:
                return 2;

            case AttributeType.Int32:
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

        static string GetAttributeTypeDefault(AttributeType type)
        {
            switch (type)
            {
            case AttributeType.Bool:
            case AttributeType.Byte:
            case AttributeType.Int16:
            case AttributeType.Int32:
            case AttributeType.StringHash:
                return "0";
            }

            return "";
        }

        static bool IsBufferPossiblyAttribute(byte[] buffer, AttributeType type)
        {
            // obviously not...
            if (buffer == null)
                return false;

            var typeSize = GetAttributeTypeSize(type);
            
            if (typeSize == -1)
                return (buffer.Length != 0);

            return (buffer.Length == typeSize);
        }
        
        static void RegisterAttributeType(string name, AttributeType type)
        {
            if (!m_attrTypes.ContainsKey(name))
                m_attrTypes.Add(name, type);
        }
        
        static void ReadEntry(BinaryStream bs/*, int level*/)
        {
            var ptrA = (int)bs.Position;
            
            var n1c = bs.ReadByte();

            if (n1c >= 254)
                n1c = ReadOffset(bs);

            var ptrB = (int)bs.Position;
            var hash = bs.ReadInt32();
            
            var size = bs.ReadInt16();

            var name = GetStringByHash(hash);

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
                    var attr = attrs[i];

                    var attrName = GetStringByHash(attr);
                    var buffer = ReadAttribute(bs);
                    
                    // cannot be null or contain spaces
                    if (attrName == null || attrName.Contains(" "))
                        attrName = $"_{attr:X8}";
                    
                    var attrValueStr = "";
                    var attrType = GetAttributeType(attrName);

                    var isUnknown = (attrType == AttributeType.BinHex);

                    if (isUnknown)
                    {
                        // HACK HACK HACK!
                        if (attrName.Contains("Name") || (buffer.Length > 4))
                        {
                            if ((buffer.Length > 1) && (buffer.Last() == '\0'))
                            {
                                attrType = AttributeType.String;
                                isUnknown = false;

                                for (int idx = 0; idx < (buffer.Length - 1); idx++)
                                {
                                    var c = (char)buffer[idx];

                                    if (c < 0x9 || (c > 0xD && c < 0x20) || c > 0x7F)
                                    {
                                        attrType = AttributeType.BinHex;
                                        isUnknown = true;
                                        break;
                                    }
                                    else
                                    {
                                        attrValueStr += c;
                                    }
                                }
                            }
                        }

                        if (isUnknown)
                        {
                            // boolean?
                            if ((buffer.Length <= 1) && attrName[0] == 'b')
                            {
                                attrType = AttributeType.Bool;
                                isUnknown = false;

                                var value = (buffer.Length == 1) ? buffer[0] : 0;
                                attrValueStr = value.ToString();
                            }

                            // float?
                            if ((buffer.Length <= 4) && attrName[0] == 'f')
                            {
                                attrType = AttributeType.Float;
                                isUnknown = false;

                                var value = (buffer.Length == 4) ? BitConverter.ToSingle(buffer, 0) : 0.0f;
                                attrValueStr = value.ToString();

                                // hacks are bad, mmkay?
                                if (attrValueStr.Contains("E") || attrValueStr.Contains("NaN"))
                                {
                                    attrType = AttributeType.BinHex;
                                    isUnknown = true;
                                }
                            }

                            // integer?
                            if ((buffer.Length <= 4) && attrName[0] == 'i')
                            {
                                isUnknown = false;

                                var value = 0;

                                switch (buffer.Length)
                                {
                                case 1:
                                    attrType = AttributeType.Byte;
                                    value = buffer[0];
                                    break;
                                case 2:
                                    attrType = AttributeType.Int16;
                                    value = BitConverter.ToInt16(buffer, 0);
                                    break;
                                case 4:
                                    attrType = AttributeType.Int32;
                                    value = BitConverter.ToInt32(buffer, 0);
                                    break;
                                default:
                                    isUnknown = true;
                                    break;
                                }

                                if (!isUnknown)
                                    attrValueStr = value.ToString();
                            }

                            if (isUnknown)
                            {
                                var iVal = 0;

                                switch (buffer.Length)
                                {
                                case 1:
                                    attrType = AttributeType.Byte;
                                    isUnknown = false;

                                    iVal = buffer[0];
                                    attrValueStr = iVal.ToString();
                                    break;
                                case 2:
                                    attrType = AttributeType.Int16;
                                    isUnknown = false;

                                    iVal = BitConverter.ToInt16(buffer, 0);
                                    attrValueStr = iVal.ToString();
                                    break;
                                case 4:
                                    var fVal = BitConverter.ToSingle(buffer, 0);
                                    iVal = BitConverter.ToInt32(buffer, 0);

                                    // hacky as fuck D:
                                    if (!(fVal > 9999.9999f || fVal < -9999.9999f))
                                    {
                                        isUnknown = false;
                                        attrValueStr = fVal.ToString();

                                        // hacks are bad, mmkay?
                                        if (attrValueStr.Contains("E-") || attrValueStr.Contains("NaN"))
                                            isUnknown = true;

                                        if (!isUnknown)
                                            attrType = AttributeType.Float;
                                    }

                                    if (isUnknown)
                                    {
                                        attrType = AttributeType.Int32;
                                        isUnknown = false;

                                        var valueStr = GetStringByHash(iVal);
                                        attrValueStr = (valueStr != null) ? $"$({valueStr})" : iVal.ToString();

                                        if (valueStr != null)
                                            attrType = AttributeType.StringHash;
                                    }

                                    break;
                                }

                                if (isUnknown)
                                {
                                    // either hex array or zero
                                    attrValueStr = (buffer.Length != 0) ? Bytes2HexString(buffer) : "0";
                                }
                            }
                        }

                        // only add unique attributes we definitely resolved
                        if (!IsAttributeTypeKnown(attrName) && (attrType != AttributeType.BinHex))
                            RegisterAttributeType(attrName, attrType);
                    }
                    else
                    {
                        var attrMayBeType = IsBufferPossiblyAttribute(buffer, attrType);

                        if (attrMayBeType)
                        {
                            // neatly retrieve the type :)
                            switch (attrType)
                            {
                            case AttributeType.Bool:
                            case AttributeType.Byte:
                                {
                                    var value = buffer[0];
                                    attrValueStr = value.ToString();
                                }
                                break;
                            case AttributeType.Int16:
                                {
                                    var value = BitConverter.ToInt16(buffer, 0);
                                    attrValueStr = value.ToString();
                                }
                                break;
                            case AttributeType.Int32:
                                {
                                    var value = BitConverter.ToInt32(buffer, 0);
                                    attrValueStr = value.ToString();
                                }
                                break;
                            case AttributeType.Float:
                                {
                                    var value = BitConverter.ToSingle(buffer, 0);
                                    attrValueStr = value.ToString();
                                }
                                break;
                            case AttributeType.String:
                                {
                                    for (int idx = 0; idx < (buffer.Length - 1); idx++)
                                    {
                                        var c = (char)buffer[idx];

                                        if (c != 0)
                                            attrValueStr += c;
                                    }
                                }
                                break;
                            case AttributeType.StringHash:
                                {
                                    var value = BitConverter.ToInt32(buffer, 0);
                                    var hashStr = GetStringByHash(value);

                                    attrValueStr = (hashStr != null) ? $"$({hashStr})" : $"_{value:X8}";
                                }
                                break;
                            default:
                                {
                                    attrValueStr = Bytes2HexString(buffer);
                                }
                                break;
                            }
                        }
                        else
                        {
                            attrValueStr = (buffer.Length == 0) ? GetAttributeTypeDefault(attrType) : Bytes2HexString(buffer);
                        }
                    }

                    fcbLog.WriteAttributeString(attrName, attrValueStr);
                }
            }
            else
            {
                throw new NotImplementedException("Zero-length nodes are not covered under TrumpCare™.");
            }
            
            if (bs.Position != next)
                throw new InvalidOperationException("You dun fucked up, son!");
            
            //var myLevel = indentLevel++;
            
            /* fourth */
            for (int n = 0; n < n1c; n++)
            {
                var nC = bs.ReadByte();
                var isOffset = (nC == 254);

                if (nC >= 254)
                    nC = ReadOffset(bs);

                if (isOffset)
                {
                    bs.Position -= (nC + 4);
                    ReadEntry(bs/*, indentLevel*/);

                    bs.Position = (next += 4);
                }
                else
                {
                    bs.Position = next;
                    ReadEntry(bs/*, indentLevel*/);

                    next = (int)bs.Position;
                }
            }

            //--indentLevel;
            //
            //if ((n1c > 0) && (indentLevel == myLevel))
            //    xmlLog.AppendLine(IndentString($"</{name}>"));

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
        
        static void LoadLibrary(BinaryStream bs)
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

            //Console.WriteLine($"({datOffset:X8})[{datCount}]");
            //Console.WriteLine($"[{count1:X8}, {count2:X8}] : {memSize:X8} ({memSizeAlign:X8})");
            //Console.WriteLine("-------------------");
            
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
                Console.WriteLine(fcbBuilder.ToString());
                DumpTypesLookup(Path.Combine(Environment.CurrentDirectory, "types.xml"));
            }
        }
        
        static void Main(string[] args)
        {
            var filename = (args.Length > 0) ? args[0] : @"C:\Dev\Research\WD2\entitylibrary.fcb";

            using (var bs = new BinaryStream(filename))
            {
                LoadLibrary(bs);
            }
        }
    }
}
