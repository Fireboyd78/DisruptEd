using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Nomad
{
    [Obsolete("This class is absolute garbage and needs to be put down.")]
    public sealed class EntityLibrary : ISerializer<NodeClass>
    {
        public string Name { get; set; }
        
        public List<EntityReference> Entries { get; set; }

        public bool Use32Bit { get; set; }

        public NodeClass GetNodeClass()
        {
            var node = new NodeClass("EntityLibrary");

            if (!String.IsNullOrEmpty(Name))
            {
                node.Attributes.Add(new NodeAttribute("Name") {
                    Data = new AttributeData(DataType.String, Name)
                });
            }

            foreach (var entry in Entries)
            {
                if (entry.UID == 0)
                    throw new InvalidOperationException("Attempted to generate a library class with uninitialized entries.");

                node.Children.Add(entry.GroupNode);
            }

            return node;
        }

        public void Serialize(NodeClass parent)
        {
            var node = GetNodeClass();
            parent.Children.Add(node);
        }

        public void Deserialize(NodeClass node)
        {
            if (node.Attributes.Count > 0)
            {
                foreach (var attr in node.Attributes)
                {
                    if (attr.Hash == StringHasher.GetHash("Name"))
                    {
                        Name = attr.Data.ToString();
                        break;
                    }
                }
            }

            var nChildren = node.Children.Count;

            Entries = new List<EntityReference>(nChildren);

            // _256A1FF9 nodes
            foreach (var group in node.Children)
            {
                if (group.Children.Count != 1)
                    throw new InvalidOperationException("Houston, we got a bit of a problem...");

                var entry = new EntityReference() {
                    Use32Bit = Use32Bit,
                    GroupNode = group,
                    EntityNode = group.Children[0],
                };

                Entries.Add(entry);
            }
        }
        
        public void Serialize(XmlDocument xml)
        {
            var parent = xml.DocumentElement;
            var elem = xml.CreateElement("EntityLibrary");

            // don't write name unless it's in a huge EntityLibraries file
            // this may never be used again, actually...
            if (parent != null)
            {
                if (!String.IsNullOrEmpty(Name))
                    elem.SetAttribute("Name", Name);
            }

            foreach (var entry in Entries)
            {
                if (entry.UID == 0)
                    throw new InvalidOperationException("Attempted to serialize a library with uninitialized entries.");

                entry.Serialize(elem);
            }

            if (parent != null)
            {
                parent.AppendChild(elem);
            }
            else
            {
                // append to the xml file itself
                xml.AppendChild(elem);
            }
        }

        public void Deserialize(XmlDocument xml)
        {
            var elem = xml.DocumentElement;

            if ((elem == null) || (elem.Name != "EntityLibrary"))
                throw new InvalidOperationException("Not a EntityLibrary node!");
            
            Entries = new List<EntityReference>();

            foreach (var node in elem.ChildNodes.OfType<XmlElement>())
            {
                var entry = new EntityReference() {
                    Use32Bit = Use32Bit,
                };

                entry.Deserialize(node);   

                Entries.Add(entry);
            }
        }
    }
}
