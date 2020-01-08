using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Nomad
{
    public class NomadXmlSerializer : NomadSerializer
    {
        public override FileType Type => FileType.Xml;
        
        public XAttribute CreateXmlAttribute(NomadValue val, XElement parent = null)
        {
            Context.State = ContextStateType.Member;
            Context.MemberIndex++;

            var id = val.Id;
            var data = val.Data;
            
            try
            {
                var name = XName.Get(id);
                var value = data.ToString();
                
                var attr = new XAttribute(name, value);

                if (parent != null)
                    parent.Add(attr);

                return attr;
            }
            catch (Exception e)
            {
                throw new XmlException($"Error parsing attribute '{id}' in '{parent.Name}' of type '{data.Type}' : {e.Message}", e);
            }
        }

        public XElement CreateXmlElement(NomadObject obj, XElement parent = null)
        {
            Context.State = ContextStateType.Object;
            Context.ObjectIndex++;

            var name = XName.Get(obj.Id);
            var elem = new XElement(name);

            if (parent != null)
                parent.Add(elem);

            foreach (var attr in obj.Attributes)
            {
                if (attr.Id == "RML_DATA")
                {
                    using (var bs = new BinaryStream(attr.Data.Buffer))
                    {
                        var rmlSize = bs.ReadInt32();
                        var rmlBuffer = bs.ReadBytes(rmlSize);

                        using (var rs = new BinaryStream(rmlBuffer))
                        {
                            var rmlData = new NomadRmlSerializer();
                            var rml = rmlData.Deserialize(rs);

                            var rmlRoot = new XElement("RML_DATA");
                            var rmlElem = CreateXmlElement(rml, rmlRoot);

                            elem.Add(rmlRoot);
                        }
                    }
                }
                else
                {
                    CreateXmlAttribute(attr, elem);
                }
            }

            foreach (var child in obj.Children)
                CreateXmlElement(child, elem);

            return elem;
        }
        
        public override void Serialize(Stream stream, NomadObject data)
        {
            Context.Begin();
            
            var elem = CreateXmlElement(data);
            var xDoc = new XDocument(elem);

            using (var writer = XmlWriter.Create(stream, Utils.XMLWriterSettings))
            {
                try
                {
                    xDoc.WriteTo(writer);
                }
                catch (Exception e)
                {
                    Context.Log($"**** XML save error : '{e.Message}' ****");
                    Context.Log(e.StackTrace);
                }
                finally
                {
                    writer.Flush();
                }
            }

            Context.End();
        }

        public NomadValue ReadXmlAttribute(XAttribute xml, NomadObject parent)
        {
            Context.State = ContextStateType.Member;
            Context.MemberIndex++;

            var name = xml.Name.LocalName;
            var cName = xml.Parent.Name.LocalName;
            
            var id = StringId.Parse(name);
            
            var type = (parent.IsRml)
                ? DataType.RML
                : AttributeTypes.GetBestType(id, cName);

            var value = xml.Value;

            if (type == DataType.BinHex)
            {
                // handle potential strings safely
                if ((value.Length > 0) && !Utils.IsHexString(value))
                    type = DataType.String;
            }

            NomadValue result = null;

            try
            {
                result = new NomadValue(type, value) {
                    Id = id
                };

                parent.Attributes.Add(result);

                return result;
            }
            catch (Exception e)
            {
                throw new XmlException($"Error parsing attribute '{id}' in '{parent.Id}' of type '{type}' : {e.Message}", e);
            }
        }

        public NomadObject ReadRmlObject(XElement rml, NomadObject parent)
        {
            var result = new NomadObject(true) {
                Id = rml.Name.LocalName,
            };

            foreach (var rmlAttr in rml.Attributes())
            {
                var value = rmlAttr.Value;
                
                var attr = new NomadValue(DataType.RML, rmlAttr.Value) {
                    Id = rmlAttr.Name.LocalName,
                };

                result.Attributes.Add(attr);
            }

            foreach (var rmlChild in rml.Elements())
                ReadRmlObject(rmlChild, result);

            if (parent != null)
                parent.Children.Add(result);

            return result;
        }

        public NomadObject ReadXmlObject(XElement xml, NomadObject parent = null)
        {
            Context.State = ContextStateType.Object;
            Context.ObjectIndex++;

            var name = xml.Name.LocalName;

            var id = StringId.Parse(name);
            var isRml = false;

            if (parent != null)
                isRml = parent.IsRml;
            if (!isRml && (id == "RML_DATA"))
                isRml = true;
            
            if (isRml)
            {
                XElement rmlElem = null;

                foreach (var elem in xml.Elements())
                {
                    if (rmlElem != null)
                        throw new XmlException("Too many elements in RML_DATA node!");

                    rmlElem = elem;
                }

                if (rmlElem == null)
                    throw new XmlException("Empty RML_DATA nodes are cancerous to your health!");

                var rmlRoot = new NomadObject(true) {
                    Id = "RML_DATA"
                };

                ReadRmlObject(rmlElem, rmlRoot);

                byte[] rmlBuffer = null;

                using (var bs = new BinaryStream(1024))
                {
                    // don't write size yet
                    bs.Position += 4;

                    var rmlData = new NomadRmlSerializer();
                    rmlData.Serialize(bs, rmlRoot);

                    // write size
                    var rmlSize = (int)(bs.Position - 4);
                    bs.Position = 0;

                    bs.Write(rmlSize);

                    rmlBuffer = bs.ToArray();
                }

                if (parent != null)
                {
                    var rml = new NomadValue() {
                        Id = id,
                        Data = new AttributeData(DataType.RML, rmlBuffer),
                    };

                    parent.Attributes.Add(rml);
                }
                
                return rmlRoot;
            }

            var result = new NomadObject(isRml) {
                Id = id
            };
            
            foreach (var attr in xml.Attributes())
                ReadXmlAttribute(attr, result);
            
            foreach (var node in xml.Elements())
                ReadXmlObject(node, result);

            if (parent != null)
                parent.Children.Add(result);

            return result;
        }
        
        public override NomadObject Deserialize(Stream stream)
        {
            Context.Begin();

            try
            {
                using (var reader = XmlReader.Create(stream, Utils.XMLReaderSettings))
                {
                    var xDoc = XDocument.Load(reader, LoadOptions.SetLineInfo);
                    var result = ReadXmlObject(xDoc.Root);
                    
                    return result;
                }
            }
            finally
            {
                Context.End();
            }
        }

        public NomadXmlSerializer()
            : base()
        {
            Format = FormatType.Generic;
        }
    }
}