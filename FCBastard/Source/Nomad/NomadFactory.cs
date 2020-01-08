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
    public static class NomadFactory
    {
        public static bool GetBinaryInfo(string filename, out NomadFileInfo info)
        {
            info = new NomadFileInfo();

            using (var bs = new BinaryStream(filename, FileMode.Open, FileAccess.Read))
            {
                try
                {
                    info.Deserialize(bs);

                    return info.IsValid;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Failed to get binary info for file '{filename}' -- {e.Message}");
                }
            }

            return false;
        }

        public static INomadSerializer GetSerializer(NomadFileInfo info)
        {
            var filter = ResourceFactory.GetFilter(info);

            INomadSerializer serializer = new NomadResourceSerializer();

            if (filter != null)
            {
                Debug.WriteLine($"Found filter for binary resource '{info.RootId}'.");

                serializer = ResourceFactory.GetSerializer(filter);
            }
            else
            {
                Debug.WriteLine($"Could not find filter for binary resource '{info.RootId}'.");

                // use a generic serializer
                return new NomadGenericResourceSerializer(info);
            }

            serializer.Format = ResourceFactory.GetFormat(info.Version);

            return serializer;
        }
    }
}