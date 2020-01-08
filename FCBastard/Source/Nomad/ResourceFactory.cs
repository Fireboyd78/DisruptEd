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

namespace Nomad
{
    public static class ResourceFactory
    {
        static List<ResourceFilter> _filters = null;

        public static IEnumerable<ResourceFilter> Filters
        {
            get { return _filters; }
        }

        public static void Initialize(string dir)
        {
            var xmlFile = Path.Combine(dir, "filters.xml");

            var xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlFile);

            var root = xmlDoc.DocumentElement;

            if (root.Name != "ResourceFilters")
                throw new XmlException("Resource filter configuration is invalid.");

            _filters = new List<ResourceFilter>();

            foreach (var child in root.ChildNodes.OfType<XmlElement>())
            {
                if (child.Name != "Resource")
                    throw new XmlException($"Unknown element '{child.Name}' in resource filter configuration.");

                var filter = ResourceFilter.LoadXml(child);

                _filters.Add(filter);
            }
        }

        public static FormatType GetFormat(int version)
        {
            switch (version)
            {
            case 1: return FormatType.RML;
            case 2: return FormatType.Resource;
            case 3: return FormatType.Objects;
            case 5: return FormatType.Entities;
            }

            return FormatType.Resource;
        }
        
        public static ResourceFilter GetFilter(XElement root)
        {
            foreach (var filter in _filters)
            {
                if (filter.Matches(root))
                    return filter;
            }

            return null;
        }
        
        public static ResourceFilter GetFilter(NomadObject root)
        {
            foreach (var filter in _filters)
            {
                if (filter.Matches(root))
                    return filter;
            }

            return null;
        }

        public static ResourceFilter GetFilter(NomadFileInfo info)
        {
            foreach (var filter in _filters)
            {
                if ((filter.Version == info.Version)
                    && (filter.Name == info.RootId))
                {
                    return filter;
                }
            }

            return null;
        }

        public static INomadSerializer GetSerializer(FormatType type)
        {
            switch (type)
            {
            case FormatType.RML:
                return new NomadRmlSerializer();

            case FormatType.Generic:
            case FormatType.Resource:
            case FormatType.Objects:
                return new NomadResourceSerializer() {
                    Format = type,
                };

            case FormatType.Entities:
                return new EntityLibrarySerializer();
            }

            return null;
        }

        public static INomadSerializer GetSerializer(ResourceType type)
        {
            switch (type)
            {
            case ResourceType.Generic:
            case ResourceType.Archetype:
                return new NomadResourceSerializer();

            case ResourceType.ArchetypeLibrary:
                return new EntityLibrarySerializer();

            case ResourceType.FCXMap:
                return new FCXMapSerializer();

            case ResourceType.CombinedMoveFile:
                return new CombinedMoveFileSerializer();
            }

            return null;
        }

        public static INomadSerializer GetSerializer(ResourceFilter filter)
        {
            if (filter.Version == 1)
                return new NomadRmlSerializer();

            INomadSerializer serializer = GetSerializer(filter.Type);

            if (serializer == null)
                Debug.WriteLine($"Could not create a serializer using resource filter '{filter.ToString()}'");
            
            return serializer;
        }
        
        public static ResourceFile Create(NomadObject obj, FileType type, ResourceFilter filter = null)
        {
            var result = new ResourceFile() {
                Root = obj,
                Type = type,
                CustomFilter = filter,
            };
            
            return result;
        }

        public static ResourceFile Create(NomadObject obj, Type serializerType, ResourceFilter filter = null)
        {
            var serializer = Activator.CreateInstance(serializerType) as INomadSerializer;

            if (serializer == null)
                throw new InvalidOperationException("Tried creating a serializer from a non-serializer type!");

            var result = new ResourceFile() {
                Root = obj,
                Type = serializer.Type,
                CustomFilter = filter,
                CustomSerializer = serializer,
            };

            return result;
        }

        public static ResourceFile Create<TSerializer>(NomadObject obj, ResourceFilter filter = null)
            where TSerializer : INomadSerializer, new()
        {
            var serializer = new TSerializer();

            var result = new ResourceFile() {
                Root = obj,
                Type = serializer.Type,
                CustomSerializer = serializer,
            };

            return result;
        }
        
        public static ResourceFile Open(string filename, bool load = true)
        {
            var type = FileType.Binary;
            var flags = FileTypeFlags.None;

            var ext = Path.GetExtension(filename);

            switch (ext)
            {
            case ".rml":
                flags |= FileTypeFlags.Rml;
                break;
            case ".xml":
                type = FileType.Xml;
                break;
            }
            
            var isRml = flags.HasFlag(FileTypeFlags.Rml);
            var isXml = (type == FileType.Xml || isRml) && Utils.IsXmlFile(filename);

            INomadSerializer serializer = null;
            var valid = true;

            ResourceFilter filter = null;

            if (isXml)
            {
                serializer = new NomadXmlSerializer();

                var xml = Utils.LoadXmlFile(filename);

                filter = GetFilter(xml.Root);

                if (filter != null)
                {
                    // is there a serializer that requires external dependencies?
                    var xmlSerializer = GetSerializer(filter) as INomadXmlFileSerializer;

                    if (xmlSerializer != null)
                        serializer = xmlSerializer;
                }
            }
            else
            {
                if (NomadFactory.GetBinaryInfo(filename, out NomadFileInfo info))
                {
                    serializer = NomadFactory.GetSerializer(info);
                    filter = GetFilter(info);
                }

                // make sure it's not an empty file
                valid = info.IsValid;
            }

            ResourceFile result = null;

            if (valid)
            {
                result = new ResourceFile() {
                    Type = type,
                    CustomSerializer = serializer,
                    CustomFilter = filter,
                };
                
                if (load)
                    result.Load(filename);
            }

            return result;
        }
    }
}
