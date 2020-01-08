namespace System
{
    public struct Identifier
    {
        int m_value;

        public static implicit operator int(Identifier ident)
        {
            return ident.m_value;
        }

        public static implicit operator string(Identifier ident)
        {
            var str = "";

            for (int i = 0, s = 24; i < 4; i++, s -= 8)
                str += (char)((ident.m_value >> s) & 0xFF);

            return str;
        }

        public static implicit operator Identifier(int value)
        {
            return new Identifier(value);
        }

        public static implicit operator Identifier(string value)
        {
            return new Identifier(value);
        }

        public Identifier(int value)
        {
            m_value = value;
        }

        public Identifier(string value)
        {
            if (value == null || value.Length > 4)
                throw new ArgumentException("Identifier strings cannot be null or greater than 4 characters long.", nameof(value));

            m_value = 0;

            for (int i = 0, s = 24; i < value.Length; i++, s -= 8)
                m_value |= (value[i] << s);
        }
    }
}
