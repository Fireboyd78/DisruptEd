using System;
using System.Globalization;

namespace Nomad
{
    public class StringId : IEquatable<string>, IEquatable<int>, IEquatable<StringId>
    {
        public static readonly StringId None = 0;

        int m_Hash;
        string m_Name;

        int m_Length;

        public static StringId Parse(int hash)
        {
            StringId result = hash;
            var name = String.Empty;

            if (StringHasher.TryResolveHash(result, out name))
                result = name;

            return result;
        }

        public static StringId Parse(string value)
        {
            if (String.IsNullOrEmpty(value))
                return StringId.None;

            if (value[0] == '_')
                return int.Parse(value.Substring(1), NumberStyles.HexNumber);

            return value;
        }
        
        public int Hash
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

        public string Name
        {
            get
            {
                // do we need to do a hash lookup?
                if (m_Length == -1)
                {
                    m_Name = StringHasher.ResolveHash(m_Hash) ?? $"_{m_Hash:X8}";
                    m_Length = m_Name.Length;
                }

                return m_Name;
            }
            set
            {
                if (!String.IsNullOrEmpty(value))
                {
                    m_Hash = StringHasher.GetHash(value);

                    m_Name = value;
                    m_Length = value.Length;
                }
                else
                {
                    m_Hash = 0;

                    m_Name = String.Empty;
                    m_Length = 0;
                }
            }
        }
        
        public StringId(int hash)
        {
            m_Length = -1;

            m_Hash = hash;
            m_Name = null;
        }

        public StringId(string name)
        {
            m_Length = name.Length;

            m_Hash = StringHasher.GetHash(name);
            m_Name = name;
        }

        public override int GetHashCode()
        {
            return Hash;
        }

        public override string ToString()
        {
            return Name;
        }

        public bool Equals(string other)
        {
            return (m_Hash == Parse(other));
        }

        public bool Equals(int other)
        {
            return (m_Hash == other);
        }

        public bool Equals(StringId other)
        {
            return (m_Hash == other.m_Hash);
        }

        public override bool Equals(object obj)
        {
            if (obj != null)
            {
                if (obj is string)
                    return Equals((string)obj);
                if (obj is int)
                    return Equals((int)obj);
                if (obj is StringId)
                    return Equals((StringId)obj);
            }

            return false;
        }

        public static implicit operator int(StringId value)             => value.Hash;
        public static implicit operator string(StringId value)          => value.Name;

        public static implicit operator StringId(int value)             => new StringId(value);
        public static implicit operator StringId(string value)          => new StringId(value);

        public static bool operator ==(StringId value, int other)       => value.Equals(other);
        public static bool operator ==(StringId value, string other)    => value.Equals(other);
        public static bool operator ==(StringId value, StringId other)  => value.Equals(other);

        public static bool operator !=(StringId value, int other)       => !value.Equals(other);
        public static bool operator !=(StringId value, string other)    => !value.Equals(other);
        public static bool operator !=(StringId value, StringId other)  => !value.Equals(other);
    }
}
