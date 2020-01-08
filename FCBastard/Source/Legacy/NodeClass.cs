using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Nomad
{
    public class NodeClass : Node, IGetChildren<NodeClass>, IGetAttributes<NodeAttribute>, ICacheableObject
    {
        public List<NodeAttribute> Attributes { get; set; }
        public List<NodeClass> Children { get; set; }

        public int Size
        {
            get
            {
                var size = 7; // header + attributes

                var nChildren = Children.Count;
                var nAttrs = Attributes.Count;

                if (nChildren > 253)
                    size += 3;

                if (nAttrs > 0)
                {
                    // hash list
                    size += ((nAttrs * 4) + 1);

                    // attribute data
                    foreach (var attr in Attributes)
                    {
                        var attrSize = attr.Data.Size;
                        size += (attrSize + 1);
                        
                        if (attrSize > 253)
                            size += 3;
                    }
                }
                
                // size is assumed to be _raw_ uncached data
                return size;
            }
        }

        public override int GetHashCode()
        {
            var hash = Hash;
            
            foreach (var attr in Attributes)
                hash ^= (attr.Hash ^ attr.Data.GetHashCode());
            foreach (var child in Children)
                hash ^= (child.Hash ^ child.GetHashCode());

            return (hash ^ ~Size) | Size;
        }

        public void Serialize(XmlNode xml)
        {
            var xmlDoc = (xml as XmlDocument) ?? xml.OwnerDocument;
            var elem = xmlDoc.CreateElement(Name);

            foreach (var attr in Attributes)
                attr.Serialize(elem);
            foreach (var node in Children)
                node.Serialize(elem);

            xml.AppendChild(elem);
        }

        public void Deserialize(XmlNode xml)
        {
            var name = xml.Name;

            if (name[0] == '_')
            {
                Hash = int.Parse(name.Substring(1), NumberStyles.HexNumber);
            }
            else
            {
                // known attribute :)
                Name = name;
            }

            foreach (XmlAttribute attr in xml.Attributes)
            {
                var attrNode = new NodeAttribute(attr);
                Attributes.Add(attrNode);
            }
            
            foreach (var node in xml.ChildNodes.OfType<XmlElement>())
            {
                var child = new NodeClass(node);
                Children.Add(child);
            }
        }
        
        private void WriteAttributeHashes(BinaryStream stream)
        {
            var ptr = (int)stream.Position;
            var nAttrs = Attributes.Count;

            if (nAttrs > 0)
            {
                var attrHBuf = new byte[(nAttrs * 4) + 1];

                using (var buf = new BinaryStream(attrHBuf))
                {
                    buf.WriteByte(nAttrs);

                    foreach (var attr in Attributes)
                        attr.Serialize(buf, true);
                }

                if (WriteCache.IsCached(attrHBuf, nAttrs))
                {
                    var cache = WriteCache.GetData(attrHBuf, nAttrs);

                    var nhD = DescriptorTag.CreateReference(cache.Offset, ReferenceType.Offset);
                    nhD.WriteTo(stream);
                }
                else
                {
                    WriteCache.Cache(ptr, attrHBuf, nAttrs);
                    stream.Write(attrHBuf);
                }
            }
            else
            {
                // nothing to write
                stream.WriteByte(0);
            }
        }

        public bool Equals(NodeClass obj)
        {
            var equal = true;

            equal = (obj.Hash == Hash)
                && (obj.Size == Size)
                && (obj.Children.Count == Children.Count)
                && (obj.Attributes.Count == Attributes.Count);

            if (equal)
            {
                for (int i = 0; i < Attributes.Count; i++)
                {
                    var myAttr = Attributes[i];
                    var datAttr = obj.Attributes[i];

                    equal = (myAttr.Hash == datAttr.Hash)
                        && (myAttr.Data.GetHashCode() == datAttr.Data.GetHashCode());

                    if (!equal)
                        break;
                }
            }

            if (equal)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    var myChild = Children[i];
                    var datChild = obj.Children[i];

                    equal = myChild.Equals(datChild);

                    if (!equal)
                        break;
                }
            }

            // will either be true or false
            return equal;
        }
        
        public override void Serialize(BinaryStream stream)
        {
            Offset = (int)stream.Position;
            
            var nChildren = Children.Count;
            var nAttributes = Attributes.Count;

            var writeData = true;

            if (Size > 16)
            {
                if (WriteCache.IsCached(this))
                {
                    var cache = WriteCache.GetData(this);
                    var obj = cache.Object as NodeClass;

                    if ((obj != null) && obj.Equals(this))
                    {
                        Debug.WriteLine($">> [Class:{Offset:X8}] Instance cached @ {cache.Offset:X8} with key {cache.Checksum:X8}");

                        var nD = DescriptorTag.CreateReference(cache.Offset, ReferenceType.Offset);
                        nD.WriteTo(stream);

                        writeData = false;
                    }
                    else
                    {
                        Debug.WriteLine($">> [Class:{Offset:X8}] !!! FALSE POSITIVE !!!");
                    }
                }
                else
                {
                    Debug.WriteLine($">> [Class:{Offset:X8}] Caching new instance with key {GetHashCode():X8}");
                    WriteCache.Cache(Offset, this);
                }
            }

            if (writeData)
            {
                var nD = DescriptorTag.Create(nChildren);
                nD.WriteTo(stream);

                stream.Write(Hash);

                // skip size parameter for now
                stream.Position += 2;

                var attrsPtr = stream.Position;

                if (nAttributes > 0)
                {
                    WriteAttributeHashes(stream);

                    // write attribute data
                    foreach (var attribute in Attributes)
                        attribute.Serialize(stream);
                }
                else
                {
                    // no attributes to write!
                    stream.WriteByte(0);
                }

                var childrenPtr = stream.Position;
                var attrsSize = (int)(childrenPtr - attrsPtr);

                if (attrsSize > 65535)
                    throw new InvalidOperationException("Attribute data too large.");
                
                // write attributes size
                stream.Position = (attrsPtr - 2);
                stream.Write((short)attrsSize);

                // now write the children out
                stream.Position = childrenPtr;
                
                foreach (var child in Children)
                    child.Serialize(stream);
            }
        }
        
        public override void Deserialize(BinaryStream stream)
        {
            var ptr = (int)stream.Position;
            
            var nD = DescriptorTag.Read(stream, ReferenceType.Offset);

            if (nD.IsOffset)
            {
                stream.Position = nD.Value;
                Deserialize(stream);

                stream.Position = (ptr + nD.Size);
            }
            else
            {
                Offset = ptr;

                var nChildren = nD.Value;

                Children = new List<NodeClass>(nChildren);

                var hash = stream.ReadInt32();
                var size = stream.ReadInt16();

                var name = StringHasher.ResolveHash(hash);

                if (name != null)
                {
                    Name = name;
                }
                else
                {
                    Hash = hash;
                }

                var attrsPtr = (int)stream.Position;
                var next = (attrsPtr + size);

                if (size != 0)
                {
                    var nhD = DescriptorTag.Read(stream, ReferenceType.Offset);

                    var adjustPtr = false;

                    if (nhD.IsOffset)
                    {
                        stream.Position = nhD.Value;

                        // read again
                        nhD = DescriptorTag.Read(stream, ReferenceType.Offset);

                        if (nhD.IsOffset)
                            throw new InvalidOperationException("Cannot have nested offsets!");

                        // adjust ptr to attributes
                        attrsPtr += nhD.Size;
                        adjustPtr = true;
                    }

                    var nAttrs = nhD.Value;

                    Attributes = new List<NodeAttribute>(nAttrs);

                    for (int i = 0; i < nAttrs; i++)
                    {
                        var attr = new NodeAttribute(stream, Name);
                        Attributes.Add(attr);
                    }

                    // move to the attributes if needed
                    if (adjustPtr)
                        stream.Position = attrsPtr;

                    // deserialize attribute data
                    foreach (var attr in Attributes)
                        attr.Deserialize(stream);
                }
                else
                {
                    throw new NotImplementedException("Zero-length nodes are not covered under TrumpCare™.");
                }

                if (stream.Position != next)
                    throw new InvalidOperationException("You dun fucked up, son!");

                // read children
                for (int n = 0; n < nChildren; n++)
                {
                    var child = new NodeClass(stream);
                    Children.Add(child);
                }
            }
        }

        public NodeClass(BinaryStream stream)
        {
            Deserialize(stream);
        }

        public NodeClass(XmlElement elem)
        {
            Children = new List<NodeClass>();
            Attributes = new List<NodeAttribute>();

            Deserialize(elem);
        }
        
        public NodeClass(int hash)
            : this(hash, -1, -1) { }
        public NodeClass(string name)
            : this(name, -1, -1) { }

        public NodeClass(int hash, int nChildren, int nAttributes)
            : base(hash)
        {
            Children = (nChildren == -1) ? new List<NodeClass>() : new List<NodeClass>(nChildren);
            Attributes = (nAttributes == -1) ? new List<NodeAttribute>() : new List<NodeAttribute>(nAttributes);
        }

        public NodeClass(string name, int nChildren, int nAttributes)
            : base(name)
        {
            Children = (nChildren == -1) ? new List<NodeClass>() : new List<NodeClass>(nChildren);
            Attributes = (nAttributes == -1) ? new List<NodeAttribute>() : new List<NodeAttribute>(nAttributes);
        }
    }
}
