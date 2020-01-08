using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomad
{
    public class EnumValue<TEnum>
        where TEnum : Enum
    {
        public TEnum Type;

        public static implicit operator TEnum(EnumValue<TEnum> typeVal)
        {
            return typeVal.Type;
        }

        public static EnumValue<TEnum> Parse(string content)
        {
            return new EnumValue<TEnum>(content);
        }

        public override string ToString()
        {
            return Type.ToString();
        }

        private EnumValue(string content)
        {
            Type = (TEnum)Enum.Parse(typeof(TEnum), content);
        }

        public EnumValue(TEnum type)
        {
            Type = type;
        }
    }
}
