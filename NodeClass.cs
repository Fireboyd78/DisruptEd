using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DisruptEd.IO
{
    public class NodeClass : Node
    {
        public List<NodeAttribute> Attributes { get; set; }
        public List<NodeClass> Children { get; set; }

        public void Serialize(XmlElement xml)
        {
            var xmlDoc = xml.OwnerDocument;
            var elem = xmlDoc.CreateElement(Name);

            foreach (var attr in Attributes)
                attr.Serialize(elem);
            foreach (var node in Children)
                node.Serialize(elem);

            xml.AppendChild(elem);
        }

        public void Deserialize(XmlElement xml)
        {
            Name = xml.Name;

            foreach (XmlAttribute attr in xml.Attributes)
            {
                // temp fix: no debug stuff
                if (attr.Name.StartsWith("text_"))
                    continue;

                var attrNode = new NodeAttribute(attr);
                Attributes.Add(attrNode);
            }

            foreach (XmlElement elem in xml.ChildNodes)
            {
                var child = new NodeClass(elem);
                Children.Add(child);
            }
        }

        public override void Serialize(BinaryStream stream)
        {
            Offset = (int)stream.Position;

            var nChildren = Children.Count;
            var nAttributes = Attributes.Count;

            var nD = new NodeDescriptor(nChildren, false);
            nD.Serialize(stream);

            stream.Write(Hash);

            var attrsPtr = stream.Position;
            stream.Position += 2;

            var nhD = new NodeDescriptor(nAttributes, false);
            nhD.Serialize(stream);

            // step 1: write hashes
            foreach (var attribute in Attributes)
                attribute.Serialize(stream, true);
            // step 2: write data
            foreach (var attribute in Attributes)
                attribute.Serialize(stream);

            var attrsSize = (int)(stream.Position - (attrsPtr + 2));

            if (attrsSize > 65535)
                throw new InvalidOperationException("Attribute data too large.");

            var childrenPtr = stream.Position;

            stream.Position = attrsPtr;
            stream.Write((short)attrsSize);

            stream.Position = childrenPtr;

            // now write the children out
            foreach (var child in Children)
                child.Serialize(stream);
        }
        
        public override void Deserialize(BinaryStream stream)
        {
            var ptr = (int)stream.Position;
            
            var nD = new NodeDescriptor(stream);

            if (nD.IsOffset)
            {
                stream.Position = nD.Value;
                Deserialize(stream);

                stream.Position = (ptr + 4);
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
                    var nhD = new NodeDescriptor(stream);

                    var adjustPtr = false;

                    if (nhD.IsOffset)
                    {
                        stream.Position = nhD.Value;

                        // read again
                        nhD = new NodeDescriptor(stream);

                        if (nhD.IsOffset)
                            throw new InvalidOperationException("Cannot have nested offsets!");

                        // adjust ptr to attributes
                        attrsPtr += 4;
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
