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
    public abstract class NomadData : IEnumerable<NomadData>
    {
        public StringId Id { get; set; }

        public virtual bool IsAttribute => false;
        public virtual bool IsObject => false;

        public abstract bool IsRml { get; }
        
        public abstract IEnumerator<NomadData> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class NomadCache
    {
        public static readonly List<long> Keys;
        public static readonly List<NomadData> Refs;
        
        public static void Clear()
        {
            Keys.Clear();
            Refs.Clear();
        }
        
        static long GenerateHashKey(NomadData data)
        {
            var id = data.Id;
            var hash = (long)id.GetHashCode();
            
            if (data.IsAttribute)
            {
                var attrData = ((NomadValue)data).Data;

                hash *= attrData.GetHashCode();
                hash ^= attrData.Size;
            }

            if (data.IsObject)
            {
                var obj = ((NomadObject)data);

                var key = 12345L;
                var size = 0;

                foreach (var attr in obj.Attributes)
                {
                    var attrData = attr.Data;

                    key += attrData.GetHashCode();
                    size += attrData.Size;
                }
                
                foreach (var child in obj.Children)
                    key ^= GenerateHashKey(child);

                hash *= key;
                hash ^= (size * obj.Children.Count);
            }

            return hash;
        }
        
        public static int Find(NomadData data)
        {
            var hash = GenerateHashKey(data);

            return Keys.IndexOf(hash);
        }
        
        public static int Register(NomadData data, out long hash)
        {
            hash = GenerateHashKey(data);

            var idx = -1;

            if (Keys.Contains(hash))
            {
                idx = Keys.IndexOf(hash);
            }
            else
            {
                idx = Keys.Count;

                Keys.Add(hash);
                Refs.Add(data);
            }
            
            return idx;
        }

        static NomadCache()
        {
            Keys = new List<long>();
            Refs = new List<NomadData>();
        }
    }

    public class NomadReference
    {
        public long Hash { get; }
        public int Index { get; }

        public NomadData Get()
        {
            return NomadCache.Refs[Index];
        }

        public override int GetHashCode()
        {
            return (int)Hash;
        }

        public NomadReference(NomadData data)
        {
            Index = NomadCache.Register(data, out long hash);
            Hash = hash;
        }
    }
}