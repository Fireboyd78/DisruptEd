using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomad
{
    using HashLookup = Dictionary<int, string>;

    public static class StringHasher
    {
        public static readonly string DefaultLookupFile = "strings.txt";
        public static readonly string UserLookupFile = "strings.user.txt";
        
        static HashLookup m_lookup = new HashLookup(50000);
        static HashLookup m_missing = new HashLookup();

        public static bool AnyMissingHashes
        {
            get { return m_missing.Count > 0; }
        }

        public static void Initialize(string dir)
        {
            AddLookupsFile(Path.Combine(dir, DefaultLookupFile));
            AddLookupsFile(Path.Combine(dir, UserLookupFile));
        }

        public static void Dump(string dir)
        {
#if DUMPSTR_SPLIT_FILES
            var names = new List<String>();
            var types = new List<String>();

            foreach (var kv in m_lookup)
            {
                var k = kv.Key;
                var v = kv.Value;

                if (Utils.CheckString(v) >= 0)
                {
                    if (AttributeTypes.IsTypeKnown(k))
                    {
                        types.Add(v);
                    }
                    else
                    {
                        names.Add(v);
                    }
                }
            }
            
            File.WriteAllLines(Path.Combine(dir, "dumpstr_names.txt"), names.OrderBy(s => s));
            File.WriteAllLines(Path.Combine(dir, "dumpstr_types.txt"), types.OrderBy(s => s));
#else
            var strings = from kv in m_lookup
                          let k = kv.Key
                          let v = kv.Value

                          // don't include strings with spaces!
                          where ((Utils.CheckString(v) >= 0) && (v.IndexOf(' ') == -1))
                          orderby v
                          select v;

            File.WriteAllLines(Path.Combine(dir, "strings_dump.txt"), strings);
#endif
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

        public static void AddMissingHash(int hash)
        {
            if (!m_missing.ContainsKey(hash))
                m_missing.Add(hash, null);
        }

        public static bool IsMissingHash(int hash)
        {
            return m_missing.ContainsKey(hash);
        }

        public static IEnumerable<int> GetMissingHashes()
        {
            foreach (var kv in m_missing)
                yield return kv.Key;

            yield break;
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

            AddMissingHash(hash);

            return null;
        }

        public static bool TryResolveHash(int hash, out string result)
        {
            if (m_lookup.TryGetValue(hash, out result))
                return true;

            AddMissingHash(hash);

            result = null;
            return false;
        }

        public static string GetHashString(int hash)
        {
            if (CanResolveHash(hash))
                return m_lookup[hash];

            AddMissingHash(hash);

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
