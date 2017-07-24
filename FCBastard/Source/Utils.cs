using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using DisruptEd.IO;

namespace DisruptEd
{
    public static class Utils
    {
        public static void SaveFormatted(this XmlDocument xml, string filename, bool newLineAttributes = false)
        {
            using (var xmlWriter = XmlWriter.Create(filename, new XmlWriterSettings() {
                Indent              = true,
                IndentChars         = "\t",

                NewLineOnAttributes = newLineAttributes,
            }))
            {
                xml.WriteTo(xmlWriter);
                xmlWriter.Flush();
            }
        }

        public static string FormatVector(params float[] inputs)
        {
            var vecs = new string[inputs.Length];

            for (int v = 0; v < inputs.Length; v++)
                vecs[v] = inputs[v].ToString("0.0###########");

            return String.Join(",", vecs);
        }

        public static string ToBoolStr(bool value)
        {
            return (value) ? "1" : "0";
        }

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

        public static byte[] HexString2Bytes(string value)
        {
            var hexBytes = value.Split(' ');
            var length = hexBytes.Length;

            var bytes = new byte[length];

            for (int i = 0; i < length; i++)
                bytes[i] = byte.Parse(hexBytes[i], NumberStyles.HexNumber);

            return bytes;
        }

        public static byte[] GetStringBuffer(string value)
        {
            // empty/null strings MUST have a null-terminator!
            if ((value == null) || (value.Length == 0))
                return new byte[1] { 0 };

            var strBuf = Encoding.UTF8.GetBytes(value);

            var buf = new byte[strBuf.Length + 1];
            Array.Copy(strBuf, buf, strBuf.Length);

            return buf;
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

            // empty strings _must_ have at least 1 byte
            // or else the game will shit the bed!!!
            if (type == DataType.String)
            {
                if ((attrBuf.Length == 1)
                    && (attrBuf[0] == 0))
                    return attrBuf;
            }

            var canMinify = true;
            
            switch (type)
            {
            case DataType.Float:
            case DataType.Vector2:
            case DataType.Vector3:
            case DataType.Vector4:
            case DataType.String:
                canMinify = false;
                break;
            }

            if (canMinify)
            {
                // completely empty?
                var isEmpty = true;

                for (int i = 0; i < attrBuf.Length; i++)
                {
                    if (attrBuf[i] != 0)
                    {
                        isEmpty = false;
                        break;
                    }
                }

                if (isEmpty)
                    return empty;

                // not empty, let's try minifying it
                var miniSize = attrBuf.Length;

                switch (type)
                {
                case DataType.Bool:
                case DataType.Byte:
                    {
                        // we've already determined it's not empty
                        // and well, a byte can't be much smaller...
                        return new byte[1] { attrBuf[0] };
                    }
                case DataType.Int16:
                    {
                        var value = BitConverter.ToInt16(attrBuf, 0);

                        if ((short)(value & 0xFF) == value)
                            miniSize = 1;
                    }
                    break;
                case DataType.UInt16:
                    {
                        var value = BitConverter.ToUInt16(attrBuf, 0);

                        if ((ushort)(value & 0xFF) == value)
                            miniSize = 1;
                    }
                    break;
                case DataType.Int32:
                    {
                        var value = BitConverter.ToInt32(attrBuf, 0);

                        if ((value & 0xFFFF) == value)
                            miniSize = 2;
                        if ((value & 0xFF) == value)
                            miniSize = 1;
                    }
                    break;
                case DataType.UInt32:
                case DataType.StringHash:
                    {
                        var value = BitConverter.ToUInt32(attrBuf, 0);

                        if ((value & 0xFFFF) == value)
                            miniSize = 2;
                        if ((value & 0xFF) == value)
                            miniSize = 1;
                    }
                    break;
                }

                // in case we somehow missed an edge case
                if (miniSize == 0)
                    return empty;

                if (miniSize < attrBuf.Length)
                {
                    var miniBuf = new byte[miniSize];

                    Array.Copy(attrBuf, miniBuf, miniSize);
                    return miniBuf;
                }
            }
            
            return buffer;
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

            // trying to fix a bug
            case DataType.StringHash:
                return "$0";

            case DataType.Vector2:
                return FormatVector(0, 0);
            case DataType.Vector3:
                return FormatVector(0, 0, 0);
            case DataType.Vector4:
                return FormatVector(0, 0, 0, 0);
            }

            // binhex & string
            return "";
        }

        public static byte[] GetAttributeDataBuffer(byte[] buffer, DataType type)
        {
            var size = GetAttributeTypeSize(type);
            
            if (size == -1)
            {
                var len = (buffer != null) ? buffer.Length : 0;

                if ((len == 0)
                    && (type == DataType.String))
                {
                    // strings must have at least 1 byte
                    len = 1;
                }

                var newBuffer = new byte[len];

                // return a copy of the data if not null
                // otherwise return an empty array
                if (len != 0)
                {
                    if (buffer != null)
                    {
                        var copyLen = (buffer.Length < len) ? buffer.Length : len;
                        Array.Copy(buffer, newBuffer, copyLen);
                    }
                }

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
