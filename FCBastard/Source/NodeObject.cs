using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DisruptEd.IO
{
    public class NodeObject : Node, IGetChildren<NodeObject>, IGetAttributes<NodeAttribute>
    {
        public List<NodeAttribute> Attributes { get; set; }
        public List<NodeObject> Children { get; set; }
        
        public bool Equals(NodeClass obj)
        {
            var equal = true;

            equal = (obj.Hash == Hash)
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

            var nD = NodeDescriptor.Create(nChildren);
            nD.WriteTo(stream);

            stream.Write(Hash);

            if (nAttributes > 0)
            {
                // write attributes
                foreach (var attribute in Attributes)
                {
                    attribute.Serialize(stream, true); // hash
                    attribute.Serialize(stream, false); // data
                }
            }
            else
            {
                // no attributes to write!
                stream.WriteByte(0);
            }

            // now write the children out
            foreach (var child in Children)
                child.Serialize(stream);
        }

        public void Deserialize(BinaryStream stream, List<NodeObject> objRefs)
        {
            Offset = (int)stream.Position;

            // define reference type just in case we fuck up somehow
            var nD = NodeDescriptor.Read(stream, ReferenceType.Index);

            if (nD.Type == DescriptorType.Reference)
                throw new InvalidOperationException("Cannot deserialize an object reference directly!");

            var nChildren = nD.Value;

            Children = new List<NodeObject>(nChildren);

            var hash = stream.ReadInt32();
            var name = StringHasher.ResolveHash(hash);

            if (name != null)
            {
                Name = name;
            }
            else
            {
                Hash = hash;
            }

            // add a reference to this object
            objRefs.Add(this);

            var aD = NodeDescriptor.Read(stream, ReferenceType.Index);
            var nAttrs = aD.Value;

            Attributes = new List<NodeAttribute>(nAttrs);

            if (nAttrs > 0)
            {
                for (int i = 0; i < nAttrs; i++)
                {
                    // hash and data inline
                    var attr = new NodeAttribute(stream, Name);
                    attr.Deserialize(stream);

                    Attributes.Add(attr);
                }
            }

            if (nChildren > 0)
            {
                // read children
                for (int n = 0; n < nChildren; n++)
                {
                    var cP = (int)stream.Position;
                    var cD = NodeDescriptor.Read(stream, ReferenceType.Index);

                    // rip
                    if (cD.IsIndex)
                    {
                        var idx = cD.Value;
                        var childRef = objRefs[idx];
                        
                        Children.Add(childRef);
                    }
                    else
                    {
                        // move back
                        stream.Position = cP;

                        var child = new NodeObject(stream, objRefs);
                        Children.Add(child);
                    }
                }
            }
        }
        
        public override void Deserialize(BinaryStream stream)
        {
            throw new InvalidOperationException("Can't deserialize an object without access to a list of references!");
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
                var child = new NodeObject(node);
                Children.Add(child);
            }
        }
        
        public NodeObject(BinaryStream stream, List<NodeObject> objRefs)
        {
            Deserialize(stream, objRefs);
        }

        public NodeObject(XmlElement elem)
        {
            Children = new List<NodeObject>();
            Attributes = new List<NodeAttribute>();

            Deserialize(elem);
        }

        public NodeObject(int hash)
            : this(hash, -1, -1) { }
        public NodeObject(string name)
            : this(name, -1, -1) { }

        public NodeObject(int hash, int nChildren, int nAttributes)
            : base(hash)
        {
            Children = (nChildren == -1) ? new List<NodeObject>() : new List<NodeObject>(nChildren);
            Attributes = (nAttributes == -1) ? new List<NodeAttribute>() : new List<NodeAttribute>(nAttributes);
        }

        public NodeObject(string name, int nChildren, int nAttributes)
            : base(name)
        {
            Children = (nChildren == -1) ? new List<NodeObject>() : new List<NodeObject>(nChildren);
            Attributes = (nAttributes == -1) ? new List<NodeAttribute>() : new List<NodeAttribute>(nAttributes);
        }
    }
}
