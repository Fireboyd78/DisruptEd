using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DisruptEd.IO
{
    public class NodeReference
    {
        public string Name { get; set; }

        public long UID { get; set; }

        public NodeClass Node { get; set; }

        public int TotalCount
        {
            get
            {
                var nodes = Utils.GetTotalNumberOfNodes(Node);
                var attrs = Node.Attributes.Count;

                if (attrs == 0)
                    attrs = 1;

                return (nodes + attrs);
            }
        }

        public int NodesCount
        {
            get
            {
                return (Node != null) ? Utils.GetTotalNumberOfNodes(Node) : 0;
            }
        }

        public void Serialize(XmlElement xml)
        {
            var xmlDoc = xml.OwnerDocument;
            var elem = xmlDoc.CreateElement("Node");

            elem.SetAttribute("UID", Utils.Bytes2HexString(BitConverter.GetBytes(UID)));

            if (!String.IsNullOrEmpty(Name))
                elem.SetAttribute("Name", Name);

            Node.Serialize(elem);
            xml.AppendChild(elem);
        }
    }

    public class NodeLibrary : IBinarySerializer
    {
        static readonly MagicNumber Magic = "nbCF";
        static readonly int Type = 0x4005;
        
        public int TotalCount { get; set; }
        public int NodesCount { get; set; }

        public NodeClass Root { get; set; }

        public List<NodeReference> References { get; set; }

        public void Serialize(BinaryStream stream)
        {
            var nInfos = References.Count;

            // we need to write the offset to our infos here
            var ptr = stream.Position;
            stream.Position += 4;

            stream.Write(nInfos);
            stream.Write((int)Magic);

            stream.Write((short)Type);
            stream.Write((short)MagicNumber.FB); // ;)

            var nodesCount = Utils.GetTotalNumberOfNodes(Root);
            var attrCount = Root.Attributes.Count;

            if (attrCount == 0)
                attrCount = 1;

            var totalCount = (nodesCount + attrCount);
            
            stream.Write(totalCount);
            stream.Write(nodesCount);

            Root.Serialize(stream);

            var infosOffset = (int)(Memory.Align(stream.Position, 8) - ptr);
            
            // write the infos offset
            stream.Position = ptr;
            stream.Write(infosOffset);

            // write the infos
            stream.Position = infosOffset;

            foreach (var info in References)
            {
                var offset = info.Node.Offset - 8;

                stream.Write(info.UID);
                stream.Write(offset);
                stream.Write((short)info.TotalCount);
                stream.Write((short)info.NodesCount);
            }
        }

        public void Serialize(XmlDocument xml)
        {
            var elem = xml.CreateElement("EntityLibraries");

            foreach (var nodeRef in References)
                nodeRef.Serialize(elem);

            xml.AppendChild(elem);
        }

        public void Deserialize(BinaryStream stream)
        {
            var infosOffset = stream.ReadInt32();
            var infosCount = stream.ReadInt32();

            var magic = stream.ReadInt32();

            if (magic != Magic)
                throw new InvalidOperationException("Bad magic, no FCB data to parse!");

            var type = stream.ReadInt16();

            if (type != Type)
                throw new InvalidOperationException("FCB library reported the incorrect type?!");

            stream.Position += 2; // ;)

            TotalCount = stream.ReadInt32(); // * 3
            NodesCount = stream.ReadInt32(); // * 4

            var memSize = ((TotalCount * 3) + NodesCount) * 4;
            var memSizeAlign = Memory.Align(memSize, 16);

            Console.WriteLine("[Library.Header]");
            Console.WriteLine($"  Total: {TotalCount}");
            Console.WriteLine($"  Nodes: {NodesCount}");
            Console.WriteLine($"  MemSize: {memSize:X8}");
            
            // read fcb data
            Root = new NodeClass(stream);

            var entries = new Dictionary<int, NodeClass>();

            foreach (var library in Root.Children)
            {
                foreach (var entry in library.Children)
                {
                    entries.Add(entry.Offset, entry);
                }
            }

            // read infos
            stream.Position = infosOffset;

            var nInfosTotal = 0;
            var nInfosNodes = 0;
            
            for (int i = 0; i < infosCount; i++)
            {
                var id = stream.ReadInt64();
                var offset = stream.ReadInt32() + 8;
                var total = stream.ReadInt16();
                var nodes = stream.ReadInt16();

                if (!entries.ContainsKey(offset))
                    throw new InvalidOperationException("YOU REALLY FUCKED UP THIS TIME!");

                var info = new NodeReference() {
                    UID = id,
                    Node = entries[offset],
                };

                nInfosTotal += total;
                nInfosNodes += nodes;

                References.Add(info);
            }
            
            var count1Diff = (TotalCount - nInfosTotal);
            var count2Diff = (NodesCount - nInfosNodes);

            Console.WriteLine("[Library.Infos]");
            Console.WriteLine($"  Total: {nInfosTotal}");
            Console.WriteLine($"  Nodes: {nInfosNodes}");
            Console.WriteLine("[Library.Logging]");
            Console.WriteLine($"  TotalDiff: {count1Diff}");
            Console.WriteLine($"  NodesDiff: {count2Diff}");

            Console.WriteLine($"Finished reading {References.Count} libraries. Collected {Utils.GetTotalNumberOfNodes(Root)} nodes in total.");
        }

        public NodeLibrary()
        {
            Root = new NodeClass("EntityLibraries");
            References = new List<NodeReference>();
        }

        public NodeLibrary(BinaryStream stream)
            : this()
        {
            Deserialize(stream);
        }
    }
}
