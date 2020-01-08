using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using LZ4Sharp;

namespace Nomad
{
    public class OasisStringsFile
    {
        public static class OasisStringLookup
        {
            static readonly Dictionary<int, string> sm_Strings 
                = new Dictionary<int, string>();

            public static void Clear()
            {
                sm_Strings.Clear();
            }

            public static void AddString(int id, string value)
            {
                sm_Strings.Add(id, value);
            }

            public static void SetString(int id, string value)
            {
                sm_Strings[id] = value;
            }

            public static string GetString(int id)
            {
                var result = String.Empty;

                sm_Strings.TryGetValue(id, out result);

                return result;
            }
        }

        public class OasisSection
        {
            public class OasisLocalizedString
            {
                public int Id;

                public StringId Section;
                public StringId Enum;
                
                public string Value
                {
                    get { return OasisStringLookup.GetString(Id); }
                }

                public OasisLocalizedString()
                { }

                public OasisLocalizedString(XElement obj, int sectionCRC)
                {
                    Deserialize(obj);

                    Section = sectionCRC;
                }

                public void Deserialize(BinaryStream input, int nameCRC)
                {
                    Id = input.ReadInt32();
                    Section = input.ReadInt32();

                    if (Section != nameCRC)
                        throw new FormatException("oasis string section CRC does not match the section's CRC value.");

                    Enum = input.ReadInt32();
                }

                public void Serialize(BinaryStream output)
                {
                    output.Write(Id);
                    output.Write(Section);
                    output.Write(Enum);
                }

                public void Deserialize(XElement obj)
                {
                    if (obj.Name != "string")
                        throw new Exception("invalide node type for constructing an OasisLocalizedString.");

                    var enumVal = obj.Attribute("enum").Value;
                    var idVal = obj.Attribute("id").Value;

                    Enum = StringId.Parse(enumVal);
                    Id = int.Parse(idVal);

                    OasisStringLookup.SetString(Id, obj.Attribute("value").Value);
                }

                public void Serialize(XElement root)
                {
                    if (Enum == 0u || Id == 0u)
                        throw new FormatException("attempting to emit XML node prior to proper OasisLocalizedString construction.");

                    var obj = new XElement("string");
                    
                    obj.SetAttributeValue("enum", Enum);
                    obj.SetAttributeValue("id", Id);
                    obj.SetAttributeValue("value", Value);

                    root.Add(obj);
                }
            }

            class CompressedValues
            {
                public int LastSortedCRC;

                public int CompressedSize;

                public int DecompressedSize;

                public byte[] CompressedBytes;

                public void Deserialize(BinaryStream input)
                {
                    LastSortedCRC = input.ReadInt32();
                    CompressedSize = input.ReadInt32();
                    DecompressedSize = input.ReadInt32();
                    CompressedBytes = input.ReadBytes(CompressedSize);
                }

                public void Serialize(BinaryStream output)
                {
                    output.Write(LastSortedCRC);
                    output.Write(CompressedSize);
                    output.Write(DecompressedSize);
                    output.Write(CompressedBytes);
                }

                public CompressedValues()
                {
                }
            }

            class DecompressedValues
            {
                public int StringCount;

                public List<int> SortedEnums;

                public List<int> StringOffsets;

                public List<KeyValuePair<int, string>> IdValuePairs;

                public DecompressedValues()
                {
                }

                public DecompressedValues(List<OasisLocalizedString> oaStrings)
                {
                    StringCount = oaStrings.Count;
                    SortedEnums = new List<int>();
                    StringOffsets = new List<int>();
                    IdValuePairs = new List<KeyValuePair<int, string>>();
                    var num = 0;
                    foreach (OasisLocalizedString oasisLocalizedString in (from x in oaStrings
                        orderby x.Enum
                        select x).ToList<OasisLocalizedString>())
                    {
                        SortedEnums.Add(oasisLocalizedString.Enum);
                        StringOffsets.Add(num);
                        var id = oasisLocalizedString.Id;
                        var value = oasisLocalizedString.Value;
                        IdValuePairs.Add(new KeyValuePair<int, string>(id, value));
                        var bytes = Encoding.Unicode.GetBytes(value);
                        num += bytes.Length + 6;
                    }
                }

