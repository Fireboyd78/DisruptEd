using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Nomad
{
    public class NomadRmlSerializer : NomadSerializer
    {
        Dictionary<int, string> _strings = null;

        int _elemCount = 0;
        int _attrCount = 0;
        
        public override FileType Type => FileType.Binary;

        public byte Reserved { get; set; }
        
        public override void Serialize(Stream stream, NomadObject data)
        {
            if (Context.State == ContextStateType.End)
                Context.Reset();
            
            if (data.Id != "RML_DATA")
                throw new InvalidOperationException("RML data wasn't prepared before initializing.");

            if ((data.Children.Count != 1) || (data.Attributes.Count != 0))
                throw new InvalidOperationException("RML data is malformed and cannot be serialized properly.");

            var _stream = (stream as BinaryStream)
                ?? new BinaryStream(stream);

            var rmlRoot = data.Children[0];

            if (!rmlRoot.IsRml)
                throw new InvalidOperationException("You can't serialize non-RML data as RML data, dumbass!");

            _strings.Clear();
            
            var strLookup = new Dictionary<string, int>();
            var strPtr = 0;
            
            var getStrIdx = new Func<string, int>((str) => {
                var ptr = 0;

                if (str == null)
                    str = String.Empty;

                if (strLookup.ContainsKey(str))
                {
                    ptr = strLookup[str];
                }
                else
                {
                    // add to lookup
                    ptr = strPtr;
                    strLookup.Add(str, strPtr);

                    // add to string table
                    _strings.Add(strPtr, str);

                    // must have null-terminator!
                    var strLen = 1;

                    if (str != null)
                        strLen += str.Length;

                    strPtr += strLen;
                }

                return ptr;
            });

            var entries = new List<NomadData>();

            var elemsCount = 1;
            var attrsCount = 0;

            entries.Add(rmlRoot);

            // iterates through attributes then children (and children's children, etc.)
            foreach (var nd in rmlRoot)
            {
                if (!nd.IsRml)
                    throw new InvalidOperationException("Can't serialize non-RML data!");

                if (nd.IsAttribute)
                    ++attrsCount;
                else if (nd.IsObject)
                    ++elemsCount;

                entries.Add(nd);
            }

            // rough size estimate            
            var rmlSize = ((elemsCount * 4) + (attrsCount * 2));

            var strTableLen = -1;

            byte[] rmlBuffer = null;

            using (var ms = new BinaryStream(rmlSize))
            {
                var writeInt = new Action<int>((ptr) => {
                    var nD = DescriptorTag.Create(ptr);
                    nD.WriteTo(ms);
                });

                var writeRml = new Action<NomadData>((nd) => {
                    var nameIdx = getStrIdx(nd.Id);
                    var valIdx = -1;
                    
                    if (nd.IsObject)
                    {
                        Context.State = ContextStateType.Object;
                        Context.ObjectIndex++;

                        var obj = (NomadObject)nd;
                        
                        valIdx = getStrIdx(obj.Tag);

                        writeInt(nameIdx);
                        writeInt(valIdx);
                        writeInt(obj.Attributes.Count);
                        writeInt(obj.Children.Count);
                    }
                    else if (nd.IsAttribute)
                    {
                        Context.State = ContextStateType.Member;
                        Context.MemberIndex++;

                        var attr = (NomadValue)nd;
                        
                        valIdx = getStrIdx(attr.Data);

                        // required for attributes
                        ms.WriteByte(0);
                        
                        writeInt(nameIdx);
                        writeInt(valIdx);
                    }
                });

                writeRml(rmlRoot);

                // enumerates attributes, then children (+ nested children)
                foreach (var rml in rmlRoot)
                    writeRml(rml);

                // setup string table size
                strTableLen = strPtr;

                // write out string table
                foreach (var kv in _strings)
                {
                    var str = kv.Value;

                    var strLen = (str != null) ? str.Length : 0;
                    var strBuf = new byte[strLen + 1];

                    if (strLen > 0)
                        Encoding.UTF8.GetBytes(str, 0, strLen, strBuf, 0);

                    ms.Write(strBuf);
                }

                // commit buffer
                rmlBuffer = ms.ToArray();
                rmlSize = rmlBuffer.Length;
            }

            var bufSize = 5; // header + 3 small ints

            // expand size as needed
            if (strTableLen >= 254)
                bufSize += 4;
            if (elemsCount >= 254)
                bufSize += 4;
            if (attrsCount >= 254)
                bufSize += 4;

            // calculate the final size (hopefully)
            bufSize += rmlSize;

            byte[] result = null;

            using (var ms = new BinaryStream(bufSize))
            {
                ms.WriteByte(0);
                ms.WriteByte(Reserved);

                DescriptorTag[] descriptors = {
                    DescriptorTag.Create(strTableLen),
                    DescriptorTag.Create(elemsCount),
                    DescriptorTag.Create(attrsCount),
                };

                foreach (var desc in descriptors)
                    desc.WriteTo(ms);

                // write RML data (+ string table)
                ms.Write(rmlBuffer);

                // profit!!!
                result = ms.ToArray();
            }
            
            _stream.Write(result, 0, result.Length);

            Context.State = ContextStateType.End;
        }

        protected NomadValue ReadRmlAttribute(BinaryStream _stream, NomadObject parent = null)
        {
            Context.State = ContextStateType.Member;
            Context.ObjectIndex++;

            var unk = (byte)_stream.ReadByte();

            if (unk != 0)
                throw new InvalidOperationException("Invalid RML attribute data.");

            var nameIdx = DescriptorTag.Read(_stream, ReferenceType.Index);
            var valIdx = DescriptorTag.Read(_stream, ReferenceType.Index);

            var buffer = Utils.GetStringBuffer(_strings[valIdx]);

            var result = new NomadValue(DataType.RML, buffer) {
                Id = _strings[nameIdx],
            };

            if (parent != null)
                parent.Attributes.Add(result);
            
            return result;
        }
        
        protected NomadObject ReadRmlObject(BinaryStream _stream, NomadObject parent = null)
        {
            Context.State = ContextStateType.Object;
            Context.ObjectIndex++;

            var nameIdx = DescriptorTag.Read(_stream, ReferenceType.Index);
            var valIdx = DescriptorTag.Read(_stream, ReferenceType.Index);

            var nAttrs = DescriptorTag.Read(_stream, ReferenceType.Index);
            var nElems = DescriptorTag.Read(_stream, ReferenceType.Index);

            _attrCount += nAttrs;
            _elemCount += nElems;
            
            var result = new NomadObject(true) {
                Id = _strings[nameIdx],
                Tag = _strings[valIdx],
            };
            
            if (parent != null)
                parent.Children.Add(result);

            for (int n = 0; n < nAttrs; n++)
                ReadRmlAttribute(_stream, result);
            for (int o = 0; o < nElems; o++)
                ReadRmlObject(_stream, result);
            
            return result;
        }
        
        public override NomadObject Deserialize(Stream stream)
        {
            if (Context.State == ContextStateType.End)
                Context.Reset();

            var _stream = (stream as BinaryStream)
                ?? new BinaryStream(stream);

            if (_stream.ReadByte() != 0)
                throw new InvalidOperationException("Invalid RML data.");

            Reserved = (byte)_stream.ReadByte();

            var sD = DescriptorTag.Read(_stream, ReferenceType.Index);
            var strTableLen = sD.Value;
            var strTablePtr = (_stream.Length - strTableLen);

            var nElems = DescriptorTag.Read(_stream, ReferenceType.Index);
            var nAttrs = DescriptorTag.Read(_stream, ReferenceType.Index);

            // save position so we can parse strings first
            var rmlDataPtr = (int)_stream.Position;
            
            // move to beginning of string table
            _stream.Position = strTablePtr;

            var strPtr = 0;

            // read in all strings and store them by their relative offset
            // TODO: convert to more efficient reads from buffer
            while (strPtr < strTableLen)
            {
                var str = "";
                var strLen = 1; // include null-terminator

                char c;

                while ((c = _stream.ReadChar()) != '\0')
                {
                    str += c;
                    ++strLen;
                }

                _strings.Add(strPtr, str);

                strPtr += strLen;
            }

            // parse RML data
            _stream.Position = rmlDataPtr;
            var rmlData = ReadRmlObject(_stream);

            var result = new NomadObject(true) {
                Id = "RML_DATA"
            };

            result.Children.Add(rmlData);

            Context.State = ContextStateType.End;

            return result;
        }

        public NomadRmlSerializer()
            : base()
        {
            _strings = new Dictionary<int, string>();

            Format = FormatType.RML;
        }
    }
}