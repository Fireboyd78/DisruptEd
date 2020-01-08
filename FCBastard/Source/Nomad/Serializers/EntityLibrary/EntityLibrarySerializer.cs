using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using System.Xml;
using System.Xml.Linq;

namespace Nomad
{
    public class EntityLibrarySerializer : NomadResourceSerializer, INomadXmlFileSerializer
    {
        public override FileType Type => FileType.Binary;
        
        public SortedDictionary<long, NomadObject> Prototypes { get; set; }

        public NomadObject Root { get; set; }

        public bool Use64Bit { get; set; }
        
        protected void ReadPrototypeEntry(BinaryStream stream)
        {
            long uid = 0;

            if (Use64Bit)
            {
                uid = stream.ReadInt64();
            }
            else
            {
                uid = stream.ReadUInt32();
            }

            var buffer = (Use64Bit)
                ? BitConverter.GetBytes(uid)
                : BitConverter.GetBytes((uint)uid);
            
            var lookup = new EntityPrototypeInfo(stream, 8);
            var obj = Context.GetRefByPtr(lookup.Offset) as NomadObject;

            if (obj == null)
                throw new InvalidDataException($"Couldn't find library '{uid:X8}' at offset {lookup.Offset:X8}!");

            // remove before writing
            var libId = new NomadValue("UID", DataType.BinHex, buffer);

            obj.Attributes.Add(libId);

            Prototypes.Add(uid, obj);
        }

        public override NomadObject Deserialize(Stream stream)
        {
            Context.Begin();

            var bs = (stream as BinaryStream)
                ?? new BinaryStream(stream);

            var infosOffset = bs.ReadInt32();
            var infosCount = bs.ReadInt32();

            Use64Bit = (bs.Length - (infosCount * 0xC)) != infosOffset;

            // deserialize the root object
            Root = base.Deserialize(stream);
            
            bs.Position = infosOffset;

            Context.Log("Processing prototypes...");
            for (int i = 0; i < infosCount; i++)
                ReadPrototypeEntry(bs);

            Context.End();

            return Root;
        }
        
        public override void Serialize(Stream stream, NomadObject _unused)
        {
            Context.Begin();

            var bs = (stream as BinaryStream)
                ?? new BinaryStream(stream);
            
            byte[] buffer = null;

            using (var nb = new BinaryStream(1024))
            {
                base.Serialize(nb, Root);
                buffer = nb.ToArray();
            }

            var count = Prototypes.Count;

            bs.Write(buffer.Length + 8);
            bs.Write(count);

            bs.Write(buffer);
            
            // todo: prototype entries
            Context.Log("Writing prototypes...");
            foreach (var kv in Prototypes)
            {
                var uid = kv.Key;
                var obj = kv.Value;

                // fail-safe
                var reference = new NomadReference(obj);
                var cached = reference.Get();

                var ptr = (int)Context.GetPtr(cached);

                if (ptr == -1)
                    throw new InvalidDataException("Couldn't get the pointer to a prototype!");
                
                var info = new EntityPrototypeInfo(obj, ptr);

                var uidBuffer = (Use64Bit)
                    ? BitConverter.GetBytes(uid)
                    : BitConverter.GetBytes((uint)uid);

                bs.Write(uidBuffer);

                info.Serialize(bs);
            }

            Context.End();
        }

