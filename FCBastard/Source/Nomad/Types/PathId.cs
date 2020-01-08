using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomad
{
    public class PathId
    {
        public static readonly PathId None = -1;

        // { 0xCBF29CE484222325, 0x100000001B3 }
        public static long[] Primes = { -3750763034362895579, 1099511628211 };

        long m_Hash;
        string m_Name;

        int m_Length;

        public static long GetHash(string value)
        {
            return GetHash(value, Primes);
        }

        public static long GetHash(string value, long[] primes)
        {
            var str = value.ToLower();
            str = str.Replace('/', '\\');

            var hash = primes[0];
            var key = primes[1];

            foreach (var c in value)
            {
                hash *= key;
                hash ^= c;
            }

            return hash;
        }
        
        public static long GetHash64(string value)
        {
            return GetHash64(value, Primes);
        }

        public static long GetHash64(string value, long[] primes)
        {
            var hash = GetHash(value, primes);

            return (hash & 0x1FFFFFFFFFFFFFFF) | (10 << 60);
        }

        public static int GetHash32(string value)
        {
            return GetHash32(value, Primes);
        }

        public static int GetHash32(string value, long[] primes)
        {
            var hash = (int)GetHash(value, primes);

            if ((hash & 0xFFFF0000) == 0xFFFF0000)
                hash &= ~(1 << 16);

            return hash;
        }

        public static PathId Parse(string value, bool is32Bit = false)
        {
            if (String.IsNullOrEmpty(value))
                return PathId.None;
            
            if (value[0] == '$')
                return long.Parse(value.Substring(1), NumberStyles.HexNumber);

            return new PathId(value, is32Bit);
        }

        public bool Is32Bit { get; set; }

        public long Hash
        {
            get { return m_Hash; }
            set
            {
                if (value != m_Hash)
                {
                    // force lookup upon next request
                    m_Name = null;
                    m_Length = -1;

                    m_Hash = value;
                }
            }
        }
        
        public string Value
        {
            get
            {
                if (m_Length == -1)
                {
                    m_Name = (Is32Bit)
                        ? $"${(int)m_Hash:X8}"
                        : $"${m_Hash:X16}";

                    m_Length = m_Name.Length;
                }

                return m_Name;
            }
            set
            {
                if (!String.IsNullOrEmpty(value))
                {
                    m_Name = value;
                    m_Length = value.Length;

                    m_Hash = (Is32Bit)
                        ? GetHash32(value)
                        : GetHash64(value);
                }
                else
                {
                    m_Hash = 0;

                    m_Name = String.Empty;
                    m_Length = 0;
                }
            }
        }
        
        public PathId(long hash)
        {
            m_Length = -1;

            m_Hash = hash;
            m_Name = null;

            Is32Bit = ((int)hash == hash);
        }

        public PathId(string value, bool is32Bit = false)
        {
            Is32Bit = is32Bit;

            // relies on Is32Bit to properly hash the value
            Value = value;
        }

        public override int GetHashCode()
        {
            return (int)Hash;
        }

        public override string ToString()
        {
            return Value;
        }

        public bool Equals(string other)
        {
            return (m_Hash == Parse(other));
        }

        public bool Equals(long other)
        {
            return (m_Hash == other);
        }

        public bool Equals(PathId other)
        {
            return (m_Hash == other.m_Hash);
        }

        public override bool Equals(object obj)
        {
            if (obj != null)
            {
                if (obj is string)
                    return Equals((string)obj);
                if (obj is long)
                    return Equals((long)obj);
                if (obj is PathId)
                    return Equals((PathId)obj);
            }

            return false;
        }

        public static implicit operator long(PathId value)          => value.Hash;
        public static implicit operator string(PathId value)        => value.Value;

        public static implicit operator PathId(long value)          => new PathId(value);
        public static implicit operator PathId(string value)        => new PathId(value);

        public static bool operator ==(PathId value, long other)    => value.Equals(other);
        public static bool operator ==(PathId value, string other)  => value.Equals(other);
        public static bool operator ==(PathId value, PathId other)  => value.Equals(other);

        public static bool operator !=(PathId value, long other)    => !value.Equals(other);
        public static bool operator !=(PathId value, string other)  => !value.Equals(other);
        public static bool operator !=(PathId value, PathId other)  => !value.Equals(other);
    }
}
