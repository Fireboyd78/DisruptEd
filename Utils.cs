using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DisruptEd.IO;

namespace DisruptEd
{
    public static class Utils
    {
        public static int GetSizeInMB(int size)
        {
            // fast way of doing (size * 1024) * 1024
            return (size << 20);
        }

        public static string Bytes2HexString(byte[] bytes)
        {
            var str = "";

            for (int i = 0; i < bytes.Length; i++)
            {
                str += $"{bytes[i]:X2}";

                if ((i + 1) != bytes.Length)
                    str += " ";
            }

            return str;
        }

        public static int GetTotalNumberOfNodes(NodeClass node)
        {
            var nChildren = 0;

            if ((node != null) && (node.Children.Count > 0))
            {
                foreach (var subNode in node.Children)
                {
                    nChildren += 1;

                    if (subNode.Children.Count > 0)
                        nChildren += GetTotalNumberOfNodes(subNode);
                }
            }

            return nChildren;
        }

        public static int GetTotalNumberOfAttributes(NodeClass node)
        {
            var nAttributes = 0;

            if (node != null)
            {
                nAttributes = node.Attributes.Count;

                if (node.Children.Count > 0)
                {
                    foreach (var subNode in node.Children)
                        nAttributes += GetTotalNumberOfAttributes(subNode);
                }
            }

            return nAttributes;
        }

        public static bool IsAttributeBufferStrangeSize(AttributeData data)
        {
            var fixedSize = false;
            var size = (data.Buffer != null) ? data.Buffer.Length : 0;

            var zeroOrFullSize = false;

            switch (data.Type)
            {
            // we don't know -- variable-sized buffer :(
            case DataType.BinHex:
            case DataType.String:
                return false;

            case DataType.Float:
                zeroOrFullSize = true;
                break;

            case DataType.Vector2:
            case DataType.Vector3:
            case DataType.Vector4:
                fixedSize = true;
                break;
            }

            var typeSize = GetAttributeTypeSize(data.Type);

            // if it's a fixed-size type, it must be
            if (fixedSize)
                return ((size != 0) && (size != typeSize));

            // zero is never strange!
            if (size == 0)
                return false;

            // biggest type should be 8-bytes long
            // so at this point, it's very strange!
            if ((size & 0xF) != size)
                return true;

            var isStrange = ((typeSize % size) == 1);

            // type can either be zero or its full-size
            if (zeroOrFullSize && (size != typeSize))
                isStrange = true;

            return isStrange;
        }

        public static DataType GetAttributeTypeBestGuess(AttributeData data)
        {
            var size = (data.Buffer != null) ? data.Buffer.Length : 0;
            return GetAttributeTypeBestGuess(size);
        }

        public static DataType GetAttributeTypeBestGuess(int size)
        {
            switch (size)
            {
            case 1:
                return DataType.Byte;
            case 2:
                return DataType.Int16;
            case 4:
                return DataType.Int32;
            case 12:
                return DataType.Vector3;
            case 16:
                return DataType.Vector4;
            }

            return DataType.BinHex;
        }

        // tries to minify the buffer
        public static byte[] GetAttributeDataMiniBuffer(byte[] buffer, DataType type)
        {
            // don't even think about it!
            if (type == DataType.BinHex)
                return buffer;

            var empty = new byte[0];

            var maxSize = GetAttributeTypeSize(type);

            // you're on your own, dude
            if ((maxSize != -1) && (buffer.Length > maxSize))
                return buffer;

            var attrBuf = GetAttributeDataBuffer(buffer, type);
            var value = 0uL;

            switch (type)
            {
            case DataType.Bool:
            case DataType.Byte:
                value = attrBuf[0];
                break;

            case DataType.Int16:
            case DataType.UInt16:
                value = BitConverter.ToUInt16(attrBuf, 0);
                break;

            case DataType.Int32:
            case DataType.UInt32:
            case DataType.StringHash:
                value = BitConverter.ToUInt32(attrBuf, 0);
                break;

            /* these types can only be minified if they're actually zero/empty */
            case DataType.String:
                {
                    var str = Encoding.UTF8.GetString(attrBuf);

                    if (String.IsNullOrEmpty(str))
                        return empty;

                    return attrBuf;
                }
            case DataType.Float:
                var fVal = BitConverter.ToSingle(attrBuf, 0);

                if (fVal == 0.0f)
                    return empty;

                return attrBuf;

            /* vectors cannot be minified :( */
            case DataType.Vector2:
            case DataType.Vector3:
            case DataType.Vector4:
                return buffer;
            }

            // now that's what I call small :)
            if (value == 0)
                return empty;

#if ALLOW_MINI_SIZE_DATA
            var miniSize = 8;

            // try to make the integer as small as possible
            if ((value & 0xFFFFFFFF) == value)
                miniSize -= 4;
            if ((value & 0xFFFF) == value)
                miniSize -= 2;
            if ((value & 0xFF) == value)
                miniSize -= 1;

            var miniBuf = new byte[miniSize];

            Array.Copy(attrBuf, miniBuf, miniSize);
            return miniBuf;
#else
            return attrBuf;
#endif
        }

        public static int GetAttributeTypeSize(DataType type)
        {
            switch (type)
            {
            case DataType.Bool:
            case DataType.Byte:
                return 1;

            case DataType.Int16:
            case DataType.UInt16:
                return 2;

            case DataType.Int32:
            case DataType.UInt32:
            case DataType.Float:
            case DataType.StringHash:
                return 4;

            /* shared with the other form hash not yet implemented */
            case DataType.Vector2:
                return 8;

            case DataType.Vector3:
                return 12;

            case DataType.Vector4:
                return 16;
            }

            // flexible size
            return -1;
        }

        public static string GetAttributeTypeDefault(DataType type)
        {
            switch (type)
            {
            case DataType.Bool:
            case DataType.Byte:
            case DataType.Int16:
            case DataType.UInt16:
            case DataType.Int32:
            case DataType.UInt32:
                return "0";

            case DataType.BinHex:
            case DataType.String:
            case DataType.StringHash:
                return "";

            case DataType.Vector2:
            case DataType.Vector3:
            case DataType.Vector4:
                return "";
            }

            return "???";
        }

        public static byte[] GetAttributeDataBuffer(byte[] buffer, DataType type)
        {
            var size = GetAttributeTypeSize(type);

            if (size == -1)
            {
                var len = (buffer != null) ? buffer.Length : 0;

                var newBuffer = new byte[len];

                // return a copy of the data if not null
                // otherwise return an empty array
                if (buffer != null)
                    Array.Copy(buffer, newBuffer, buffer.Length);

                return newBuffer;
            }

            var data = new byte[size];

            if (buffer != null)
            {
                var copySize = (buffer.Length > size) ? size : buffer.Length;

                Array.Copy(buffer, data, copySize);
            }

            // return a properly-sized buffer :)
            return data;
        }

        public static byte[] GetCountBuffer(int count)
        {
            if (count >= 254)
            {
                // you're gonna need a bigger buffer ;)
                var largeSize = (0xFF | ((count & 0xFFFFFF) << 8));

                return BitConverter.GetBytes(largeSize);
            }
            else
            {
                return new byte[1] { (byte)count };
            }
        }

        public static int MakeOffsetValue(int offset)
        {
            return (0xFE | ((offset & 0xFFFFFF) << 8));
        }
    }
}
