using System;

namespace Nomad
{
    public struct CachedData
    {
        private static readonly Type CacheType = typeof(ICacheableObject);

        public static readonly CachedData Empty = new CachedData(0, 0, -1);

        public int Offset;
        public int Size;
        public int Checksum;

        public ICacheableObject Object;

        public bool IsEmpty
        {
            get { return ((Offset + Size) == 0) && (Checksum == -1); }
        }

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
}
