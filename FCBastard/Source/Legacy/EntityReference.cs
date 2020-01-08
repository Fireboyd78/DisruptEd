using System;
using System.Xml;

namespace Nomad
{
    [Obsolete("This class is absolute garbage and needs to be put down.")]
    public class EntityReference
    {
        public long UID { get; set; }

        public int UID32
        {
            get { return (int)(UID & 0xFFFFFFFF); }
        }

        public bool Use32Bit { get; set; }

        // TODO: fix this crap
        public NodeClass GroupNode { get; set; }
        public NodeClass EntityNode { get; set; }
        
        public void Serialize(XmlElement xml)
        {
            var xmlDoc = xml.OwnerDocument;
            var elem = xmlDoc.CreateElement("EntityReference");
            
            var uidHex = (Use32Bit) ? BitConverter.GetBytes(UID32) : BitConverter.GetBytes(UID);

            elem.SetAttribute("UID", Utils.Bytes2HexString(uidHex));

            EntityNode.Serialize(elem);
            xml.AppendChild(elem);
        }

        public void Deserialize(XmlElement xml)
        {
            var attrs = xml.Attributes;
            var children = xml.ChildNodes;

            if (children.Count > 1)
                throw new InvalidOperationException("Malformed EntityReference node -- too many children!");

            var entNode = xml.FirstChild as XmlElement;

            if (entNode == null)
                throw new InvalidOperationException("Malformed EntityReference node -- could not get the Entity node!");
            
            if (attrs.Count != 0)
            {
                var uidAttr = attrs.GetNamedItem("UID");

                if (uidAttr != null)
                {
                    var uidHex = AttributeData.Parse(uidAttr.Value);
                    
                    if (Use32Bit)
                    {
                        UID = BitConverter.ToInt32(uidHex, 0);
                    }
                    else
                    {
                        UID = BitConverter.ToInt64(uidHex, 0);
                    }
                }
            }

            if (UID == 0)
                throw new InvalidOperationException("Attempted to deserialize a reference with no UID attribute!");
            
            // yuck!!!
            GroupNode = new NodeClass(0x256A1FF9);
            EntityNode = new NodeClass(entNode);

            GroupNode.Children.Add(EntityNode);
        }
    }
}
