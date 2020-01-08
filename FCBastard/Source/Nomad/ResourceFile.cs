using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Nomad
{
    public class ResourceFile
    {
        public FileType Type { get; set; }
        
        public NomadObject Root { get; set; }
        
        public ResourceFilter CustomFilter { get; set; }
        public INomadSerializer CustomSerializer { get; set; }
        
        public ResourceFilter GetFilter()
        {
            if (CustomFilter != null)
                return CustomFilter;

            ResourceFilter filter = null;

            if (Root != null)
                filter = ResourceFactory.GetFilter(Root);

            return filter;
        }

        public INomadSerializer GetSerializer()
        {
            if (CustomSerializer != null)
                return CustomSerializer;

            INomadSerializer serializer = null;
            
            var filter = GetFilter();

            if (filter != null)
            {
                serializer = ResourceFactory.GetSerializer(filter);
                serializer.Format = ResourceFactory.GetFormat(filter.Version);
            }

            // need generic serializer?
            if (serializer == null)
                serializer = new NomadResourceSerializer();
            
            return serializer;
        }

        public bool Load(Stream stream)
        {
            var serializer = GetSerializer();
            
            if (serializer != null)
            {
                if (Type == FileType.Xml)
                {
                    if (serializer is INomadXmlFileSerializer)
                        throw new InvalidOperationException("Can't load XML data from a stream; serializer has external dependencies");

                    // make sure we have an XML serializer
                    if (serializer.Type != FileType.Xml)
                        serializer = new NomadXmlSerializer();
                }

                Root = serializer.Deserialize(stream);
                return true;
            }

            // couldn't deserialize data
            return false;
        }

        public bool Load(string filename)
        {
            if (Type == FileType.Xml)
            {
                var serializer = GetSerializer() as INomadXmlFileSerializer;

                if (serializer != null)
                {
                    serializer.LoadXml(filename);
                    return true;
                }
            }

            using (var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // load from filestream
                return Load(fs);
            }
        }
        
        public bool Save(Stream stream)
        {
            var serializer = GetSerializer();
            
            if (serializer != null)
            {
                if (Type == FileType.Xml)
                {
                    if (serializer is INomadXmlFileSerializer)
                        throw new InvalidOperationException("Can't save XML data to a stream; serializer has external dependencies");

                    // make sure we have an XML serializer
                    if (serializer.Type != FileType.Xml)
                        serializer = new NomadXmlSerializer();
                }

                serializer.Serialize(stream, Root);
                return true;
            }

            Debug.WriteLine("Attempted to serialize to a stream but no serializer was present.");
            return false;
        }

        public bool Save(string filename)
        {
            if (Type == FileType.Xml)
            {
                var serializer = GetSerializer() as INomadXmlFileSerializer;

                if (serializer != null)
                {
                    serializer.SaveXml(filename);
                    return true;
                }
            }

            byte[] buffer = null;

            using (var bs = new BinaryStream(1024))
            {
                if (Save(bs))
                    buffer = bs.ToArray();
            }

            if (buffer != null)
            {
                var outDir = Path.GetDirectoryName(filename);

                if (!Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);
                
                File.WriteAllBytes(filename, buffer);
                return true;
            }

            // couldn't serialize data
            return false;
        }
    }
}
