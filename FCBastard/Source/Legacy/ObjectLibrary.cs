using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Nomad
{
    [Obsolete("This class is absolute garbage and needs to be put down.")]
    public class ObjectLibrary : IResourceFile
    {
        static readonly MagicNumber Magic = "nbCF";
        static readonly int Type = 3;

        static readonly int BufferSize = Utils.GetSizeInMB(16);

        public NodeObject Root { get; set; }
        
        public void LoadBinary(string filename)
        {
            using (var stream = new BinaryStream(filename))
            {
                Debug.WriteLine(">> Reading FCB header...");
                var magic = stream.ReadInt32();

                if (magic != Magic)
                    throw new InvalidOperationException("Bad magic, no FCB data to parse!");

                var type = stream.ReadInt16();

                if (type != Type)
                    throw new InvalidOperationException("FCB library reported the incorrect type?!");

                stream.Position += 2; // ;)

                var objCount = stream.ReadInt32();
                var attrCount = stream.ReadInt32();
                
                // read fcb data
                Debug.WriteLine(">> Reading objects...");

                var objRefs = new List<NodeObject>();
                Root = new NodeObject(stream, objRefs);
                
                Console.WriteLine($"Finished reading {Root.Children.Count} objects. Collected {objRefs.Count} nodes in total.");
            }
        }

        public void SaveBinary(string filename)
        {
            throw new NotImplementedException("Can't serialize this kind of binary data yet!");

            //--byte[] buffer;
            //--
            //--Debug.WriteLine(">> Generating binary data...");
            //--using (var stream = new BinaryStream(BufferSize))
            //--{
            //--    Debug.WriteLine(">> Writing FCB header...");
            //--    stream.Write((int)Magic);
            //--
            //--    stream.Write((short)Type);
            //--    stream.Write((short)MagicNumber.FB); // ;)
            //--    
            //--    /* TODO: serialize */
            //--
            //--    var size = (int)stream.Position;
            //--    buffer = new byte[size];
            //--
            //--    Debug.WriteLine(">> Copying to buffer...");
            //--    stream.Position = 0;
            //--    stream.Read(buffer, 0, size);
            //--}
            //--
            //--Debug.WriteLine(">> Writing to file...");
            //--File.WriteAllBytes(filename, buffer);
        }

        public void LoadXml(string filename)
        {
            throw new NotImplementedException("Can't load these kind of XML files yet!");
        }

        public void SaveXml(string filename)
        {
            var rootDir = Path.GetDirectoryName(filename);

            var xml = new XmlDocument();
            Root.Serialize(xml);
            
            Debug.WriteLine(">> Saving XML document...");
            xml.SaveFormatted(filename, true);
        }
    }
}
