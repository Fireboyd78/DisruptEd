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
    public class EntityPrototypeInfo
    {
        public int Offset;
        public int TotalCount;
        public int ChildCount;

        public void Serialize(BinaryStream stream)
        {
            stream.Write(Offset);
            stream.Write((ushort)TotalCount);
            stream.Write((ushort)ChildCount);
        }

        public void Deserialize(BinaryStream stream)
        {
            Offset = stream.ReadInt32();
            TotalCount = stream.ReadUInt16();
            ChildCount = stream.ReadUInt16();
        }

        public EntityPrototypeInfo(BinaryStream stream, int baseOffset = 0)
        {
            Deserialize(stream);
            Offset += baseOffset;
        }

        public EntityPrototypeInfo(NomadObject obj, int offset)
        {
            var childCount = 0;
            var attrCount = obj.Attributes.Count;

            if (attrCount == 0)
                attrCount = 1;

            foreach (var child in obj)
            {
                if (child.IsObject)
                    childCount++;
            }

            Offset = offset;

            TotalCount = (childCount + attrCount);
            ChildCount = childCount;
        }
    }
}
