using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Nomad
{
    using TypeLookup = Dictionary<int, DataType>;
    
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

        public static bool IsTypeKnown(StringId id)
        {
            return (m_attrTypes.ContainsKey(id) || m_userTypes.ContainsKey(id));
        }
        
        public static DataType GetType(StringId id)
        {
            if (m_attrTypes.ContainsKey(id))
                return m_attrTypes[id];
            if (m_userTypes.ContainsKey(id))
                return m_userTypes[id];

            return DataType.BinHex;
        }
        
        public static bool TryGetType(StringId id, out DataType type)
        {
            if (m_attrTypes.TryGetValue(id, out type) || m_userTypes.TryGetValue(id, out type))
                return true;

            type = DataType.BinHex;
            return false;
        }

        public static DataType GetBestType(StringId id, string className)
        {
            var type = GetType(id);

            // try resolving the full name, e.g. 'Class.bProperty'
            StringId altId = $"{className}.{id}";

            if (altId != id)
            {
                var altType = DataType.BinHex;

                if (TryGetType(altId, out altType))
                    type = altType;
            }

            return type;
        }

        public static void RegisterType(string name, DataType type)
        {
            var hash = StringHasher.GetHash(name);

            if (!m_userTypes.ContainsKey(hash))
                m_userTypes.Add(hash, type);
        }

        public static void RegisterGuessType(StringId id, DataType type)
        {
            if (!m_userTypes.ContainsKey(id))
            {
                m_userTypes.Add(id, type);
                Debug.WriteLine($"<!-- GUESS: --><Attribute Name=\"{id}\" Type=\"{type.ToString()}\" />");
            }
        }

        private static bool RegisterTypeToLookup(TypeLookup lookup, string name, int hash, DataType type)
        {
            if (name != null)
            {
                if (hash != -1)
                {
                    // add manual lookup
                    StringHasher.AddToLookup(hash, name);
                }
                else
                {
                    hash = StringHasher.GetHash(name);

                    // try adding this to the lookup
                    if (!StringHasher.CanResolveHash(hash))
                    {
                        Debug.WriteLine($"- Adding '{name}' to lookup");
                        StringHasher.AddToLookup(name);
                    }
                }

                if (!lookup.ContainsKey(hash))
                    lookup.Add(hash, type);

                var id = name;
                var parentId = "";

                var splitIdx = name.LastIndexOf('.');

                if (splitIdx != -1)
                {
                    parentId = name.Substring(0, splitIdx);
                    id = name.Substring(splitIdx + 1);

                    StringHasher.AddToLookup(parentId);
                    StringHasher.AddToLookup(id);
                }

                // auto-register accompanying "text_*" string
                if ((type == DataType.StringId) || (type == DataType.PathId))
                {
                    id = $"text_{id}";

                    if (!String.IsNullOrEmpty(parentId))
                        id = $"{parentId}.{id}";

                    RegisterTypeToLookup(lookup, id, -1, DataType.String);
                }
            }
            else
            {
                // empty attribute!?
                if (hash == -1)
                    return false;

                //--var canResolve = StringHasher.CanResolveHash(hash);
                //--
                //--name = (canResolve)
                //--    ? StringHasher.ResolveHash(hash)
                //--    : $"_{hash:X8}";
                //--
                //--if (canResolve)
                //--{
                //--    if (IsTypeKnown(hash))
                //--    {
                //--        var knownType = GetType(hash);
                //--
                //--        //WriteUniqueHint($"<!-- Remove: --><Attribute Hash=\"{attrHash:X8}\" Type=\"{attrType.ToString()}\" /><!-- SameAs --><Attribute Name=\"{name}\" Type=\"{knownType.ToString()}\" />");
                //--    }
                //--    else
                //--    {
                //--        //WriteUniqueHint($"<!-- Rename: --><Attribute Hash=\"{attrHash:X8}\" Type=\"{attrType.ToString()}\" /><!-- EqualTo --><Attribute Name=\"{name}\" Type=\"{attrType.ToString()}\" />");
                //--    }
                //--}

                if (!lookup.ContainsKey(hash))
                    lookup.Add(hash, type);
            }
            
            return true;
        }

        public static void LoadXml(string xmlFile)
        {
            using (var fs = File.Open(xmlFile, FileMode.Open, FileAccess.Read, FileShare.Read))
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
                                groupType = EnumValue<DataType>.Parse(xml.GetAttribute("Type"));

                            inGroup = true;
                        } continue;
                    case "Attribute":
                        {
                            var hashAttr = xml.GetAttribute("Hash");
                            var typeAttr = xml.GetAttribute("Type");

                            var name = xml.GetAttribute("Name");
                            var type = DataType.BinHex;
                            var hash = (hashAttr != null) ? int.Parse(hashAttr, NumberStyles.HexNumber) : -1;

                            if (inGroup)
                                type = groupType;
                            if (typeAttr != null)
                                type = EnumValue<DataType>.Parse(typeAttr);

                            RegisterTypeToLookup(lookup, name, hash, type);
                        } continue;
                    }
                }
            }
        }
    }
}
