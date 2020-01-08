using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace FCBastard
{
    using Nomad;

    class Program
    {
        static readonly AppDomain MyDomain = AppDomain.CurrentDomain;
        static readonly string MyDirectory = MyDomain.BaseDirectory;

        static TraceListener Snoopy = null;
        
        static void RunSuperHasher9000TM()
        {
            Console.Clear();
            Utils.Bastard.Say("<<< SUPER HASHER 9000(tm) >>>");

            var readLine = new Func<string>(() => {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("> ");

                Console.ResetColor();

                return Console.ReadLine();
            });

            var respond = new Action<string>((s) => {
                Utils.Bastard.Say($"{s}");
            });

            var line = "";

            while ((line = readLine()) != null)
            {
                if (line.Length == 0)
                {
                    respond("Type something, dumbass!");
                    continue;
                }

                switch (line[0])
                {
                case '?':
                    {
                        var sb = new StringBuilder();
                        var num = 0;

                        foreach (var hash in StringHasher.GetMissingHashes().OrderBy((k) => $"{k:X8}"))
                        {
                            if ((num % 10) == 0.0)
                            {
                                if (num > 0)
                                    sb.AppendLine();

                                sb.Append($" - ");
                            }

                            sb.Append($"{hash:X8} ");
                            num++;
                        }

                        sb.AppendLine();

                        if (num > 0)
                        {
                            respond($"{num} unknown hashes:");
                            respond(sb.ToString());
                        }
                        else
                        {
                            respond("You should probably load something first, dumbass.");
                        }
                    } continue;
                case '@':
                    {
                        var file = line.Substring(1).Replace("\"", "");

                        try
                        {
                            respond("Loading...");

                            var resource = ResourceFactory.Open(file);

                            if (resource != null)
                            {
                                respond("Done.");
                            }
                            else
                            {
                                respond("Fail.");
                            }
                        }
                        catch (Exception)
                        {
                            respond("Nope.");
                        }
                    } continue;
                default:
                    {
                        var hash = 0;

                        if (line[0] == '$')
                        {
                            try
                            {
                                hash = int.Parse(line.Substring(1), NumberStyles.HexNumber);
                            }
                            catch (Exception)
                            {
                                respond("You're an idiot.");
                                continue;
                            }
                        }
                        else
                        {
                            switch (line)
                            {
                            case "help":
                                respond("I'm afraid I can't do that, Dave.");
                                continue;
                            case "exit":
                                respond("You can check out anytime you like, but you can never leave.");
                                continue;
                            }

                            hash = StringHasher.GetHash(line);
                        }
                        
                        if (StringHasher.CanResolveHash(hash))
                        {
                            respond($"{hash:X8} : '{line}'");
                        }
                        else
                        {
                            if (StringHasher.IsMissingHash(hash))
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGreen;
                                Console.WriteLine("You found a missing hash!");

                                Console.ResetColor();
                            }

                            respond($"{hash:X8} : (unknown)");
                        }
                    } continue;
                }
            }
        }
        
        static IEnumerable<XName> EnumerateXMLNames(XElement elem)
        {
            foreach (var attr in elem.Attributes())
                yield return attr.Name;

            foreach (var child in elem.Elements())
            {
                yield return child.Name;

                foreach (var name in EnumerateXMLNames(child))
                    yield return name;
            }
        }

        static void SnoopFile(string filename)
        {
            XDocument xml = null;
            
            if (!Utils.TryLoadXmlFile(filename, out xml))
                Utils.Bastard.Fail("Snoopy only works with XML files.");

            var strs = new HashSet<String>();
            
            foreach (var name in EnumerateXMLNames(xml.Root))
            {
                var id = new StringId(name.LocalName);

                if (!strs.Contains(id) && !StringHasher.CanResolveHash(id))
                    strs.Add(id);
            }

            if (strs.Count == 0)
                Utils.Bastard.Fail("Couldn't find anything new, sadly.");
            
            var outFile = Path.ChangeExtension(filename, ".snoop.log");
            
            File.WriteAllText(outFile, String.Join("\n", strs.OrderBy((s) => s)));
        }
        
        //
        // TODO: Clean up logic
        //
        static void ProcessFile(string filename)
        {
            ResourceFile resource = null;

            var inputType = FileFactory.GetFileType(filename);

            //
            // TODO: Add custom triggers for filenames
            //
            if (Config.HasArg("movedata") || (Path.GetFileNameWithoutExtension(filename) == "combinedmovefile"))
            {
                resource = new ResourceFile() {
                    Type = inputType,
                    CustomSerializer = new CombinedMoveFileSerializer(),
                    CustomFilter = new ResourceFilter("CombinedMoveFile", 3, ResourceType.Generic, ".bin"),
                };

                resource.Load(filename);
            }
            else if (Config.HasArg("oasis"))
            {
                resource = new ResourceFile() {
                    Type = inputType,
                    CustomSerializer = new OasisSerializer(),
                    CustomFilter = new ResourceFilter("stringtable", 1, ResourceType.Generic, ".bin"),
                };

                resource.Load(filename);
            }
            else
            {
                resource = ResourceFactory.Open(filename);
            }

            if (resource == null)
                throw new InvalidDataException("File has no data to process!");

            var filter = resource.GetFilter();
            
            if (String.IsNullOrEmpty(Config.Output))
            {
                var fileExt = ".xml";

                if (inputType == FileType.Xml)
                {
                    if (filter != null)
                    {
                        fileExt = filter.FileExt;
                    }
                    else
                    {
                        // ruh-roh
                        fileExt = $"{Path.GetExtension(filename)}.out";
                    }
                }

                Config.Output = Path.ChangeExtension(filename, fileExt);
            }

            if (Config.OutDir != null)
            {
                var outDir = Config.OutDir.TrimStart('\\');
                
                if (!Path.IsPathRooted(outDir))
                    outDir = Path.Combine(Path.GetDirectoryName(filename), outDir);
                
                Config.Output = Path.Combine(outDir, Path.GetFileName(Config.Output));
            }

            var outputType = FileFactory.GetFileType(Config.Output);

            if (resource.Type == FileType.Xml)
            {
                if (!(resource.CustomSerializer is INomadXmlFileSerializer))
                    resource.CustomSerializer = null;
            }

            resource.Type = outputType;
            
            if (String.Equals(filename, Config.Output, StringComparison.InvariantCultureIgnoreCase))
                Utils.Bastard.Fail("Can't process a file that results in recompilation!");

            resource.Save(Config.Output);
        }

        static void Bastard_DoBatch(string directory)
        {
            var filter = "*.fcb";

            if (Config.HasArg("filter"))
            {
                filter = Config.GetArg("filter");
            }
            else
            {
                Utils.Bastard.Warn($"No filter specified for batch job -- using defaults!");
            }

            var option = SearchOption.TopDirectoryOnly;

            if (Config.HasArg("r"))
                option = SearchOption.AllDirectories;
            
            var dirInfo = new DirectoryInfo(directory);
            var filters = filter.Split(';', '|');
            
            Utils.Bastard.Say($"Running batch job on directory '{dirInfo.FullName}' using filters '{filter}'.");

            foreach (var fi in filters)
            {
                foreach (var file in dirInfo.EnumerateFiles(fi, option))
                {
                    //
                    // HACK: Temporary measure until ProcessFile is cleaned up
                    //
                    Config.Output = null;

                    try
                    {
                        Utils.Bastard.Say($"Processing file '{file.Name}'...");
                        ProcessFile(file.FullName);
                    }
                    catch (Exception e)
                    {
                        Utils.Bastard.Warn($" - FAILED: {e.Message}");
                    }
                }
            }
        }

        static void Bastard_WorkNEW()
        {
            if (Config.NoLoveForFireboyd)
                Nomad.WriteSealOfApproval = false; // :(

            if (Config.HasArg("dir"))
            {
                var dir = Config.GetArg("dir");

                Bastard_DoBatch(dir);
            }
            else
            {
                if (String.IsNullOrEmpty(Config.Input))
                    Utils.Bastard.Fail("No input specified!");

                if (!File.Exists(Config.Input))
                {
                    Utils.Bastard.Fail("Yeah sure, lemme just pull that file out of my ass...");
                    Environment.Exit(69);
                }

                if (Config.HasArg("snoop"))
                {
                    SnoopFile(Config.Input);
                }
                else
                {
                    //
                    // TODO: Do more stuff here
                    //
                    ProcessFile(Config.Input);
                }
            }

            // job well done
            var success = Config.GetSuccessMessage();
            Utils.Bastard.Say(success);
        }

#if OLD_BASTARD_WORK
        static void Bastard_Work()
        {
            var inputType = FileFactory.GetFileType(Config.Input);

            if (inputType == FileType.Xml)
            {
                using (var stream = File.Open(Config.Input, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Console.WriteLine("Loading XML...");
                    var xmlData = new NomadXmlSerializer();
                    var root = xmlData.Deserialize(stream);

                    if (Config.HasArg("snoop"))
                    {
                        var strs = new Dictionary<int, string>();

                        foreach (var obj in root)
                        {
                            var id = obj.Id;

                            if (!strs.ContainsKey(id) && !StringHasher.CanResolveHash(id))
                                strs.Add(id.Hash, id.Name);
                        }

                        var log = String.Join("\n", strs.ToArray().Select((kv) => kv.Value).OrderBy((s) => s));

                        File.WriteAllText(GetOutputFilePath(".snoop.log"), log);
                    }

                    var dataFilter = ResourceFactory.GetFilter(root);

                    var ext = (dataFilter != null) ? dataFilter.FileExt : ".fcb";
                    var outFile = GetOutputFilePath(ext);

                    using (var bs = new BinaryStream(1024))
                    {
                        if (root.IsRml)
                        {
                            var rmlData = new NomadRmlSerializer();

                            Console.WriteLine("Saving RML...");
                            rmlData.Serialize(bs, root);
                        }
                        else
                        {
                            var resData = new NomadResourceSerializer() {
                                Format = FormatType.Resource
                            };

                            // HACK HACK HACK!
                            Utils.Bastard.Warn("**** WARNING! Write cache is broken in this version -- this MAY cause game crashes! ****");

                            AttributeData.AllowMiniBuffers = false;
                            WriteCache.Enabled = false;

                            if ((dataFilter != null) && (dataFilter.Version >= 3))
                            {
                                resData.Format = FormatType.Objects;
                                AttributeData.AllowMiniBuffers = true;
                            }

                            Console.WriteLine("Saving data...");
                            resData.Serialize(bs, root);
                        }

                        Utils.WriteFile(bs, outFile, (int)bs.Length);
                    }
                }
            }
            else
            {
                if (inputType == FileType.Any)
                    throw new InvalidOperationException(">>> NOT SUPPORTED YET! <<<");

                if (inputType == FileType.FCXMap)
                {
                    using (var stream = new BinaryStream(Config.Input))
                    {
                        var serializer = new FCXMapSerializer();

                        Console.WriteLine("Loading map data...");
                        var mapData = serializer.Deserialize(stream);

                        var outFile = GetOutputFilePath(".fc5map.xml");

                        using (var fsXml = File.Open(outFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            var xmlSerializer = new NomadXmlSerializer();

                            Console.WriteLine("Saving XML...");
                            xmlSerializer.Serialize(fsXml, mapData);
                        }
                    }
                }
                else
                {
                    using (var stream = new BinaryStream(Config.Input))
                    {
                        var serializer = NomadFileInfo.GetSerializer(stream);

                        Console.WriteLine("Loading data...");
                        var root = serializer.Deserialize(stream);

                        var outFile = GetOutputFilePath(".xml");

                        using (var fsXml = File.Open(outFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            var xmlSerializer = new NomadXmlSerializer();

                            Console.WriteLine("Saving XML...");
                            xmlSerializer.Serialize(fsXml, root);
                        }
                    }
                }
            }
            
            // job well done
            var success = Config.GetSuccessMessage();
            Utils.Bastard.Say(success);
        }
#endif

        static void Bastard_Die()
        {
            if (Snoopy != null)
            {
                Snoopy.Close();
                Snoopy.Flush();

                Snoopy = null;
            }
        }

        static void Bastard_Think()
        {
            if (Config.SuperHasher9000)
            {
                RunSuperHasher9000TM();
            }
            else if (Config.HasArg("dumpstr"))
            {
                Utils.Bastard.Say("Dumping strings data...");
                StringHasher.Dump(MyDirectory);
            }
            else
            {
                Bastard_WorkNEW();
            }
        }
        
        static void Bastard_Prepare()
        {
            // TODO TODO TODO
            if (Config.DumpInfo)
                Utils.Bastard.Fail("**** Not implemented yet! ****");

            if (Config.DebugLog)
            {
                var debugLog = Path.Combine(MyDirectory, "debug.log");

                if (File.Exists(debugLog))
                    File.Delete(debugLog);

                Snoopy = new TextWriterTraceListener(debugLog, "DEBUG_LOG");

                Debug.AutoFlush = true;

                Debug.Listeners.Clear();
                Debug.Listeners.Add(Snoopy);
            }

            if (Config.Silent)
            {
                Console.SetOut(TextWriter.Null);
                Console.SetError(TextWriter.Null);
            }

            StringHasher.Initialize(MyDirectory);
            AttributeTypes.Initialize(MyDirectory);
            ResourceFactory.Initialize(MyDirectory);
        }

        static void Bastard_Awaken()
        {
            if (!Debugger.IsAttached)
            {
                MyDomain.UnhandledException += (o, e) => {
                    var ex = e.ExceptionObject as Exception;

                    Console.WriteLine($"ERROR: {ex.Message}\r\n");

                    Console.WriteLine("==================== Stack Trace ====================");
                    Console.WriteLine($"<{e.GetType().FullName}>:");
                    Console.WriteLine(ex.StackTrace);

                    Environment.Exit(1);
                };
            }
            
            // make sure the user's culture won't fuck up program operations :/
            Thread.CurrentThread.CurrentCulture = Config.Culture;
            Thread.CurrentThread.CurrentUICulture = Config.Culture;
            
            Utils.Bastard.Say($"<<< FCBastard {Config.VersionString} >>>");
        }
        
        static void Main(string[] args)
        {
            Bastard_Awaken();

            if (args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(Config.UsageString);

                Console.ResetColor();

                Environment.Exit(0);
            }
            
            if (Config.ProcessArgs(args) > 0)
            {
                try
                {
                    Bastard_Prepare();
                    Bastard_Think();
                }
                finally
                {
                    Bastard_Die();
                }
            }
            else
            {
                Utils.Bastard.Say("Wh--what the fuck did you do?! HOW DID YOU GET HERE!?");
                Environment.Exit(MagicNumber.FIREBIRD);
            }
        }
    }
}
