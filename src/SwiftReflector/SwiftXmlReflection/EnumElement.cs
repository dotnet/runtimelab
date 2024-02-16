// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SwiftReflector.ExceptionTools;
using SwiftReflector.IOUtils;
using System.Xml.Linq;
using SwiftRuntimeLibrary;

namespace SwiftReflector.SwiftXmlReflection
{
    public class EnumElement : IXElementConvertible
    {
        public EnumElement(string name, string typeName, long? value)
        {
            Name = Exceptions.ThrowOnNull(name, nameof(name));
            TypeName = typeName;
            Value = value;
        }

        public string Name { get; set; }
        public bool HasType { get { return typeName != null && TypeSpec != null; } }
        string typeName;
        public string TypeName
        {
            get
            {
                return typeName;
            }
            set
            {
                typeName = value;
                if (value == null)
                    TypeSpec = null;
                else
                {
                    try
                    {
                        TypeSpec = TypeSpecParser.Parse(typeName);
                    }
                    catch (RuntimeException ex)
                    {
                        ErrorHelper.CreateError(ReflectorError.kReflectionErrorBase + 9, $"Unable to parse type name '{typeName}': {ex.Message}");
                    }
                }
            }
        }
        public TypeSpec TypeSpec { get; private set; }
        public long? Value { get; private set; }

        #region IXElementConvertible implementation
        public XElement ToXElement()
        {
            XElement elem = new XElement("element",
                                          new XAttribute("name", Name));
            if (HasType)
                elem.Add(new XAttribute("type", TypeName));
            if (Value.HasValue)
                elem.Add("intValue", Value.Value);
            return elem;
        }
        #endregion
    }
}

