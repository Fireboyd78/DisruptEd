using System;
using System.Collections.Generic;

namespace Nomad
{
    public static class WriteCache
    {
        static Dictionary<int, CachedData> m_buffers = new Dictionary<int, CachedData>();

        public static bool Enabled = true;

        static int CalculateHashCode(byte[] buffer, int key)
        {
            if (buffer != null)
            {
                var size = buffer.Length;
                var crcKey = 0xFFFFFFFF;

                if (size != 0)
                    crcKey &= (uint)((~key ^ size) | size);

                return (int)Memory.GetCRC32(buffer, crcKey);
            }

            return -1;
        }

        public static bool IsCached(byte[] buffer, int key)
        {
            var hash = CalculateHashCode(buffer, key);
            return (m_buffers.ContainsKey(hash));
        }

        public static bool IsCached(ICacheableObject data)
        {
            var key = data.GetHashCode();
            return (m_buffers.ContainsKey(key));
        }
        
        public static void Cache(int offset, byte[] buffer, int key)
        {
            var size = buffer.Length;
            var checksum = CalculateHashCode(buffer, key);
            var entry = new CachedData(offset, size, checksum);

            m_buffers.Add(entry.Checksum, entry);
        }

        public static void Cache(int offset, ICacheableObject data)
        {
            var entry = new CachedData(offset, data);
            m_buffers.Add(entry.Checksum, entry);
        }

        public static CachedData PreCache(int offset, byte[] buffer, int key)
        {
            var hashKey = CalculateHashCode(buffer, key);

            // return the cached version
            if (m_buffers.ContainsKey(hashKey))
                return m_buffers[hashKey];

            // cache it and return an empty instance
            var size = buffer.Length;
            var entry = new CachedData(offset, size, hashKey);

            m_buffers.Add(entry.Checksum, entry);

            return CachedData.Empty;
        }
        
        public static CachedData GetData(byte[] buffer, int key)
        {
            var hashKey = CalculateHashCode(buffer, key);

            if (m_buffers.ContainsKey(hashKey))
                return m_buffers[hashKey];

            return CachedData.Empty;
        }

        public static CachedData GetData(ICacheableObject data)
        {
            var key = data.GetHashCode();

            if (m_buffers.ContainsKey(key))
                return m_buffers[key];

            return CachedData.Empty;
        }

        public static void Clear()
        {
            m_buffers.Clear();
        }
    }
}
