using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisruptEd.IO
{
    public interface ICacheableObject
    {
        int Size { get; }
        int GetHashCode();
    }

    public struct CachedData
    {
        private static readonly Type CacheType = typeof(ICacheableObject);

        public static readonly CachedData Empty = new CachedData(0, 0, -1);

        public int Offset;
        public int Size;
        public int Checksum;

        public ICacheableObject Object;

        public override bool Equals(object obj)
        {
            var objType = obj.GetType();

            if (CacheType.IsAssignableFrom(objType))
            {
                var data = (ICacheableObject)obj;
                return (Checksum == data.GetHashCode());
            }
            
            return false;
        }

        public override int GetHashCode()
        {
            return Checksum;
        }

        public CachedData(int offset, int size, int checksum)
        {
            Offset = offset;
            Size = size;
            Checksum = checksum;
            Object = null;
        }

        public CachedData(int offset, ICacheableObject data)
        {
            Offset = offset;
            Size = data.Size;
            Checksum = data.GetHashCode();
            Object = data;
        }
    }

    public static class WriteCache
    {
        static Dictionary<int, CachedData> m_buffers = new Dictionary<int, CachedData>();

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