        public void LoadXml(string filename)
        {
            Prototypes = new SortedDictionary<long, NomadObject>();

            var inputDir = Path.GetDirectoryName(filename);

            var xml = new XmlDocument();
            xml.Load(filename);

            var libsElem = xml.DocumentElement;

            if ((libsElem == null) || (libsElem.Name != "EntityLibraries"))
                throw new InvalidOperationException("Not a valid EntityLibraries node");

            var versionAttr = new AttributeData(DataType.Int32, libsElem.GetAttribute("Version"));
            var version = versionAttr.ToInt32();

            if (version == 0)
                throw new InvalidDataException("Invalid/Missing 'Version' attribute!");
            
            Use64Bit = (version == 2);

            Root = new NomadObject("EntityLibraries");

            Context.Log("Loading libraries...");
            foreach (var node in libsElem.ChildNodes.OfType<XmlElement>())
            {
                if (node.Name != "EntityLibrary")
                    throw new InvalidDataException($"What the hell do I do with a '{node.Name}' element?!");

                var libName = node.GetAttribute("Name");

                if (String.IsNullOrEmpty(libName))
                    throw new InvalidDataException($"EntityLibrary is malformed: no name specified!");
                
                var lib = new NomadObject("EntityLibrary");
                lib.SetAttributeValue("Name", DataType.String, libName);

                Root.Children.Add(lib);

                foreach (var childNode in node.ChildNodes.OfType<XmlElement>())
                {
                    if (childNode.Name != "Include")
                        throw new InvalidDataException($"EntityLibrary['{libName.ToString()}'] is malformed: unexpected element '{childNode.Name}'");

                    var pathAttr = childNode.GetAttribute("Path");

                    if (String.IsNullOrEmpty(pathAttr))
                        throw new InvalidDataException($"EntityLibrary['{libName.ToString()}'] is malformed: include element is missing path!");
                    
                    var libFile = Path.Combine(inputDir, pathAttr);

                    Context.LogDebug($"Loading archetype '{libFile}'...");
                    var arkRes = ResourceFactory.Open(libFile);

                    var ark = arkRes.Root;
                    var arkId = ark.GetAttribute("UID");

                    long uid = 0;

                    if (Use64Bit)
                    {
                        uid = BitConverter.ToInt64(arkId.Data.Buffer, 0);
                    }
                    else
                    {
                        uid = BitConverter.ToUInt32(arkId.Data.Buffer, 0);
                    }

                    // remove attributes
                    ark.Attributes.Clear();

                    Prototypes.Add(uid, ark);
                    lib.Children.Add(ark);
                }
            }
        }

        public void SaveXml(string filename)
        {
            var rootDir = Path.GetDirectoryName(filename);

            var libsDir = "libraries";
            var libsPath = Path.Combine(rootDir, libsDir);

            if (!Directory.Exists(libsPath))
                Directory.CreateDirectory(libsPath);

            var xml = new XmlDocument();
            var libsElem = xml.CreateElement("EntityLibraries");

            var version = 1;

            if (Use64Bit)
                version = 2;

            libsElem.SetAttribute("Version", version.ToString());

            var libs = new Dictionary<string, List<string>>();

            Context.Log("Splitting libraries...");
            foreach (var lib in Root.Children)
            {
                if (lib.Id != "EntityLibrary")
                    throw new InvalidDataException($"Expected an EntityLibrary but got '{lib.Id}' instead.");

                var libName = lib.GetAttributeValue("Name");

                if (libName == null)
                    throw new InvalidDataException($"Couldn't get name for EntityLibrary!");

                var arkList = new List<string>();

                foreach (var child in lib.Children)
                {
                    if (child.Id != "EntityPrototype")
                        throw new InvalidDataException($"Expected an EntityPrototype but got '{child.Id}' instead.");

                    var arkArk = child.GetChild("Entity");
                    var arkName = arkArk.GetAttributeValue("hidName");

                    if (String.IsNullOrEmpty(arkName))
                        throw new InvalidOperationException("Can't figure out EntityPrototype name!");

                    var arkPath = arkName.Replace('.', '\\');
                    var arkFile = Path.Combine(libsDir, $"{arkPath}.xml");

                    // attempt to serialize data
                    var arkRes = ResourceFactory.Create<NomadXmlSerializer>(child);
                    arkRes.Save(Path.Combine(rootDir, arkFile));

                    arkList.Add(arkFile);
                }

                libs.Add(libName, arkList);
            }

            Context.Log("Saving XML...");
            foreach (var lib in libs)
            {
                var name = lib.Key;
                var files = lib.Value;

                // write import statement
                var import = xml.CreateElement("EntityLibrary");

                import.SetAttribute("Name", name);

                foreach (var ent in files)
                {
                    var include = xml.CreateElement("Include");

                    include.SetAttribute("Path", ent);

                    import.AppendChild(include);
                }

                libsElem.AppendChild(import);
            }

            xml.AppendChild(libsElem);
            xml.SaveFormatted(filename);
        }

        public EntityLibrarySerializer()
            : base()
        {
            Format = FormatType.Entities;
            Prototypes = new SortedDictionary<long, NomadObject>();
        }
    }
}
