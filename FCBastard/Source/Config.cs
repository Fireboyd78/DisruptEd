using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Nomad;

namespace FCBastard
{
    static class Config
    {
        private static IEnumerable<ArgInfo> m_args;

        public static string Input { get; set; }
        public static string Output { get; set; }
        
        public static string OutDir { get; set; }
        
        public static bool Compile { get; set; }
        public static bool Silent { get; set; }
        
        public static bool SuperHasher9000 { get; set; }

        public static bool DebugLog { get; set; }
        public static bool DumpInfo { get; set; }

        public static bool NoLoveForFireboyd { get; set; }
        
        public static IEnumerable<ArgInfo> Args
        {
            get { return m_args; }
        }

        public static bool HasArg(string name)
        {
            foreach (var arg in m_args)
            {
                if (arg.HasName && (arg.Name == name))
                    return true;
            }

            return false;
        }

        public static string GetArg(string name)
        {
            foreach (var arg in m_args)
            {
                if (arg.HasName && (arg.Name == name))
                    return arg.Value;
            }

            return null;
        }

        public static bool GetArg(string name, ref int value)
        {
            int result = 0;

            foreach (var arg in m_args)
            {
                if (arg.HasName && (arg.Name == name))
                {
                    if (int.TryParse(arg.Value, out result))
                    {
                        value = result;
                        return true;
                    }
                }
            }

            return false;
        }

        static readonly string[] m_usage = {
            "Usage: [options] <input> <:output>",
            "  By default, the input file will be used to determine the type of output (if any).",
            "  Additonal options can be specified to force a certain behavior (see below).",
            "",
            "  Output is optional* -- The Bastard can figure out what to do (e.g. 'FCB'->'XML' etc.)",
            "    * Generated binaries will be put in a 'bin' folder where the XML file resides.",
            "Options:",
            "  -i|info          Displays version info for the input file, but does not take any action upon it.",
            "                   Useful for seeing how The Bastard will interpret a file.",
            "",
            "                   NOTE: This will cause any output arguments/options to be ignored.",
            "",
            "  -o|out|out_dir   Specifies the output directory to be used for any data created.",
            "                   If the directory does not exist, it will be created.",
            "",
            "                   Paths may also be relative to the input/output file's location:",
            "                     '-out_dir bin'",
            "                     '-out_dir .\\bin'",
            "                     '-out_dir ..\\data\\bin'",
            "                     '-out_dir z:\\projects\\wd2\\data\\bin'",
            "Advanced options:",
            "  -sh9k            Clears the console and starts up the Super Hasher 9000(tm).",
            "                   Can also be enabled by specifying '--' as the only argument.",
            "",
            "                   Type anything to get the hash of it, or prefix '$' and enter a valid hash to find a match.",
            "",
            "                   Load files by prefixing '@' to your command. Their hashes will be cached upon loading.",
            "                   You can dump all cached hashes by simply typing '?'.",
            "",
            "                   To exit, just do Ctrl+C to break (like any other console command).",
            "",
            "                   There is no 'help' command. Don't bother trying!",
            "",
            "                   Beware of bugs!",
            "",
            "  -l|log           Enables debugging information to be output to a file where The Bastard resides.",
            "                   May cause slowdowns due to additional operations being performed!",
            "",
            "                   If you need to submit a bug report, turn this on and provide the log.",
            "Examples:",
            "  'fcbastard z:\\library.fcb' ; create XML file 'z:\\library.xml'",
            "  'fcbastard z:\\library.xml' ; create FCB file 'z:\\bin\\library.fcb'",
            "  'fcbastard z:\\library.xml z:\\final\\library.fcb' ; XML->FCB",
            "  'fcbastard z:\\library.fcb z:\\export\\library.xml' ; FCB->XML",
        };

        static readonly string[] m_types = {
            "dev",
            "rel",
        };

        static readonly string[] m_complete_msg = {
            "My job's done here.",
            "Fin~",
            "All done!",

            "This successful operation sponsored in part by Deebz__(tm).",
            "Another successful job completed. You should buy me a beer! ;)",
            "Success! What else did you expect?",
            "Failed to cause an error: Operation completed successfully.",

            "Enjoy flying helicopters and shit.",
            "The planets glowed as The Bastard said, 'Let there be helicopters.'",

            "Did you even change anything?!",
            "Could you spare some change? Please?!",
            "It's a far cry from the watch dogs we thought we'd inherit!",
            
            "[The Bastard gained +1 in the Sentinence skill.]",
        };

        public static readonly int BuildType =
#if RELEASE
            1;
#else
            0;
#endif

        public static readonly CultureInfo Culture;

        public static readonly Version BuildVersion;
        
        public static string UsageString
        {
            get { return String.Join("\r\n", m_usage); }
        }

        public static string VersionString
        {
            get { return $"v{BuildVersion.ToString()}-{m_types[BuildType]}"; }
        }

        public static string GetSuccessMessage()
        {
            var seed = (int)(DateTime.Now.ToBinary() * ~0xF12EB12D);
            var rand = new Random(seed);
            
            var idx = rand.Next(0, m_complete_msg.Length - 1);
            return m_complete_msg[idx];
        }
        
        static Config()
        {
            Culture = new CultureInfo("en-US", false);
            BuildVersion = Assembly.GetExecutingAssembly().GetName().Version;
#if DEBUG
            // force debug log for dev builds
            DebugLog = true;
#endif
        }
        
        public static int ProcessArgs(string[] args)
        {
            // argument stage flags:
            //  0: empty
            //  1: got options
            //  2: got input
            //  4: got output
            //  8: got extras
            var argFlags = 0;

            var _args = new List<ArgInfo>();

            for (int i = 0; i < args.Length; i++)
            {
                ArgInfo arg = args[i];

                if (arg.IsEmpty)
                {
                    if (i != 0)
                        throw new InvalidOperationException("String Hasher 9000(tm) threw error code 0x1D1070 -- shit for brains!");

                    SuperHasher9000 = true;
                    return 1;
                }

                if (arg.HasName)
                {
                    if (arg.IsSwitch)
                    {
                        switch (arg.Name)
                        {
                        case "sh9k":
                            SuperHasher9000 = true;
                            break;
                        case "log":
                            DebugLog = true;
                            break;
                        case "i":
                        case "info":
                            DumpInfo = true;
                            break;
                        case "ihatefireboyd":
                            NoLoveForFireboyd = true;
                            break;
                        case "o":
                        case "out":
                        case "out_dir":
                            if (!arg.HasValue)
                                arg = new ArgInfo(arg.Name, args[++i]);

                            OutDir = Environment.ExpandEnvironmentVariables(arg.Value);
                            break;
                        case "q":
                        case "quiet":
                            Silent = true;
                            break;
                        }
                    }

                    argFlags |= 1;
                }
                else
                {
                    if (arg.IsValue)
                    {
                        switch ((argFlags >> 1) & 3)
                        {
                        case 0:
                            Input = arg.Value;
                            argFlags |= 2;
                            continue;
                        case 1:
                            Output = arg.Value;
                            argFlags |= 4;
                            continue;
                        default:
                            argFlags |= 8;
                            break;
                        }
                    }
                }

                _args.Add(arg);
            }
            
            m_args = _args.AsEnumerable();
            
            // all done, return number of args processed
            return _args.Count + ((argFlags >> 1) & 3);
        }
    }
}
