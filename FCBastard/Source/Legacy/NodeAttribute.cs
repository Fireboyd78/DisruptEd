using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using System.Xml;

namespace Nomad
{
    public class NodeAttribute : Node
    {
        public AttributeData Data;
        
        public string Class { get; set; }

        public string FullName
        {
            get
            {
                if (String.IsNullOrEmpty(Class))
                    return Name;

                return $"{Class}.{Name}";
            }
        }
        
        public void Serialize(XmlElement xml)
        {
            xml.SetAttribute(Name, Data.ToString());
        }

        public void Deserialize(XmlAttribute xml)
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
            
            var type = AttributeTypes.GetType(Hash);

            // try resolving the full name, e.g. 'Class.bProperty'
            var fullHash = StringHasher.GetHash(FullName);

            if (fullHash != Hash)
            {
                if (AttributeTypes.IsTypeKnown(fullHash))
                    type = AttributeTypes.GetType(fullHash);
            }
            
            Data = new AttributeData(type, xml.Value);

            // looks to be part of the spec :/
            //if (Data.Type != type)
            //    Debug.WriteLine($"Attribute '{FullName}' was created as a '{type.ToString()}' but was actually a '{Data.Type.ToString()}'!");
        }

        public void Serialize(BinaryStream stream, bool writeHash)
        {
            if (writeHash)
            {
                stream.Write(Hash);
            }
            else
            {
                Serialize(stream);
            }
        }

        public override void Serialize(BinaryStream stream)
        {
            Offset = (int)stream.Position;
            Data.Serialize(stream);
        }

        public override void Deserialize(BinaryStream stream)
        {
            Offset = (int)stream.Position;

            try
            {    
                Data.Deserialize(stream);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Attribute '{Name}' read error -- {e.Message}", e);
            }

            // pretty sure this is garbage
            if (Data.Type == DataType.BinHex)
            {
                if (Data.IsBufferValid_LEGACY())
                {
                    var guess = Utils.GetAttributeTypeBestGuess(Data);

                    if (guess != DataType.BinHex)
                    {
                        //Debug.WriteLine($"[INFO] Attribute type for '{Name}' (hash={Hash:X8}) in '{Class}' may be '{guess.ToString()}'");
                        Debug.WriteLine($"<!-- GUESS: --><Attribute Name=\"{FullName}\" Type=\"{guess.ToString()}\" />");
                    }
                }
                else
                {
                    // really really slow
                    Debug.WriteLine($"[INFO] Attribute type for '{Name}' (hash={Hash:X8}) in '{Class}' is unknown.");
                }
            }
            else
            {
                if (Utils.IsAttributeBufferStrangeSize(Data))
                    Debug.WriteLine($"Attribute '{Name}' in '{Class}' is defined as a '{Data.Type}' and has a strange size of {Data.Buffer.Length} byte(s).");

                if (Data.Type == DataType.Byte)
                {
                    if ((Data.Buffer.Length == 1) && (Data.Buffer[0] == 0))
                        throw new InvalidOperationException($"VERY BAD: Attribute '{Name}' (hash:{Hash:X8}) defined as a Byte in class '{Class}' but is actually a String.\r\nThis will BREAK the exporter, so please update this attribute immediately!");
                }
            }
        }

        public void Deserialize(BinaryStream stream, bool readHash)
        {
            if (readHash)
            {
                var hash = stream.ReadInt32();
                
                var name = StringHasher.ResolveHash(hash);
                var type = AttributeTypes.GetType(hash);

                // cannot be null or contain spaces
                var nameResolved = ((name != null) && !name.Contains(" "));
                
                if (nameResolved)
                {
                    Name = name;
                }
                else
                {
                    Hash = hash;
                }

                // try resolving the full name, e.g. 'Class.bProperty'
                var fullHash = StringHasher.GetHash(FullName);

                if (fullHash != hash)
                {
                    if (AttributeTypes.IsTypeKnown(fullHash))
                        type = AttributeTypes.GetType(fullHash);
                }
                
                Data = new AttributeData(type);
            }
            else
            {
                Deserialize(stream);
            }
        }

        public NodeAttribute(BinaryStream stream, string className)
        {
            Class = className;
            Deserialize(stream, true);
        }

        public NodeAttribute(XmlAttribute elem)
        {
            Class = elem.OwnerElement.Name;
            Deserialize(elem);
        }

        public NodeAttribute(string name)
            : this(name, DataType.BinHex) { }

        public NodeAttribute(int hash)
            : this(hash, DataType.BinHex) { }

        public NodeAttribute(int hash, string name)
        : this(hash, name, DataType.BinHex) { }

        public NodeAttribute(string name, DataType type)
            : base(name)
        {
            Data = new AttributeData(type);
        }

        public NodeAttribute(int hash, DataType type)
            : base(hash)
        {
            Data = new AttributeData(type);
        }

        public NodeAttribute(int hash, string name, DataType type)
            : base(hash, name)
        {
            Data = new AttributeData(type);
        }
    }
}
