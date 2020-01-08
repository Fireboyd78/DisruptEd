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
    public class NomadResourceSerializer : NomadSerializer
    {
        public override FileType Type => FileType.Binary;
        
        protected void WriteRmlData(BinaryStream stream, NomadObject data)
        {
            byte[] rmlBuffer = null;

            using (var bs = new BinaryStream(1024))
            {
                var rmlData = new NomadRmlSerializer();
                rmlData.Serialize(bs, data);

                rmlBuffer = bs.ToArray();
            }
            
            var size = DescriptorTag.Create(rmlBuffer.Length);
            var next = DescriptorTag.Create(size);

            next.WriteTo(stream);
            size.WriteTo(stream);

            stream.Write(rmlBuffer, 0, rmlBuffer.Length);
        }

        protected void WriteAttributeRmlData(BinaryStream stream, NomadValue attr)
        {
            var rmlBuffer = attr.Data.Buffer;

            var rmlSize = DescriptorTag.Create(rmlBuffer.Length);
            rmlSize.WriteTo(stream);

            stream.Write(rmlBuffer);
        }

        protected void WriteAttributeData(BinaryStream stream, NomadValue attr, int baseOffset = 0)
        {
            var ptr = (int)stream.Position + baseOffset;

            var data = attr.Data;
            var type = data.Type;
            var size = data.Size;

            var buffer = data.Buffer;

            if (data.Type == DataType.RML)
                throw new InvalidOperationException("Cannot serialize RML data directly!");

            var oldSize = size;

            var attrData = (Format != FormatType.Resource)
                ? Utils.GetAttributeDataMiniBuffer(buffer, type)
                : Utils.GetAttributeDataBuffer(buffer, type);

            size = attrData.Length;

            var writeData = true;

            if (size > 4)
            {
                // return cached instance, else cache it and return empty
                var cache = WriteCache.PreCache(ptr, attrData, size);

                if (!cache.IsEmpty)
                {
                    // sizes must match
                    if (cache.Size == size)
                    {
                        var nD = DescriptorTag.CreateReference(cache.Offset, ReferenceType.Offset);
                        nD.WriteTo(stream, baseOffset);
            
                        writeData = false;
                    }
                }
            }

            if (writeData)
            {
                var nD = DescriptorTag.Create(size);
                nD.WriteTo(stream);

                stream.Write(attrData);
            }
        }

        protected void WriteAttribute_FmtA(BinaryStream stream, NomadValue attr)
        {
            Context.State = ContextStateType.Member;
            Context.MemberIndex++;
            
            stream.Write(attr.Id.Hash);

            if (attr.IsRml)
            {
                WriteAttributeRmlData(stream, attr);
            }
            else
            {
                WriteAttributeData(stream, attr);
            }
        }

        protected void WriteAttribute_FmtB(BinaryStream stream, NomadValue attr, int baseOffset)
        {
            Context.State = ContextStateType.Member;
            Context.MemberIndex++;

            WriteAttributeData(stream, attr, baseOffset);
        }
        
        protected void WriteAttributesData_FmtB(BinaryStream stream, List<NomadValue> attributes, int baseOffset)
        {
            var ptr = (int)stream.Position + baseOffset;
            var nAttrs = attributes.Count;
            
            if (nAttrs > 0)
            {
                var attrData = new byte[nAttrs * 4];

                using (var bs = new BinaryStream(attrData))
                {
                    foreach (var attr in attributes)
                        bs.Write(attr.Id.Hash);
                }

                var cache = WriteCache.PreCache(ptr, attrData, nAttrs);

                if (!cache.IsEmpty)
                {
                    var ndAttrs = DescriptorTag.CreateReference(cache.Offset, ReferenceType.Offset);
                    ndAttrs.WriteTo(stream, baseOffset);
                }
                else
                {
                    var count = DescriptorTag.Create(nAttrs);
                    count.WriteTo(stream);

                    stream.Write(attrData);
                }

                foreach (var attr in attributes)
                    WriteAttribute_FmtB(stream, attr, baseOffset);
            }
            else
            {
                // nothing to write
                stream.WriteByte(0);
            }
        }

        protected bool WriteAttributesList_FmtB(BinaryStream stream, NomadObject obj)
        {
            var ptr = (int)stream.Position;

            byte[] buffer = new byte[0];

            using (var bs = new BinaryStream(1024))
            {
                WriteAttributesData_FmtB(bs, obj.Attributes, ptr);
                buffer = bs.ToArray();
            }

            var length = buffer.Length;

            if (length > 65535)
                throw new InvalidOperationException("Attribute data too large.");

            stream.Write((short)length);
            stream.Write(buffer, 0, length);

            // did we write any data?
            return (length > 0);
        }

        protected void WriteObject_FmtA(BinaryStream stream, NomadObject obj)
        {
            Context.State = ContextStateType.Object;
            Context.ObjectIndex++;

            var ptr = (int)stream.Position;
            var idx = NomadCache.Find(obj);

            if (idx != -1)
            {
                var cached = NomadCache.Refs[idx];
                
                var tag = DescriptorTag.CreateReference(Context.GetIdx(cached), ReferenceType.Index);
                tag.WriteTo(stream);
            }
            else
            {
                var nChildren = DescriptorTag.Create(obj.Children.Count);
                var nAttributes = DescriptorTag.Create(obj.Attributes.Count);

                Context.AddRef(obj, ptr);

                nChildren.WriteTo(stream);
                stream.Write(obj.Id.Hash);

                if (obj.IsRml)
                {
                    WriteRmlData(stream, obj);
                }
                else
                {
                    nAttributes.WriteTo(stream);

                    Context.State = ContextStateType.Member;

                    foreach (var attr in obj.Attributes)
                        WriteAttribute_FmtA(stream, attr);

                    foreach (var child in obj.Children)
                        WriteObject_FmtA(stream, child);
                }
            }
        }

        protected void WriteObject_FmtB(BinaryStream stream, NomadObject obj)
        {
            Context.State = ContextStateType.Object;
            Context.ObjectIndex++;

            var ptr = (int)stream.Position;
            var idx = NomadCache.Find(obj);

            if (idx != -1)
            {
                var cached = NomadCache.Refs[idx];
                var offset = (int)Context.GetPtr(cached);

                var tag = DescriptorTag.CreateReference(offset, ReferenceType.Offset);
                tag.WriteTo(stream);
            }
            else
            {
                var reference = new NomadReference(obj);
                var cached = reference.Get();

                Context.AddRef(cached, ptr);

                var count = DescriptorTag.Create(obj.Children.Count);
                count.WriteTo(stream);

                stream.Write(obj.Id.Hash);

                WriteAttributesList_FmtB(stream, obj);

                foreach (var child in obj.Children)
                    WriteObject_FmtB(stream, child);
            }
        }
        
        protected void WriteHeader(BinaryStream stream, NomadObject data)
        {
            var childCount = 0;
            var attrCount = data.Attributes.Count;

            if (attrCount == 0)
                attrCount = 1;

            foreach (var obj in data)
            {
                if (obj.IsObject)
                {
                    childCount++;
                    continue;
                }
            }
            
            var totalCount = (childCount + attrCount);

            int magic = Nomad.Magic;
            int type = (ushort)Format;
            
            if (Nomad.WriteSealOfApproval)
            {
                // won't trip the "debug info" flag for version 2 resources;
                // has no effect on versions 3 and 5 (which is why it went undetected for so long)
                // always check your bits, folks!
                type |= (MagicNumber.fB << 16); // ;)
            }

            stream.Write(magic);
            stream.Write(type);
            
            stream.Write(totalCount);
            stream.Write(childCount);
        }
        
        public override void Serialize(Stream stream, NomadObject data)
        {
            Context.Begin();

            // make sure the cache is ready
            NomadCache.Clear();

            var bs = (stream as BinaryStream)
                ?? new BinaryStream(stream);

            Context.Log("Serializing data...");

            WriteHeader(bs, data);

            switch (Format)
            {
            case FormatType.Resource:
            case FormatType.Objects:
                WriteObject_FmtA(bs, data);
                break;
            case FormatType.Entities:
                DescriptorTag.GlobalFlags |= DescriptorFlags.Use24Bit;

                WriteObject_FmtB(bs, data);

                DescriptorTag.GlobalFlags &= ~DescriptorFlags.Use24Bit;
                break;
            }
            
            Context.End();
        }

        protected NomadValue ReadAttributeData(BinaryStream stream, NomadObject parent, int hash)
        {
            var ptr = (int)stream.Position;
            var id = StringId.Parse(hash);
            
            var type = (id == "RML_DATA")
                ? DataType.RML
                : AttributeTypes.GetBestType(id, parent.Id);
            
            var data = AttributeData.Read(stream, type);

            switch (type)
            {
            case DataType.BinHex:
                {
                    if (data.Type == DataType.String)
                    {
                        StringId fullType = $"{parent.Id}.{id}";

                        if (!AttributeTypes.IsTypeKnown(fullType))
                            AttributeTypes.RegisterGuessType(fullType, data.Type);
                    }
                }
                break;
            case DataType.String:
                {
                    if (data.Type == DataType.BinHex)
                    {
                        StringId fullType = $"{parent.Id}.{id}";

                        Context.LogDebug($"**** Attribute '{fullType}' was incorrectly assumed to be a String! ****");
                    }
                } break;
            }
            
            var result = new NomadValue() {
                Id = id,
                Data = data,
            };

            if (parent != null)
                parent.Attributes.Add(result);

            return result;
        }

        protected NomadValue ReadAttribute_FmtA(BinaryStream stream, NomadObject parent)
        {
            Context.State = ContextStateType.Member;
            Context.MemberIndex++;

            var hash = stream.ReadInt32();

            return ReadAttributeData(stream, parent, hash);
        }
        
        protected NomadObject ReadObject_FmtA(BinaryStream stream, NomadObject parent = null)
        {
            Context.State = ContextStateType.Object;
            Context.ObjectIndex++;

            var ptr = (int)stream.Position;
            var nChildren = DescriptorTag.Read(stream, ReferenceType.Index);

            if (nChildren.Type == DescriptorType.Reference)
                throw new InvalidOperationException("Cannot deserialize an object reference directly!");

            var hash = stream.ReadInt32();
            var id = StringId.Parse(hash);

            var result = new NomadObject(id);

            Context.AddRef(result, ptr);

            if (result.IsRml)
            {
                var next = DescriptorTag.Read(stream, ReferenceType.Index);

                var rmlBase = (int)stream.Position;

                var rmlSize = stream.ReadInt32();
                var rmlBuffer = stream.ReadBytes(rmlSize);

                using (var bs = new BinaryStream(rmlBuffer))
                {
                    var rmlData = new NomadRmlSerializer();
                    var rml = rmlData.Deserialize(bs);

                    result.Children.Add(rml);
                }

                stream.Position = (rmlBase + next);
            }
            else
            {
                var nAttrs = DescriptorTag.Read(stream, ReferenceType.Index);

                for (int i = 0; i < nAttrs; i++)
                    ReadAttribute_FmtA(stream, result);

                for (int i = 0; i < nChildren; i++)
                {
                    var cP = (int)stream.Position;
                    var cD = DescriptorTag.Read(stream, ReferenceType.Index);

                    // rip
                    if (cD.IsIndex)
                    {
                        var idx = cD.Value;
                        var childRef = Context.GetRefByIdx(idx) as NomadObject;

                        result.Children.Add(childRef);
                    }
                    else
                    {
                        // move back
                        stream.Position = cP;

                        ReadObject_FmtA(stream, result);
                    }
                }
            }
            
            if (parent != null)
                parent.Children.Add(result);

            return result;
        }

        protected List<NomadValue> ReadAttributes_FmtB(BinaryStream stream, NomadObject parent, IEnumerable<int> hashes)
        {
            Context.State = ContextStateType.Member;

            var result = new List<NomadValue>();

            foreach (var hash in hashes)
            {
                Context.MemberIndex++;

                var attrPtr = (int)stream.Position;
                var attr = ReadAttributeData(stream, parent, hash);

                result.Add(attr);
            }

            return result;
        }

        protected NomadObject ReadObject_FmtB(BinaryStream stream, NomadObject parent = null)
        {
            Context.State = ContextStateType.Object;
            Context.ObjectIndex++;

            var ptr = (int)stream.Position;
            
            var nD = DescriptorTag.Read(stream, ReferenceType.Offset);

            NomadObject result = null;

            if (nD.IsOffset)
            {
                result = Context.GetRefByPtr(nD.Value) as NomadObject;

                // this should never happen
                if (result == null)
                    throw new InvalidDataException("Malformed data!");
            }
            else
            {
                var nChildren = nD.Value;
                
                var hash = stream.ReadInt32();
                var size = stream.ReadInt16();

                if (size == 0)
                    throw new NotImplementedException("Zero-length nodes are not covered under TrumpCare(tm).");

                var id = StringId.Parse(hash);

                result = new NomadObject(id);
                
                Context.AddRef(result, ptr);

                var attrsPtr = (int)stream.Position;
                var next = (attrsPtr + size);
                
                var nhD = DescriptorTag.Read(stream, ReferenceType.Offset);

                var adjustPtr = false;

                if (nhD.IsOffset)
                {
                    stream.Position = nhD.Value;

                    // adjust ptr to attributes
                    attrsPtr += nhD.Size;
                    adjustPtr = true;

                    // read again
                    nhD = DescriptorTag.Read(stream, ReferenceType.Offset);

                    if (nhD.IsOffset)
                        throw new InvalidOperationException("Cannot have nested offsets!");   
                }

                var nAttrs = nhD.Value;
                var hashes = new int[nAttrs];

                // read attribute hash list
                for (int i = 0; i < nAttrs; i++)
                    hashes[i] = stream.ReadInt32();

                // move to the attributes if needed
                if (adjustPtr)
                    stream.Position = attrsPtr;

                // deserialize attributes
                if (nAttrs > 0)
                    ReadAttributes_FmtB(stream, result, hashes);

                if (stream.Position != next)
                {
                    Context.LogDebug($"Something went wrong when reading attributes for '{result.Id}':");
                    Context.LogDebug($" - Expected to read {size} bytes but only read {stream.Position - attrsPtr}");

                    foreach (var attr in result.Attributes)
                        Context.LogDebug($" - '{attr.Id}' : {attr.Data.Type} ({attr.Data.Size} bytes)");
                    
                    stream.Position = next;
                }

                // read children
                for (int n = 0; n < nChildren; n++)
                    ReadObject_FmtB(stream, result);
            }

            if (parent != null)
                parent.Children.Add(result);
            
            return result;
        }
        
        public override NomadObject Deserialize(Stream stream)
        {
            Context.Begin();
            
            var bs = (stream as BinaryStream)
                ?? new BinaryStream(stream);

            Context.Log("Deserializing data...");
            var magic = bs.ReadInt32();

            if (magic != Nomad.Magic)
                throw new InvalidOperationException("Invalid binary data -- bad magic!");

            Format = (FormatType)bs.ReadInt16();

            var flags = bs.ReadInt16() & 1;
            
            var nElems = bs.ReadInt32();
            var nAttrs = bs.ReadInt32();

            NomadObject result = null;
            
            switch (Format)
            {
            case FormatType.Resource:
            case FormatType.Objects:
                result = ReadObject_FmtA(bs);
                break;
            case FormatType.Entities:
                // ugly hacks :(
                DescriptorTag.GlobalFlags |= DescriptorFlags.Use24Bit;

                result = ReadObject_FmtB(bs);

                DescriptorTag.GlobalFlags &= ~DescriptorFlags.Use24Bit;
                break;
            }

            Context.End();

            return result;
        }

        public NomadResourceSerializer()
            : base()
        {
            Format = FormatType.Resource;
        }
    }
}