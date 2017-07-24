using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DisruptEd.IO
{
    public struct EntityReferenceData : IBinarySerializer
    {
        public long UID;

        public int Offset;
        public int TotalCount;
        public int NodesCount;

        public bool Use32Bit { get; set; }

        public void Serialize(BinaryStream stream)
        {
            if (Use32Bit)
            {
                var uid32 = (int)(UID & 0xFFFFFFFF);
                stream.Write(uid32);
            }
            else
            {
                stream.Write(UID);
            }

            stream.Write(Offset - 8);
            stream.Write((ushort)TotalCount);
            stream.Write((ushort)NodesCount);
        }

        public void Deserialize(BinaryStream stream)
        {
            if (Use32Bit)
            {
                UID = stream.ReadUInt32();
            }
            else
            {
                UID = stream.ReadInt64();
            }

            Offset = stream.ReadInt32() + 8;
            TotalCount = stream.ReadUInt16();
            NodesCount = stream.ReadUInt16();
        }

        public EntityReferenceData(BinaryStream stream, bool use32Bit)
            : this()
        {
            Use32Bit = use32Bit;
            Deserialize(stream);
        }

        public EntityReferenceData(EntityReference reference)
            : this()
        {
            var node = reference.GroupNode;

            var nodesCount = Utils.GetTotalNumberOfNodes(node);
            var attrCount = node.Attributes.Count;

            if (attrCount == 0)
                attrCount = 1;

            var totalCount = (nodesCount + attrCount);

            if (nodesCount > 65535)
                throw new InvalidOperationException("Too many nodes in entity reference.");
            if (totalCount > 65535)
                throw new InvalidOperationException("Too many total nodes+attributes in entity reference.");

            Use32Bit = reference.Use32Bit;
            
            UID = (Use32Bit) ? reference.UID32 : reference.UID;
            Offset = node.Offset;
            
            TotalCount = totalCount;
            NodesCount = nodesCount;
        }
    }

    public class EntityReference
    {
        public long UID { get; set; }

        public int UID32
        {
            get { return (int)(UID & 0xFFFFFFFF); }
        }

        public bool Use32Bit { get; set; }

        // TODO: fix this crap
        public NodeClass GroupNode { get; set; }
        public NodeClass EntityNode { get; set; }
        
        public void Serialize(XmlElement xml)
        {
            var xmlDoc = xml.OwnerDocument;
            var elem = xmlDoc.CreateElement("EntityReference");
            
            var uidHex = (Use32Bit) ? BitConverter.GetBytes(UID32) : BitConverter.GetBytes(UID);

            elem.SetAttribute("UID", Utils.Bytes2HexString(uidHex));

            EntityNode.Serialize(elem);
            xml.AppendChild(elem);
        }

        public void Deserialize(XmlElement xml)
        {
            var attrs = xml.Attributes;
            var children = xml.ChildNodes;

            if (children.Count > 1)
                throw new InvalidOperationException("Malformed EntityReference node -- too many children!");

            var entNode = xml.FirstChild as XmlElement;

            if (entNode == null)
                throw new InvalidOperationException("Malformed EntityReference node -- could not get the Entity node!");
            
            if (attrs.Count != 0)
            {
                var uidAttr = attrs.GetNamedItem("UID");

                if (uidAttr != null)
                {
                    var uidHex = AttributeData.Parse(uidAttr.Value);
                    
                    if (Use32Bit)
                    {
                        UID = BitConverter.ToInt32(uidHex, 0);
                    }
                    else
                    {
                        UID = BitConverter.ToInt64(uidHex, 0);
                    }
                }
            }

            if (UID == 0)
                throw new InvalidOperationException("Attempted to deserialize a reference with no UID attribute!");
            
            // yuck!!!
            GroupNode = new NodeClass(0x256A1FF9);
            EntityNode = new NodeClass(entNode);

            GroupNode.Children.Add(EntityNode);
        }
    }

    public sealed class EntityLibrary : ISerializer<NodeClass>
    {
        public string Name { get; set; }
        
        public List<EntityReference> Entries { get; set; }

        public bool Use32Bit { get; set; }

        public NodeClass GetNodeClass()
        {
            var node = new NodeClass("EntityLibrary");

            if (!String.IsNullOrEmpty(Name))
            {
                node.Attributes.Add(new NodeAttribute("Name") {
                    Data = new AttributeData(DataType.String, Name)
                });
            }

            foreach (var entry in Entries)
            {
                if (entry.UID == 0)
                    throw new InvalidOperationException("Attempted to generate a library class with uninitialized entries.");

                node.Children.Add(entry.GroupNode);
            }

            return node;
        }

        public void Serialize(NodeClass parent)
        {
            var node = GetNodeClass();            
            parent.Children.Add(node);
        }

        public void Serialize(XmlDocument xml)
        {
            var parent = xml.DocumentElement;
            var elem = xml.CreateElement("EntityLibrary");

            // don't write name unless it's in a huge EntityLibraries file
            // this may never be used again, actually...
            if (parent != null)
            {
                if (!String.IsNullOrEmpty(Name))
                    elem.SetAttribute("Name", Name);
            }

            foreach (var entry in Entries)
            {
                if (entry.UID == 0)
                    throw new InvalidOperationException("Attempted to serialize a library with uninitialized entries.");

                entry.Serialize(elem);
            }

            if (parent != null)
            {
                parent.AppendChild(elem);
            }
            else
            {
                // append to the xml file itself
                xml.AppendChild(elem);
            }
        }

        public void Deserialize(XmlDocument xml)
        {
            var elem = xml.DocumentElement;

            if ((elem == null) || (elem.Name != "EntityLibrary"))
                throw new InvalidOperationException("Not a EntityLibrary node!");

            var children = elem.ChildNodes;

            Entries = new List<EntityReference>(children.Count);

            foreach (XmlElement node in children)
            {
                var entry = new EntityReference() {
                    Use32Bit = Use32Bit,
                };

                entry.Deserialize(node);   

                Entries.Add(entry);
            }
        }
        
        public void Deserialize(NodeClass node)
        {
            if (node.Attributes.Count > 0)
            {
                foreach (var attr in node.Attributes)
                {
                    if (attr.Name == "Name")
                    {
                        Name = attr.Data.ToString();
                        break;
                    }
                }
            }
            
            var nChildren = node.Children.Count;

            Entries = new List<EntityReference>(nChildren);

            // _256A1FF9 nodes
            foreach (var group in node.Children)
            {
                if (group.Children.Count != 1)
                    throw new InvalidOperationException("Houston, we got a bit of a problem...");

                var entry = new EntityReference() {
                    Use32Bit    = Use32Bit,
                    GroupNode   = group,
                    EntityNode  = group.Children[0],
                };

                Entries.Add(entry);
            }
        }
    }
    
    public class EntityLibraryCollection
    {
        static readonly MagicNumber Magic = "nbCF";
        static readonly int Type = 0x4005;

        static readonly int BufferSize = Utils.GetSizeInMB(16);
        
        public List<EntityLibrary> Libraries { get; set; }

        public bool Use32Bit { get; set; }

        public List<EntityReference> GetEntityReferences(bool sorted)
        {
            if (Libraries == null)
                throw new NullReferenceException("Libraries not initialized.");

            var refs = new List<EntityReference>();
            
            foreach (var library in Libraries)
                refs.AddRange(library.Entries);
            
            return (sorted) ? refs.OrderBy((e) => (ulong)e.UID).ToList() : refs;
        }

        public NodeClass GetNodeClass()
        {
            var root = new NodeClass("EntityLibraries");

            foreach (var library in Libraries)
            {
                var node = library.GetNodeClass();
                root.Children.Add(node);
            }

            return root;
        }
        
        public void LoadBinary(string filename)
        {
            using (var stream = new BinaryStream(filename))
            {
                Debug.WriteLine(">> Reading infos header...");
                var infosOffset = stream.ReadInt32();
                var infosCount = stream.ReadInt32();

                Use32Bit = ((stream.Length - (infosCount * 0xC)) == infosOffset);

                Debug.WriteLine(">> Reading FCB header...");
                var magic = stream.ReadInt32();

                if (magic != Magic)
                    throw new InvalidOperationException("Bad magic, no FCB data to parse!");

                var type = stream.ReadInt16();

                if (type != Type)
                    throw new InvalidOperationException("FCB library reported the incorrect type?!");

                stream.Position += 2; // ;)

                var totalCount = stream.ReadInt32(); // * 3
                var nodesCount = stream.ReadInt32(); // * 4

                var dataOffset = (int)stream.Position;

                var memSize = ((totalCount * 3) + nodesCount) * 4;
                var memSizeAlign = Memory.Align(memSize, 16);

#if DEBUG
            Console.WriteLine("[Library.Header]");
            Console.WriteLine($"  Total: {totalCount}");
            Console.WriteLine($"  Nodes: {nodesCount}");
            Console.WriteLine($"  MemSize: {memSize:X8}");
#endif

                // read the infos first!
                Debug.WriteLine(">> Reading infos...");
                stream.Position = infosOffset;

                var nInfosTotal = 0;
                var nInfosNodes = 0;

                var refDatas = new Dictionary<int, EntityReferenceData>(infosCount);
                
                for (int i = 0; i < infosCount; i++)
                {
                    var refData = new EntityReferenceData(stream, Use32Bit);

                    nInfosTotal += refData.TotalCount;
                    nInfosNodes += refData.NodesCount;

                    refDatas.Add(refData.Offset, refData);
                }

                var count1Diff = (totalCount - nInfosTotal);
                var count2Diff = (nodesCount - nInfosNodes);

#if DEBUG
            Console.WriteLine("[Library.Infos]");
            Console.WriteLine($"  Total: {nInfosTotal}");
            Console.WriteLine($"  Nodes: {nInfosNodes}");
            Console.WriteLine("[Library.Logging]");
            Console.WriteLine($"  TotalDiff: {count1Diff}");
            Console.WriteLine($"  NodesDiff: {count2Diff}");
#endif

                // read fcb data
                Debug.WriteLine(">> Reading libraries...");
                stream.Position = dataOffset;

                var root = new NodeClass(stream);

                Libraries = new List<EntityLibrary>(root.Children.Count);
                
                foreach (var library in root.Children)
                {
                    // deserialize from the class
                    var lib = new EntityLibrary() {
                        Use32Bit = Use32Bit,
                    };

                    lib.Deserialize(library);

                    // update UIDs
                    foreach (var entry in lib.Entries)
                    {
                        var node = entry.GroupNode;
                        var offset = node.Offset;

                        if (refDatas.ContainsKey(offset))
                        {
                            var entRef = refDatas[offset];
                            entry.UID = entRef.UID;
                        }
                    }

                    Libraries.Add(lib);
                }

                Console.WriteLine($"Finished reading {Libraries.Count} libraries. Collected {Utils.GetTotalNumberOfNodes(root)} nodes in total.");
            }
        }

        public void SaveBinary(string filename)
        {
            var root = GetNodeClass();

            byte[] buffer;

            Debug.WriteLine(">> Generating binary data...");
            using (var stream = new BinaryStream(BufferSize))
            {
                // list of references sorted by their UID
                var references = GetEntityReferences(true);

                // we need to write the offset to our infos here
                Debug.WriteLine(">> Writing infos header...");
                var ptr = stream.Position;
                stream.Position += 4;

                stream.Write(references.Count);

                Debug.WriteLine(">> Writing FCB header...");
                stream.Write((int)Magic);

                stream.Write((short)Type);
                stream.Write((short)MagicNumber.FB); // ;)

                var nodesCount = Utils.GetTotalNumberOfNodes(root);
                var attrCount = root.Attributes.Count;

                if (attrCount == 0)
                    attrCount = 1;

                var totalCount = (nodesCount + attrCount);

                stream.Write(totalCount);
                stream.Write(nodesCount);

                root.Serialize(stream);

                var refsOffset = (int)(Memory.Align(stream.Position, 8) - ptr);

                Debug.WriteLine(">> Writing infos offset...");
                stream.Position = ptr;
                stream.Write(refsOffset);

                Debug.WriteLine(">> Writing infos...");
                stream.Position = refsOffset;

                foreach (var reference in references)
                {
                    var refData = new EntityReferenceData(reference) {
                        Use32Bit = Use32Bit,
                    };

                    refData.Serialize(stream);
                }

                var size = (int)stream.Position;
                buffer = new byte[size];
                
                Debug.WriteLine(">> Copying to buffer...");
                stream.Position = 0;
                stream.Read(buffer, 0, size);
            }

            Debug.WriteLine(">> Writing to file...");
            File.WriteAllBytes(filename, buffer);
        }

        public void LoadXml(string filename)
        {
            Libraries = new List<EntityLibrary>();

            var root = Path.GetDirectoryName(filename);

            Debug.WriteLine(">> Loading XML file...");
            var xml = new XmlDocument();
            xml.Load(filename);

            var libsElem = xml.DocumentElement;

            if ((libsElem == null) || (libsElem.Name != "EntityLibraries"))
                throw new InvalidOperationException("Not a valid EntityLibraries node");

            var attr32Bit = new AttributeData(DataType.Bool, libsElem.GetAttribute("Use32Bit"));
            Use32Bit = attr32Bit.ToBool();

            Debug.WriteLine(">> Loading libraries...");
            foreach (var child in libsElem.ChildNodes)
            {
                var node = child as XmlElement;

                // skip anything that's not a node
                if (node == null)
                    continue;

                if (node.Name != "EntityLibrary")
                    throw new InvalidOperationException($"What the hell do I do with a '{node.Name}' element?!");

                var file = node.GetAttribute("File");

                if (file != null)
                {
                    var libName = Path.GetFileNameWithoutExtension(file);
                    var libFile = Path.Combine(root, file);

                    var libDoc = new XmlDocument();
                    libDoc.Load(libFile);

                    var library = new EntityLibrary() {
                        Use32Bit = Use32Bit,
                        Name = libName,
                    };

                   library.Deserialize(libDoc);

                    Libraries.Add(library);
                    Console.WriteLine($" > Loaded library '{libName}' via XML");
                }
                else
                {
                    throw new InvalidOperationException("Sorry, cannot load embedded EntityLibrary nodes :(");
                }
            }

            Console.WriteLine($"Processed {Libraries.Count} XML libraries and collected {Utils.GetTotalNumberOfNodes(GetNodeClass())} nodes in total.");
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

            if (Use32Bit)
                libsElem.SetAttribute("Use32Bit", "1");

            Debug.WriteLine(">> Parsing libraries...");
            foreach (var library in Libraries)
            {
                var libName = $"{library.Name}.xml";
                var libPath = Path.Combine(libsDir, libName);

                // attempt to serialize data
                var libDoc = new XmlDocument();

                library.Serialize(libDoc);

                // make sure we resolve the full path to the file
                libDoc.SaveFormatted(Path.Combine(libsPath, libName), true);

                // write import statement
                var import = xml.CreateElement("EntityLibrary");
                import.SetAttribute("File", libPath);

                libsElem.AppendChild(import);
            }

            Debug.WriteLine(">> Saving XML document...");
            xml.AppendChild(libsElem);
            xml.SaveFormatted(filename);
        }

        public EntityLibraryCollection()
        {
            Libraries = new List<EntityLibrary>();
        }
    }
}
