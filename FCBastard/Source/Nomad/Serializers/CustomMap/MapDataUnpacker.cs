using System;
using System.Collections.Generic;

namespace Nomad
{
    internal class MapDataUnpacker
    {
        private Dictionary<string, NomadValue> _values;
        private Dictionary<string, NomadObject> _objects;

        public MapDataUnpacker GetObject(string name)
        {
            var obj = _objects[name];

            return new MapDataUnpacker(obj);
        }

        public AttributeData GetValue(string name)
        {
            return _values[name].Data;
        }

        public byte[] GetBuffer(string name)
        {
            return GetValue(name).Buffer;
        }

        public bool GetBool(string name)
        {
            return GetValue(name).ToBool();
        }

        public int GetInt(string name)
        {
            return GetValue(name).ToInt32();
        }
        
        public long GetLong(string name)
        {
            var value = GetValue(name);
            var buf = value.Buffer;

            return BitConverter.ToInt64(buf, 0);
        }

        public float GetFloat(string name)
        {
            return GetValue(name).ToFloat();
        }

        public string GetString(string name)
        {
            return GetValue(name).ToString();
        }

        public DateTime GetDateTime(string prefix)
        {
            int year = GetInt($"{prefix}Year") + 1900,
                month = GetInt($"{prefix}Mon") + 1,

                m_day = GetInt($"{prefix}MDay"), // day of month
                y_day = GetInt($"{prefix}YDay"), // day of year
                w_day = GetInt($"{prefix}WDay"), // day of week

                hour = GetInt($"{prefix}Hour"),
                min = GetInt($"{prefix}Min"),
                sec = GetInt($"{prefix}Sec");

            // ?!
            var dst = GetInt($"{prefix}IsDst");

            return new DateTime(year, month, m_day, hour, min, sec);
        }

        public Guid GetUID(string prefix)
        {
            var high = GetBuffer($"{prefix}High");
            var low = GetBuffer($"{prefix}Low");

            var buffer = new byte[16];

            Buffer.BlockCopy(low, 0, buffer, 0, low.Length);
            Buffer.BlockCopy(high, 0, buffer, 8, high.Length);

            return new Guid(buffer);
        }

        public MapDataUnpacker(NomadObject obj)
        {
            foreach (var child in obj)
            {
                var id = child.Id;

                if (child.IsAttribute)
                {
                    _values.Add(id.Name, child as NomadValue);
                }
                else
                {
                    _objects.Add(id.Name, child as NomadObject);
                }
            }
        }
    }
}
