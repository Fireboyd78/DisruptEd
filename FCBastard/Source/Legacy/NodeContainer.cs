using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomad
{
    public enum ContainerType : short
    {
        Objects = 0x0003,
        Classes = 0x4005,
    }

    public class NodeContainer : IResourceFile
    {
        public static readonly Identifier Magic = "FCbn";
        public static readonly int BufferSize = Utils.GetSizeInMB(16);

        public ContainerType Type { get; set; }

        public Node Root { get; set; }

        public void Serialize(BinaryStream stream)
        {
            var nodesCount = 0;
            var attrsCount = 0;

            switch (Type)
            {
            case ContainerType.Objects:
                {
                    var root = (NodeObject)Root;
                    nodesCount = Utils.GetTotalNumberOfNodes(root);
                    attrsCount = root.Attributes.Count;
                }
                break;
            case ContainerType.Classes:
                {
                    var root = (NodeClass)Root;
                    nodesCount = Utils.GetTotalNumberOfNodes(root);
                    attrsCount = root.Attributes.Count;
                }
                break;
            }

            if (attrsCount == 0)
                attrsCount = 1;

            var totalCount = (nodesCount + attrsCount);

            Debug.WriteLine(">> Writing FCB header...");
            stream.Write((int)Magic);

            stream.Write((short)Type);
            stream.Write((short)MagicNumber.FB); // ;)

            stream.Write(totalCount);
            stream.Write(nodesCount);

            Debug.WriteLine(">> Writing data...");
            Root.Serialize(stream);
        }

        public void Deserialize(BinaryStream stream)
        {
            Debug.WriteLine(">> Reading FCB header...");
            var magic = stream.ReadInt32();

            if (magic != Magic)
                throw new InvalidOperationException("Bad magic, no FCB data to parse!");

            Type = (ContainerType)stream.ReadInt16();

            stream.Position += 2; // ;)

            var totalCount = stream.ReadInt32();
            var nodesCount = stream.ReadInt32();
            
            // read fcb data
            switch (Type)
            {
            case ContainerType.Objects:
                {
                    Debug.WriteLine(">> Reading objects...");

                    var objRefs = new List<NodeObject>();
                    Root = new NodeObject(stream, objRefs);
                }
                break;
            case ContainerType.Classes:
                {
                    Debug.WriteLine(">> Reading classes...");
                    Root = new NodeClass(stream);
                }
                break;
            }
        }

        public void LoadBinary(string filename)
        {
            using (var stream = new BinaryStream(filename))
            {
                Deserialize(stream);
            }
        }

        public void SaveBinary(string filename)
        {
            using (var stream = new BinaryStream(BufferSize))
            {
                Debug.WriteLine(">> Serializing binary data...");
                Serialize(stream);

                var size = (int)stream.Position;

                Debug.WriteLine(">> Writing to file...");
                stream.WriteFile(filename, size);
            }
        }

        public void LoadXml(string filename)
        {
            throw new NotImplementedException();
        }
        
        public void SaveXml(string fileanme)
        {
            throw new NotImplementedException();
        }
    }
}
