using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Nomad
{
    public class ResourceFilter
    {
        public readonly StringId Name;

        public readonly int Version;
        public readonly ResourceType Type;

        public readonly string FileExt;

        public readonly StringId ItemName;

        public bool HasItemTemplate => (ItemName != StringId.None);
        
        public bool Matches(string value)
        {
            if (String.IsNullOrEmpty(value))
                return false;

            StringId myType = Name;

            switch (value[0])
            {
            case '.':
                return value.Equals(FileExt, StringComparison.InvariantCultureIgnoreCase);
            case ':':
                myType = ItemName;
                value = value.Substring(1);
                break;
            }
            
            return myType.Equals(value);
        }

        public bool Matches(XElement root)
        {
            if (Name != root.Name.LocalName)
                return false;

            if (HasItemTemplate)
            {
                foreach (var child in root.Elements())
                {
                    if (ItemName != child.Name.LocalName)
                        return false;
                }
            }

            return true;
        }

        public bool Matches(NomadObject root)
        {
            if (Name != root.Id)
                return false;

            if (HasItemTemplate)
            {
                foreach (var child in root.Children)
                {
                    if (ItemName != child.Id)
                        return false;
                }
            }

            return true;
        }

        public static ResourceFilter LoadXml(XmlElement elem)
        {
            var childAttrs = elem.Attributes;
            
            XmlAttribute[] attrs = {
                childAttrs["Name"]      ?? throw new XmlException("Resource filter missing name."),
                childAttrs["Version"]   ?? throw new XmlException("Resource filter missing version."),
                childAttrs["Type"]      ?? throw new XmlException("Resource filter missing type."),
                childAttrs["FileExt"]   ?? throw new XmlException("Resource filter missing file extension."),
            };
            
            return new ResourceFilter(
                name:       attrs[0].Value,
                version:    attrs[1].Value,
                type:       attrs[2].Value,
                fileExt:    attrs[3].Value,
                itemName:   elem.GetAttribute("ItemName"));
        }

        public override string ToString()
        {
            if (HasItemTemplate)
                return $"{Name}:{ItemName}({Type}, {Version}, '{FileExt}')";

            return $"{Name}({Type}, {Version}, '{FileExt}')";
        }

        public ResourceFilter(StringId name, int version, ResourceType type, string fileExt)
            : this(name, version, type, fileExt, StringId.None)
        { }

        public ResourceFilter(StringId name, string version, string type, string fileExt)
            : this(name, version, type, fileExt, StringId.None)
        { }

        public ResourceFilter(StringId name, int version, ResourceType type, string fileExt, StringId itemName)
        {
            Name = name;
            Version = version;
            Type = type;
            FileExt = fileExt;
            ItemName = itemName;
        }
        
        public ResourceFilter(StringId name, string version, string type, string fileExt, StringId itemName)
        {
            Name = name;

            if (!int.TryParse(version, out Version))
                throw new InvalidOperationException($"Invalid resource filter version '{version}'.");

            if (!Enum.TryParse(type, true, out Type))
                throw new InvalidOperationException($"Unknown resource filter type '{type}'.");
            
            FileExt = fileExt;
            ItemName = itemName;
        }
    }
}
