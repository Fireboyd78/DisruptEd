using System;

namespace Nomad
{
    [Obsolete("This class is absolute garbage and needs to be put down.")]
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
}
