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
    public class NomadObject : NomadData
    {
        public List<NomadValue> Attributes { get; }
        public List<NomadObject> Children { get; }

        public override bool IsObject => true;
        public override bool IsRml { get; }

        // extra data for RML
        public string Tag { get; set; }
        
        public override IEnumerator<NomadData> GetEnumerator()
        {
            foreach (var attr in Attributes)
                yield return attr;

            foreach (var child in Children)
            {
                yield return child;

                // down the rabbit hole we go!
                foreach (var grandchild in child)
                    yield return grandchild;
            }
        }

        public NomadObject GetChild(StringId id)
        {
            foreach (var child in Children)
            {
                if (child.Id == id)
                    return child;
            }

            return null;
        }

        public IEnumerable<NomadObject> GetChildren(StringId id)
        {
            foreach (var child in Children)
            {
                if (child.Id == id)
                    yield return child;
            }
        }

        public NomadValue GetAttribute(StringId id)
        {
            foreach (var attr in Attributes)
            {
                if (attr.Id == id)
                    return attr;
            }

            return null;
        }
        
        public string GetAttributeValue(StringId id)
        {
            foreach (var attr in Attributes)
            {
                if (attr.Id == id)
                    return attr.ToString();
            }

            return null;
        }
        
        public void SetAttributeValue(StringId id, DataType type, string value)
        {
            var attr = GetAttribute(id);

            if (attr != null)
            {
                attr.Data = new AttributeData(type, value);
            }
            else
            {
                attr = new NomadValue(id, type, value);

                Attributes.Add(attr);
            }
        }
        
        public NomadObject()
        {
            Attributes = new List<NomadValue>();
            Children = new List<NomadObject>();
        }

        public NomadObject(bool isRml)
            : this()
        {
            IsRml = isRml;
        }

        public NomadObject(StringId id)
            : this()
        {
            Id = id;
            IsRml = (id == "RML_DATA");
        }
    }
}