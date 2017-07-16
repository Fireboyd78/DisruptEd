using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

using DisruptEd;
using DisruptEd.IO;

namespace DisruptEd.FCBastard
{
    class Program
    {
        static StringBuilder fcbBuilder = new StringBuilder();
        static StringBuilder typesBuilder = new StringBuilder();
        
        static XmlWriter fcbLog = XmlWriter.Create(fcbBuilder, new XmlWriterSettings() {
            Indent = true,
            IndentChars = "\t",

            NewLineOnAttributes = true,

            // gotta write my own :/
            OmitXmlDeclaration = true,
        });
        
        static NodeLibrary Library { get; set; }
        
        static void Main(string[] args)
        {
            var filename = (args.Length >= 1) ? args[0] : @"C:\Dev\Research\WD2\entitylibrary_rt.fcb";
            var xmlFile = (args.Length >= 2) ? args[1] : Path.ChangeExtension(filename, ".xml");

            var appDir = AppDomain.CurrentDomain.BaseDirectory;

            StringHasher.Initialize(appDir);
            AttributeTypes.Initialize(appDir);
            
            using (var bs = new BinaryStream(filename))
            {
                Library = new NodeLibrary(bs);
            }
            
            var xml = new XmlDocument();
            Library.Serialize(xml);

            xml.WriteTo(fcbLog);

            var writeTest = true;

            if (writeTest)
            {
                byte[] buffer;
                
                using (var tmp = new BinaryStream(Utils.GetSizeInMB(16)))
                {
                    Console.WriteLine("Writing binary data...");
                    Library.Serialize(tmp);

                    var size = (int)tmp.Position;
                    buffer = new byte[size];

                    Console.WriteLine("Copying to buffer...");
                    tmp.Position = 0;
                    tmp.Read(buffer, 0, size);
                }

                Console.WriteLine("Writing to file...");
                File.WriteAllBytes(Path.ChangeExtension(filename, ".out"), buffer);
            }
        }
    }
}