                public void Deserialize(BinaryStream input)
                {
                    StringCount = input.ReadInt32();

                    SortedEnums = new List<int>(StringCount);
                    StringOffsets = new List<int>(StringCount);
                    IdValuePairs = new List<KeyValuePair<int, string>>(StringCount);

                    //
                    // SortedEnums
                    //
                    for (int i = 0; i < StringCount; i++)
                    {
                        var val = input.ReadInt32();

                        SortedEnums.Add(val);
                    }
                    
                    //
                    // StringOffsets
                    //
                    for (int j = 0; j < StringCount; j++)
                    {
                        var val = input.ReadInt32();

                        StringOffsets.Add(val);
                    }
                    
                    //
                    // IdValuePairs
                    //
                    for (int k = 0; k < StringCount; k++)
                    {
                        var id = input.ReadInt32();
                        var str = input.ReadString(Encoding.Unicode);

                        var kv = new KeyValuePair<int, string>(id, str);

                        IdValuePairs.Add(kv);
                    }
                }

                public void Serialize(BinaryStream output)
                {
                    output.Write(StringCount);

                    foreach (var value in SortedEnums)
                        output.Write(value);

                    foreach (var value2 in StringOffsets)
                        output.Write(value2);

                    foreach (var tuple in IdValuePairs)
                    {
                        output.Write(tuple.Key);
                        output.Write(tuple.Value, Encoding.Unicode);
                    }
                }
            }

            public StringId Name;

            public int StringCount;

            public List<OasisLocalizedString> LocalizedStrings = new List<OasisLocalizedString>();

            public int CompressedValuesSectionsCount;
            
            readonly uint MAX_LENGTH = 16384u;

            public OasisSection()
            { }

            public OasisSection(XElement obj)
            {
                Deserialize(obj);
            }

            public void Deserialize(BinaryStream input)
            {
                Name = input.ReadInt32();
                StringCount = input.ReadInt32();
                
                for (int i = 0; i < StringCount; i++)
                {
                    var locStr = new OasisLocalizedString();
                    locStr.Deserialize(input, Name);

                    LocalizedStrings.Add(locStr);
                }

                CompressedValuesSectionsCount = input.ReadInt32();
                
                for (int i = 0; i < CompressedValuesSectionsCount; i++)
                {
                    var cpr = new CompressedValues();
                    cpr.Deserialize(input);

                    var lz = new LZ4Decompressor64();
                    var buf = new byte[cpr.DecompressedSize];

                    lz.DecompressKnownSize(cpr.CompressedBytes, buf, cpr.DecompressedSize);
                    
                    var dCpr = new DecompressedValues();

                    using (var bs = new BinaryStream(buf))
                        dCpr.Deserialize(bs);

                    foreach (var kv in dCpr.IdValuePairs)
                    {
                        var id = kv.Key;
                        var value = kv.Value;

                        OasisStringLookup.SetString(id, value);
                    }
                }
            }

            public void Serialize(BinaryStream output)
            {
                output.Write(Name.Hash);
                output.Write(StringCount);

                foreach (var locStr in LocalizedStrings)
                    locStr.Serialize(output);
                
                var vals = new List<DecompressedValues>();
                var locStrs = new List<OasisLocalizedString>();

                var len = 0;
                var crc = 0;

                foreach (var locStr in LocalizedStrings)
                {
                    locStrs.Add(locStr);

                    len += (locStr.Value.Length * 2);

                    if ((len >= MAX_LENGTH)
                        && (locStr.Enum != crc))
                    {
                        var val = new DecompressedValues(locStrs);
                        vals.Add(val);

                        locStrs = new List<OasisLocalizedString>();
                        len = 0;
                        crc = 0;
                    }
                    else
                    {
                        crc = locStr.Enum;
                    }
                }

                if (locStrs.Count != 0)
                {
                    var val = new DecompressedValues(locStrs);
                    vals.Add(val);

                    locStrs = new List<OasisLocalizedString>();
                }

                output.Write(vals.Count);

                foreach (var val in vals)
                {
                    var cpr = new CompressedValues();

                    using (var bs = new BinaryStream(1024))
                    {
                        val.Serialize(bs);

                        var buffer = bs.ToArray();                        
                        var size = buffer.Length;

                        byte[] cprBuffer = null;
                        var lz = new LZ4Compressor64();

                        var cprSize = lz.Compress(buffer, cprBuffer);

                        cpr.CompressedBytes = cprBuffer;
                        cpr.CompressedSize = cprSize;

                        cpr.DecompressedSize = size;

                        cpr.LastSortedCRC = val.SortedEnums.Last();
                    }

                    cpr.Serialize(output);
                }
            }

