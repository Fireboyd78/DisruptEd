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
    using TypeLookup = Dictionary<int, DataType>;

    public enum DataType
    {
        BinHex = 0, /* MUST BE ZERO */

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

    public struct DataTypeValue
    {
        public DataType Type;

        public static implicit operator DataType(DataTypeValue typeVal)
        {
            return typeVal.Type;
        }

        public static DataTypeValue Parse(string content)
        {
            return new DataTypeValue(content);
        }

        public override string ToString()
        {
            return Type.ToString();
        }

        private DataTypeValue(string content)
        {
            Type = (DataType)Enum.Parse(typeof(DataType), content);
        }

        public DataTypeValue(DataType type)
        {
            Type = type;
        }
    }

    public static class AttributeTypes
    {
        public static readonly string DefaultTypesName = "types.default.xml";
        public static readonly string UserTypesName = "types.user.xml";

        static TypeLookup m_attrTypes = new TypeLookup();
        static TypeLookup m_userTypes = new TypeLookup();

        static XmlReaderSettings xmlAttrTypeSettings = new XmlReaderSettings() {
            ConformanceLevel = ConformanceLevel.Document,
            DtdProcessing = DtdProcessing.Parse,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
        };
        
        private static TypeLookup GetLookup(string name)
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

        public static void Initialize(string dir)
        {
            LoadXml(Path.Combine(dir, DefaultTypesName));
            LoadXml(Path.Combine(dir, UserTypesName));
        }

        public static bool IsTypeKnown(int hash)
        {
            return (m_attrTypes.ContainsKey(hash) || m_userTypes.ContainsKey(hash));
        }
        
        public static DataType GetType(int hash)
        {
            if (m_attrTypes.ContainsKey(hash))
                return m_attrTypes[hash];
            if (m_userTypes.ContainsKey(hash))
                return m_userTypes[hash];

            return DataType.BinHex;
        }

        public static void RegisterType(string name, DataType type)
        {
            var hash = StringHasher.GetHash(name);

            if (!m_userTypes.ContainsKey(hash))
                m_userTypes.Add(hash, type);
        }

        public static void LoadXml(string xmlName)
        {
            var file = Path.Combine(Environment.CurrentDirectory, xmlName);

            using (var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var xml = XmlReader.Create(fs, xmlAttrTypeSettings))
            {
                if (!xml.ReadToFollowing("AttributeTypes"))
                    throw new XmlException("Attribute types data corrupted!!");

                var kind = xml.GetAttribute("Kind");
                var lookup = GetLookup(kind);

                if (lookup == null)
                    throw new XmlException("Cannot load attribute types due to unknown 'Kind' parameter!");

                var groupType = DataType.BinHex;
                var inGroup = false;

                while (xml.Read())
                {
                    switch (xml.Name)
                    {
                    case "AttributeGroup":
                        {
                            // reset current group type
                            if (inGroup)
                                groupType = DataType.BinHex;

                            var type = xml.GetAttribute("Type");

                            if (type != null)
                                groupType = DataTypeValue.Parse(xml.GetAttribute("Type"));

                            inGroup = true;
                        }
                        continue;
                    case "Attribute":
                        {
                            var name = xml.GetAttribute("Name");
                            var hash = xml.GetAttribute("Hash");
                            var type = xml.GetAttribute("Type");

                            var attrType = DataType.BinHex;
                            var attrHash = (hash != null) ? int.Parse(hash, NumberStyles.HexNumber) : -1;

                            if (inGroup)
                                attrType = groupType;
                            if (type != null)
                                attrType = DataTypeValue.Parse(type);

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
                                    if (IsTypeKnown(attrHash))
                                    {
                                        var knownType = GetType(attrHash);

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
                        }
                        continue;
                    }
                }
            }
        }
    }
}
