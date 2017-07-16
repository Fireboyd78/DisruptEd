using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisruptEd.IO
{
    using HashLookup = Dictionary<int, string>;

    public static class StringHasher
    {
        public static readonly string DefaultLookupFile = "strings.txt";
        public static readonly string UserLookupFile = "strings.user.txt";

        static HashLookup m_lookup = new HashLookup();

        public static void Initialize(string dir)
        {
            AddLookupsFile(Path.Combine(dir, DefaultLookupFile));
            AddLookupsFile(Path.Combine(dir, UserLookupFile));
        }

        public static void AddToLookup(int hash, string value)
        {
            if (!m_lookup.ContainsKey(hash))
                m_lookup.Add(hash, value);
        }

        public static void AddToLookup(string value)
        {
            var hash = GetHash(value);
            AddToLookup(hash, value);
        }

        public static int GetHash(string value)
        {
            if (value == null)
                return 0;

            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = (int)Memory.GetCRC32(bytes);

            return hash;
        }

        public static bool CanResolveHash(int hash)
        {
            return m_lookup.ContainsKey(hash);
        }

        public static string ResolveHash(int hash)
        {
            if (CanResolveHash(hash))
                return m_lookup[hash];

            return null;
        }

        public static string GetHashString(int hash)
        {
            if (CanResolveHash(hash))
                return m_lookup[hash];

            return $"_{hash:X8}";
        }

        public static void AddLookupsFile(string lookupFile)
        {
            var lines = File.ReadAllLines(lookupFile);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // skip empty lines
                if (line.Length == 0)
                    continue;

                // skip first line if it's a comment
                if ((i == 0) && line[0] == '#')
                    continue;

                AddToLookup(line);
            }
        }
    }
}