            public void Serialize(XElement root)
            {
                if (Name == StringId.None || StringCount == 0 || LocalizedStrings.Count == 0 || CompressedValuesSectionsCount == 0)
                    throw new FormatException("attempting to emit XML node prior to proper OasisSection construction.");

                var obj = new XElement("section");
                
                obj.SetAttributeValue("name", Name);

                foreach (var str in LocalizedStrings)
                    str.Serialize(obj);

                root.Add(obj);
            }

            public void Deserialize(XElement obj)
            {
                if (obj.Name != "section")
                    throw new Exception("invalid node type for populating an OasisSection");

                var nameVal = obj.Attribute("name").Value;

                Name = StringId.Parse(nameVal);
                
                foreach (var child in obj.Elements())
                {
                    var locStr = new OasisLocalizedString(child, Name.Hash);

                    LocalizedStrings.Add(locStr);
                }

                StringCount = LocalizedStrings.Count;
            }
        }
        
        public int Version { get; set; }
        
        public string Language { get; set; }

        public List<OasisSection> Sections { get; set; }

        public OasisStringsFile()
        { }

        public OasisStringsFile(string filename)
        {
            var xDoc = Utils.LoadXmlFile(filename);

            var root = xDoc.Root;

            if (root.Name != "stringtable")
                throw new XmlException("Not a valid stringtable!");

            Language = root.Attribute("language").Value;

            foreach (var obj in root.Elements("section"))
            {
                var section = new OasisSection(obj);

                Sections.Add(section);
            }
        }
        
        public void Deserialize(BinaryStream input)
        {
            var type = input.ReadInt32();

            if (type != 1)
                throw new FormatException("not an oasisstrings_compressed.bin file");

            var count = input.ReadInt32();

            Sections = new List<OasisSection>(count);
            
            for (int i = 0; i < count; i++)
            {
                var section = new OasisSection();
                section.Deserialize(input);

                Sections.Add(section);
            }
        }

        public void Serialize(BinaryStream output)
        {
            output.Write(Version);
            output.Write(Sections.Count);

            foreach (var section in Sections)
                section.Serialize(output);
        }

        public void WriteTo(string filename)
        {
            var xDoc = new XDocument();
            var root = new XElement("stringtable");

            root.SetAttributeValue("language", Language);

            foreach (var section in Sections)
                section.Serialize(root);

            xDoc.Add(root);

            using (var writer = XmlWriter.Create(filename, new XmlWriterSettings() {
                Encoding = Encoding.Unicode,

                Indent = true,
                IndentChars = "\t",

                NewLineOnAttributes = false,
            }))
            {
                xDoc.WriteTo(writer);
                writer.Flush();
            }
        }
    }

    public class OasisSerializer : INomadXmlFileSerializer
    {
        FileType INomadSerializer.Type => FileType.Binary;

        FormatType INomadSerializer.Format { get; set; }

        public OasisStringsFile Data { get; set; }

        public NomadObject Deserialize(Stream stream)
        {
            var bs = (stream as BinaryStream)
                ?? new BinaryStream(stream);

            Data = new OasisStringsFile();
            Data.Deserialize(bs);

            return null;
        }

        public void Serialize(Stream stream, NomadObject _unused)
        {
            var bs = (stream as BinaryStream)
                ?? new BinaryStream(stream);

            Data.Serialize(bs);
        }

        public void LoadXml(string filename)
        {
            Data = new OasisStringsFile(filename);
        }

        public void SaveXml(string filename)
        {
            Data.WriteTo(filename);
        }
    }
}
