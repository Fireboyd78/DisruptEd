using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using Nomad;

public static class Utils
{
    public static class Bastard
    {
        public static void Abort(int exitCode)
        {
            Console.WriteLine(">> Aborting...");
            Environment.Exit(exitCode);
        }
        
        public static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"ERROR: {message}");

            Console.ResetColor();
        }

        public static void Fail(string message)
        {
            Error(message);
            Environment.Exit(1);
        }

        public static void Warn(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);

            Console.ResetColor();
        }

        public static void Say(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(message);

            Console.ResetColor();
        }
    }

    public static int WriteFile(this Stream stream, string filename, int size)
    {
        var buffer = new byte[size];

        stream.Position = 0;
        stream.Read(buffer, 0, size);

        File.WriteAllBytes(filename, buffer);
        return size;
    }

    public static readonly XmlReaderSettings XMLReaderSettings
        = new XmlReaderSettings() {
            CloseInput = false,

            IgnoreComments = true,
            IgnoreWhitespace = false,

            DtdProcessing = DtdProcessing.Parse,
            ValidationType = ValidationType.DTD,
        };

    public static readonly XmlWriterSettings XMLWriterSettings
        = new XmlWriterSettings() {
            Indent = true,
            IndentChars = "\t",
            NewLineOnAttributes = true,
        };
    
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

    public static void SaveFormatted(this XDocument xml, string filename, bool newLineAttributes = false)
    {
        using (var xmlWriter = XmlWriter.Create(filename, new XmlWriterSettings() {
            Indent = true,
            IndentChars = "\t",

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
    
    public static int GetSizeInMB(int size)
    {
        // fast way of doing (size * 1024) * 1024
        return (size << 20);
    }

    static readonly char[] nib2chr = {
        '0', '1', '2', '3',
        '4', '5', '6', '7',
        '8', '9', 'A', 'B',
        'C', 'D', 'E', 'F',
    };

    private static byte[] chr2nib = null;
    private static byte[] chr2asc = null;

    private static void BuildLookup()
    {
        chr2nib = new byte[256];
        chr2asc = new byte[256];

        for (int i = 0; i < chr2nib.Length; i++)
        {
            var chr = (char)i;
            var hx = 0;

            if ((chr >= 'a') && (chr <= 'f'))
                hx = (chr - 'a') + 10;
            else if ((chr >= 'A') && (chr <= 'F'))
                hx = (chr - 'A') + 10;
            else if ((chr >= '0') && (chr <= '9'))
                hx = (chr - '0');
            else if (chr == ' ')
                hx = ' ';
            else if (char.IsControl(chr))
                hx = '?';
            else
                hx = 'X';

            chr2nib[i] = (byte)hx;
        }

        for (int i = 0; i < chr2asc.Length; i++)
        {
            var chr = (char)i;
            var hx = 0;

            if (chr <= '\x1F')
            {
                switch (chr)
                {
                case '\t':
                //case '\r':
                //case '\n':
                    hx = chr;
                    break;
                }
            }
            else if (chr < '\x7F')
                hx = chr;

            chr2asc[i] = (byte)hx;
        }
    }
    
    // >= 0: possibly good, -1: invalid character, -2 = missing terminator
    public static int CheckStringBuffer(byte[] buffer, bool checkAhnulld = true)
    {
        int len = 0;
        
        if ((buffer == null) || ((len = buffer.Length) == 0))
            return 0;

        if (chr2asc == null)
            BuildLookup();

        var last = (len - 1);
        var end = last;

        var count = end;

        while ((end >= 0) && (buffer[end] == '\0'))
            count = end--;
        
        if (checkAhnulld && (end == last))
            return -2; // thou shalt be terminated!
        
        // check string for invalid characters
        // (null-terminator won't cause it to be invalid)
        for (int i = 0; i < count; i++)
        {
            var c = buffer[i];
            var asc = chr2asc[c];

            if ((asc == 0) || (asc != c))
                return -1;
        }
        
        // looks good to me ¯\_(ツ)_/¯
        return count;
    }

    public static int CheckString(string value)
    {
        int len = 0;

        if ((value == null) || ((len = value.Length) == 0))
            return 0;

        if (chr2asc == null)
            BuildLookup();

        // check string for invalid characters
        for (int i = 0; i < len; i++)
        {
            var c = value[i];
            var asc = chr2asc[c];

            if ((asc == 0) || (asc == '?'))
                return -1;
        }

        return len;
    }

    public static bool IsHexString(string value)
    {
        if ((value == null) || (value.Length == 0))
            return false;

        if (chr2nib == null)
            BuildLookup();

        var isByteRun = (value[0] == '#');
        var whitespace = false;

        if (isByteRun)
            value = value.Substring(1);

        for (int i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            var hx = chr2nib[ch];

            // nope!
            if ((hx == '?') || (hx == 'X'))
                return false;

            if (hx == ' ')
            {
                // byte runs cannot have spaces in them
                if (isByteRun)
                    return false;

                whitespace = true;
            }
        }

        if (!isByteRun)
        {
            // if there's more than 2 bytes but no whitespace, it's not a hex string
            if ((value.Length > 2) && !whitespace)
                return false;
        }

        return true;
    }
    
    public static unsafe string Bytes2HexString(byte[] bytes)
    {
        if ((bytes == null) || (bytes.Length == 0))
            return String.Empty;
        
        if (bytes.Length > 8)
        {
            var strLen = (bytes.Length * 3);
            var strBuf = new char[strLen];

            var idx = 0;

            for (int i = 0; i < bytes.Length; i++)
            {
                var ch = bytes[i];
                
                // little-endian
                strBuf[idx++] = nib2chr[(ch >> 4) & 0xF];
                strBuf[idx++] = nib2chr[(ch >> 0) & 0xF];

                // add space?
                if ((i + 1) < bytes.Length)
                    strBuf[idx++] = ' ';
            }

            return new string(strBuf, 0, (strLen - 1));
        }
        else
        {
            var strLen = (bytes.Length * 2) + 1;
            var strBuf = new char[strLen];

            var idx = 0;

            // we don't need '#' for single bytes
            if (bytes.Length > 1)
                strBuf[idx++] = '#';

            var len = idx;
            
            for (int i = bytes.Length; i > 0; i--)
            {
                var ch = bytes[i - 1];
                
                // little-endian
                strBuf[len++] = nib2chr[(ch >> 4) & 0xF];
                strBuf[len++] = nib2chr[(ch >> 0) & 0xF];
            }

            var ptr = (len > (idx + 1)) ? 0 : 1;

            // return a string representing zero
            if ((len == idx) || ((len - ptr) == 0))
                return "0";
            
            return new string(strBuf, ptr, len - ptr);
        }
    }

    public static int ParseHexByte(string value, int index, int length, out byte result)
    {
        if (length == 0)
            throw new ArgumentException("Length must be greater-than zero.", nameof(length));
        if (index >= length)
            throw new ArgumentException("Index must be less than the total length.", nameof(index));
        
        var len = 0;
        byte val = 0;

        var nibs = 1;

        var rem = (length - index);

        if ((rem > 1) && ((rem % 2) == 0))
            nibs = 2;

        for (int n = 0; n < nibs; n++)
        {
            var c = value[index + n];
            var hx = chr2nib[c];

            // make sure it's not invalid
            if (hx == '?')
                throw new InvalidDataException($"Hex string malformed - invalid control code '{c:X2}'.");
            if (hx == 'X')
                throw new InvalidDataException($"Hex string malformed - invalid character '{c}'.");

            // delimited by spaces
            if (hx == ' ')
                break;
            
            // 2 nibbles instead of one (little-endian)
            if (n > 0)
                val <<= 4;

            val |= hx;
            len++;
        }

        // set value
        result = val;

        // return number of nibbles parsed;
        // zero means whitespace / delimiter only
        return len;
    }

    public static byte[] ParseHexString(string value, int index, int length, bool swapOrder)
    {
        if (chr2nib == null)
            BuildLookup();

        // hopefully this doesn't cause bugs :P
        if (length == 0)
            return new byte[0];

        // parse single nibbles quickly
        if (length == 1)
        {
            byte val = 0;

            if (ParseHexByte(value, index, length, out val) == 0)
                throw new InvalidDataException("Hex strings cannot be made up entirely of whitespace.");

            return new byte[1] { val };
        }
        
        var hexIdx = 0;
        
        // store parsed data in a temporary buffer;
        // we may need to make adjustments afterwards
        var hexBuf = new byte[length];

        while (index < length)
        {
            byte val = 0;
            var count = ParseHexByte(value, index, length, out val);

            if (count > 0)
            {
                // append to buffer,
                // then advance our position
                hexBuf[hexIdx++] = val;
                index += count;
            }
            else
            {
                // skip whitespace character
                index++;
            }
        }

        if (hexIdx == 0)
            throw new InvalidDataException("Hex strings cannot be made up entirely of whitespace.");

        // build a valid buffer from what we parsed
        var size = hexIdx;

        // make sure buffer is properly sized;
        // align to a 4-bit boundary if necessary
        if (size > 2)
            size = (size + 3) & ~0x3;

        var result = new byte[size];

        if (swapOrder)
        {
            // fill our buffer in reverse
            var lastIdx = (hexIdx - 1);
            
            for (int i = 0; i < hexIdx; i++)
                result[lastIdx - i] = hexBuf[i];
        }
        else
        {
            // buffer can be copied directly
            Buffer.BlockCopy(hexBuf, 0, result, 0, hexIdx);
        }

        return result;
    }
    
    public static byte[] HexString2Bytes(string value)
    {
        if (String.IsNullOrEmpty(value))
            return new byte[0];

        var length = value.Length;

        var swapOrder = false;

        // '#DEADBEEF' format
        // same as typing out 'EF BE AD DE'
        if (value[0] == '#')
        {
            value = value.Substring(1);
            length--;

            swapOrder = true;
        }
        else if (length > 1)
        {
            var len = length - (length / 3);
            var buf = new char[len];

            var strIdx = 0;
            var hexIdx = 0;

            // strip whitespace
            while (strIdx < len)
            {
                var b = value[hexIdx++];

                if (b == ' ')
                    continue;

                buf[strIdx++] = b;
            }

            value = new string(buf, 0, strIdx);
            length = value.Length;
        }

        // empty buffer
        if (length == 0)
            return new byte[0];

        return ParseHexString(value, 0, length, swapOrder);
    }

    public static byte[] GetStringBuffer(string value)
    {
        // empty/null strings MUST have a null-terminator!
        if ((value == null) || (value.Length == 0))
            return new byte[1] { 0 };

        var strBuf = Encoding.UTF8.GetBytes(value);

        var buf = new byte[strBuf.Length + 1];

        Buffer.BlockCopy(strBuf, 0, buf, 0, strBuf.Length);

        return buf;
    }
    
    public static int GetTotalNumberOfNodes<T>(T node)
        where T : Node, IGetChildren<T>
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
    public unsafe static byte[] GetAttributeDataMiniBuffer(byte[] buffer, DataType type)
    {
        // don't even think about it!
        if ((type == DataType.BinHex) || (type == DataType.RML))
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
        
        // don't try to minify unsupported types
        switch (type)
        {
        case DataType.Float:
        case DataType.Vector2:
        case DataType.Vector3:
        case DataType.Vector4:
        case DataType.String:
        case DataType.PathId:
            return buffer;
        }

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

        fixed (byte* buf = attrBuf)
        {
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
            case DataType.UInt16:
                {
                    var value = *(ushort*)buf;

                    if ((ushort)(value & 0xFF) == value)
                        miniSize = 1;
                }
                break;
            case DataType.Int32:
            case DataType.UInt32:
            case DataType.StringId:
                {
                    var value = *(uint*)buf;

                    if ((value & 0xFF) == value)
                        miniSize = 1;
                    else if ((value & 0xFFFF) == value)
                        miniSize = 2;
                }
                break;
            }
        }

        // in case we somehow missed an edge case
        if (miniSize == 0)
            return empty;

        if (miniSize < attrBuf.Length)
        {
            var miniBuf = new byte[miniSize];

            Buffer.BlockCopy(attrBuf, 0, miniBuf, 0, miniSize);
            return miniBuf;
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
        case DataType.StringId:
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

        case DataType.StringId:
        case DataType.PathId:
            return "$00000000";
            
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
                && ((type == DataType.String) || (type == DataType.RML)))
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

                    Buffer.BlockCopy(buffer, 0, newBuffer, 0, copyLen);
                }
            }

            return newBuffer;
        }

        var data = new byte[size];

        if (buffer != null)
        {
            var copySize = (buffer.Length > size) ? size : buffer.Length;

            Buffer.BlockCopy(buffer, 0, data, 0, copySize);
        }

        // return a properly-sized buffer :)
        return data;
    }

    public static int GetHash(string value)
    {
        var buf = Encoding.UTF8.GetBytes(value);
        var crc = (int)Memory.GetCRC32(buf);

        return crc;
    }

    public static string GetFileExtension(string path)
    {
        var name = Path.GetFileName(path);
        var idx = name.IndexOf('.');

        if (idx == -1)
            return String.Empty;

        var ext = name.Substring(idx);

        return ext.ToLower();
    }

    public static XDocument LoadXml(Stream stream)
    {
        using (var reader = XmlReader.Create(stream, XMLReaderSettings))
            return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }
    
    public static bool TryLoadXml(Stream stream, out XDocument xDoc)
    {
        using (var reader = XmlReader.Create(stream, XMLReaderSettings))
        {
            try
            {
                xDoc = XDocument.Load(reader, LoadOptions.SetLineInfo);
                return true;
            }
            catch (XmlException) { /* not XML data! */ }
        }

        xDoc = null;
        return false;
    }

    public static XDocument LoadXmlFile(string filename)
    {
        using (var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            return LoadXml(fs);
    }

    public static bool TryLoadXmlFile(string filename, out XDocument xDoc)
    {
        using (var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            return TryLoadXml(fs, out xDoc);
    }

    public static bool IsXmlFile(string filename)
    {
        return TryLoadXmlFile(filename, out XDocument xDoc);
    }

    public static T UnpackData<T>(NomadValue value, Func<byte[], int, T> converter)
    {
        var data = value.Data;
        var buffer = data.Buffer;

        return converter(buffer, 0);
    }

    public static T[] UnpackArray<T>(NomadValue value, Func<byte[], int, T> converter, int size, out int count)
    {
        var data = value.Data;
        var buffer = data.Buffer;

        count = BitConverter.ToInt32(buffer, 0);

        var offset = 4;
        var result = new T[count];

        for (int i = 0; i < count; i++)
        {
            result[i] = converter(buffer, offset);
            offset += size;
        }

        return result;
    }

    public static int PackArray<T>(NomadValue value, List<T> array, Func<T, byte[]> converter, int size)
    {
        var data = value.Data;
        
        var offset = 4;
        var count = array.Count;
        
        var result = new byte[(count * size) + 4];

        Buffer.BlockCopy(BitConverter.GetBytes(count), 0, result, 0, 4);

        for (int i = 0; i < count; i++)
        {
            var val = array[i];
            var buffer = converter(val);

            Buffer.BlockCopy(buffer, 0, result, offset, size);

            offset += size;
        }

        value.Data = new AttributeData(DataType.Array, result);

        return count;
    }

#if USE_CONCEPTUAL_DATA_TYPE_PREFIXES
    // ***********************************
    // MUST MATCH 'DataType' ENUM EXACTLY!
    // ***********************************
    private static readonly List<String> m_typeNameIndex = new List<string>() {
        String.Empty,

        "bool",
        "byte",

        "int16",
        "int32",
        "uint16",
        "uint32",

        "float",

        "string",
        "id",

        "vec2",
        "vec3",
        "vec4",

        String.Empty,
    };
    
    public static string GetAttributeTypeAsString(DataType type)
    {
        return m_typeNameIndex[(int)type];
    }

    public static DataType? GetAttributeTypeFromString(string value)
    {
        if (!String.IsNullOrEmpty(value))
        {
            var idx = -1;

            if ((idx = m_typeNameIndex.IndexOf(value)) != -1)
                return (DataType)idx;
        }

        return null;
    }
#endif
}
