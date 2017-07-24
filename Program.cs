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
    class BastardConfig
    {
        public static readonly string[] Types = {
            "dev",
            "rel",
        };

        public static readonly int Type;
        public static readonly Version Version;

        public static CultureInfo Culture = new CultureInfo("en-US", false);

        public static string VersionString
        {
            get { return $"v{Version.ToString()}-{Types[Type]}"; }
        }
        
        static BastardConfig()
        {
            Version = Assembly.GetExecutingAssembly().GetName().Version;
        #if RELEASE
            Type = 1;
        #else
            Type = 0;
        #endif
        }
    }

    class Program
    {
        enum FileType
        {
            BinaryData,
            Xml,

            Other,
        }

        static StringBuilder fcbBuilder = new StringBuilder();
        static StringBuilder typesBuilder = new StringBuilder();
        
        static XmlWriter fcbLog = XmlWriter.Create(fcbBuilder, new XmlWriterSettings() {
            Indent = true,
            IndentChars = "\t",

            NewLineOnAttributes = true,

            // gotta write my own :/
            OmitXmlDeclaration = true,
        });
        
        static EntityLibraryCollection Library { get; set; }

        static FileType GetFileType(string filename)
        {
            var ext = Path.GetExtension(filename);

            switch (ext)
            {
            case ".bin":
            case ".dat":
            case ".fcb":
                return FileType.BinaryData;

            case ".xml":
                return FileType.Xml;
            }

            return FileType.Other;
        }

        static void Abort(string message)
        {
            Console.Error.Write($"ERROR: {message}");
            Environment.Exit(1);
        }
        
        static void Main(string[] args)
        {
            // make sure the user's culture won't fuck up program operations :/
            Thread.CurrentThread.CurrentCulture = BastardConfig.Culture;
            Thread.CurrentThread.CurrentUICulture = BastardConfig.Culture;

            Console.WriteLine($"<<< FCBastard {BastardConfig.VersionString} >>>");
            
#if RELEASE
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: <input> <:output>");
                Console.WriteLine(" The file extension* is used to determine which operation to perform.");
                Console.WriteLine("  * FCB/Binary: *.bin, *.dat, *.fcb");
                Console.WriteLine("  * XML: ?!");
                Console.WriteLine("  ** If you supply an invalid extension, The Bastard won't know what to do.");
                Console.WriteLine(" Output is optional* -- The Bastard can figure out what to do (e.g. 'FCB'->'XML' etc.)");
                Console.WriteLine("  * Generated binaries will be put in a 'bin' folder where the XML file resides.");
                Console.WriteLine("Examples:");
                Console.WriteLine(" 'fcbastard z:\\library.fcb' ; create XML file 'z:\\library.xml'");
                Console.WriteLine(" 'fcbastard z:\\library.xml' ; create FCB file 'z:\\bin\\library.fcb'");
                Console.WriteLine(" 'fcbastard z:\\library.xml z:\\final\\library.fcb' ; XML->FCB");
                Console.WriteLine(" 'fcbastard z:\\library.fcb z:\\export\\library.xml' ; FCB->XML");

                Environment.Exit(0);
            }
#endif

            var loadType = FileType.Other;
            var saveType = FileType.Other;
            
            var inFile = (args.Length >= 1) ? args[0] : @"C:\Dev\Research\W_D\entitylibrary_rt.fcb";
            loadType = GetFileType(inFile);

            if (loadType == FileType.Other)
                Abort($"Can't determine input file type of '{inFile}', aborting...");
            
            var outDir = Path.GetDirectoryName(inFile);

            // generate binary data
            if (loadType == FileType.Xml)
                outDir = Path.Combine(outDir, "bin");

            var outExt = (loadType == FileType.BinaryData) ? "xml" : "fcb";

            var outFile = (args.Length >= 2) ? args[1] : Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(inFile)}.{outExt}");
            saveType = GetFileType(outFile);

            if (saveType == FileType.Other)
                Abort($"Can't determine output file type of '{outFile}', aborting...");

#if RELEASE
            if (loadType == saveType)
            {
                if (String.Compare(inFile, outFile, StringComparison.OrdinalIgnoreCase) == 0)
                    Abort($"Fatal error ID:10T -- put the crack pipe away and try again.");

                Abort($"The input and output file types are the same ('{loadType}' == '{saveType}'), perhaps you should just copy the file?");
            }
#endif
            Console.WriteLine($"Input file: '{inFile}' ({loadType.ToString()})");
            Console.WriteLine($"Output file: '{outFile}' ({saveType.ToString()})");

            Console.WriteLine("Initializing...");
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            
            StringHasher.Initialize(appDir);
            AttributeTypes.Initialize(appDir);

#if DEBUG
            var debugLog = Path.Combine(appDir, "debug.log");

            if (File.Exists(debugLog))
                File.Delete(debugLog);

            var debugListener = new TextWriterTraceListener(debugLog, "DEBUG_LOG");

            Debug.Listeners.Clear();
            Debug.Listeners.Add(debugListener);
#endif
            // get the real output directory
            outDir = Path.GetDirectoryName(outFile);

            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            Library = new EntityLibraryCollection();

            switch (loadType)
            {
            case FileType.BinaryData:
                {
                    Console.WriteLine("Loading binary data...");
                    Library.LoadBinary(inFile);
                } break;
            case FileType.Xml:
                {
                    Console.WriteLine("Loading xml data...");
                    Library.LoadXml(inFile);
                } break;
            }

            switch (saveType)
            {
            case FileType.BinaryData:
                {
                    Console.WriteLine("Saving binary data...");
                    Library.SaveBinary(outFile);
                } break;
            case FileType.Xml:
                {
                    Console.WriteLine("Saving xml...");
                    Library.SaveXml(outFile);
                } break;
            }

#if DEBUG
            debugListener.Flush();
#endif

            Console.WriteLine("The Bastard has successfully completed his job.");
        }
    }
}
