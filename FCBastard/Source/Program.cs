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
        static string[] m_usage = {
            "Usage: <input> <:output>",
            " The file extension* is used to determine which operation to perform.",
            "  * FCB/Binary: *.bin, *.dat, *.fcb, *.lib, *.obj",
            "  * XML: ?!",
            "  ** If you supply an invalid extension, The Bastard won't know what to do.",
            " Output is optional* -- The Bastard can figure out what to do (e.g. 'FCB'->'XML' etc.)",
            "  * Generated binaries will be put in a 'bin' folder where the XML file resides.",
            "Examples:",
            " 'fcbastard z:\\library.fcb' ; create XML file 'z:\\library.xml'",
            " 'fcbastard z:\\library.xml' ; create FCB file 'z:\\bin\\library.fcb'",
            " 'fcbastard z:\\library.xml z:\\final\\library.fcb' ; XML->FCB",
            " 'fcbastard z:\\library.fcb z:\\export\\library.xml' ; FCB->XML",
        };

        static readonly string[] m_types = {
            "dev",
            "rel",
        };

        static readonly string[] m_complete_msg = {
            "My job's done here.",
            "I'm pretty damn good at kicking ass, no?",
            "Fin~",
            "This successful operation sponsored in part by Deebz__(tm)",
            "Another successful job completed. You should buy me a beer ;)",
            "Kiss my shiny metal ass, [redacted]!",
            "Success! What else did you expect?",
            "[insert witty success message here]",
            "Failed to cause an error: Operation completed successfully.",
            "Failure is for losers. You're a WINNER!",
            "Doesn't it feel good to be on the winning team?",
            "I don't mean to brag, but I'm pretty fucking awesome!",
            "Once a bastard, always a bastard. Don't fuck with me!",
            "Who the hell wrote these awful success messages?!",
            "Successfully completed the operation. Now, was that so hard?",
            "Enjoy flying helicopters and shit.",
            "I saw what you did there ;)",
            "Are you sure about th-- nevermind, that's none of my business.",
            "Amazing how a free and open-source tool is so useful!",
            "Another fan-fucking-tastic operation completed!",
            "I could really, really fucking use some beer money...please!",
            "Runnin' a bit low on that beer money...*cough cough*",
            "I'm addicted to success.",
            "Definitely not sick of winning yet ;)",
        };
        
        public static readonly int Type;
        public static readonly Version Version;

        public static CultureInfo Culture = new CultureInfo("en-US", false);

        public static string UsageString
        {
            get { return String.Join("\r\n", m_usage); }
        }

        public static string VersionString
        {
            get { return $"v{Version.ToString()}-{m_types[Type]}"; }
        }

        public static string GetSuccessMessage()
        {
            var seed = (int)(DateTime.Now.ToBinary() * ~0xF12EB12D);
            var rand = new Random(seed);
            
            var idx = rand.Next(0, m_complete_msg.Length - 1);
            return m_complete_msg[idx];
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

        enum LibraryType
        {
            Entities,
            Objects,
        }

        static FileType GetFileType(string filename)
        {
            var ext = Path.GetExtension(filename);

            switch (ext)
            {
            case ".bin":
            case ".dat":
            case ".fcb":
            case ".lib":
            case ".obj":
                return FileType.BinaryData;

            case ".xml":
                return FileType.Xml;
            }

            return FileType.Other;
        }

        /* TODO: make this do more advanced checks */
        static LibraryType GetLibraryType(string filename)
        {
            if (filename.StartsWith("entitylibrary", StringComparison.OrdinalIgnoreCase))
                return LibraryType.Entities;

            return LibraryType.Objects;
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
                Console.WriteLine(BastardConfig.UsageString);
                Environment.Exit(0);
            }
#endif

            var loadType = FileType.Other;
            var saveType = FileType.Other;
            
            var inFile = args[0];
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

            IResourceFile library = null;

            var libType = GetLibraryType(inFile);

            switch (libType)
            {
            case LibraryType.Entities:
                library = new EntityLibraryCollection();
                NodeDescriptor.GlobalFlags = DescriptorFlags.Use24Bit;
                break;
            case LibraryType.Objects:
                library = new ObjectLibrary();
                NodeDescriptor.GlobalFlags = DescriptorFlags.None;
                break;
            }
            
            switch (loadType)
            {
            case FileType.BinaryData:
                {
                    Console.WriteLine("Loading binary data...");
                    library.LoadBinary(inFile);
                } break;
            case FileType.Xml:
                {
                    Console.WriteLine("Loading xml data...");
                    library.LoadXml(inFile);
                } break;
            }

            switch (saveType)
            {
            case FileType.BinaryData:
                {
                    Console.WriteLine("Saving binary data...");
                    library.SaveBinary(outFile);
                } break;
            case FileType.Xml:
                {
                    Console.WriteLine("Saving xml...");
                    library.SaveXml(outFile);
                } break;
            }

#if DEBUG
            debugListener.Flush();
#endif

            var success = BastardConfig.GetSuccessMessage();
            Console.WriteLine(success);
        }
    }
}
