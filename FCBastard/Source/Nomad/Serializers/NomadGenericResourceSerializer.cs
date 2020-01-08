using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomad
{
    public class NomadGenericResourceSerializer : NomadResourceSerializer
    {
        public NomadFileInfo Info { get; set; }

        public override NomadObject Deserialize(Stream stream)
        {
            stream.Position += Info.Offset;
            return base.Deserialize(stream);
        }

        public override void Serialize(Stream stream, NomadObject data)
        {
            throw new InvalidOperationException("Cannot serialize data a generic serializer.");
        }

        public NomadGenericResourceSerializer(NomadFileInfo info)
        {
            Info = info;
            Format = ResourceFactory.GetFormat(info.Version);
        }
    }
}
