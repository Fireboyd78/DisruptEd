using System;

namespace Nomad
{
    public abstract class Node : IBinarySerializer
    {
        private string m_name;
        private int m_hash;

        internal int Offset { get; set; }

        public string Name
        {
            get { return m_name; }
            set
            {
                m_name = value;
                m_hash = (m_name != null) ? StringHasher.GetHash(m_name) : 0;
            }
        }

        public int Hash
        {
            get { return m_hash; }
            set
            {
                m_hash = value;
                m_name = $"_{m_hash:X8}";
            }
        }
        
        public abstract void Serialize(BinaryStream stream);
        public abstract void Deserialize(BinaryStream stream);

        public override string ToString()
        {
            return (m_name != null) ? m_name : String.Empty;
        }

        protected Node()
        {
        }

        protected Node(string name)
        {
            Name = name;
        }

        protected Node(int hash)
        {
            Hash = hash;
        }

        protected Node(int hash, string name)
        {
            m_name = name;
            m_hash = hash;
        }
    }
}
