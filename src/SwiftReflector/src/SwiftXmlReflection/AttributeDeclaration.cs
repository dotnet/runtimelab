// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using SyntaxDynamo;
using System.Linq;

namespace SwiftReflector.SwiftXmlReflection
{
    public class AttributeDeclaration
    {
        public AttributeDeclaration(string typeName)
        {
            ArgumentNullException.ThrowIfNull(typeName, nameof(typeName));
            Name = typeName;
            Parameters = new List<AttributeParameter>();
        }

        public AttributeDeclaration(AttributeDeclaration other)
            : this(other.Name)
        {
            foreach (var parameter in other.Parameters)
                Parameters.Add(DuplicateOf(parameter));
        }

        public static AttributeDeclaration FromXElement(XElement elem)
        {
            var decl = new AttributeDeclaration(elem.Attribute("name").Value);
            var parameters = elem.Element("attributeparameterlist");
            if (parameters == null)
                return decl;
            FromAttributeParameterList(parameters, decl.Parameters);
            return decl;
        }

        internal static void FromAttributeParameterList(XElement parameters, List<AttributeParameter> outlist)
        {
            foreach (var parameterElem in parameters.Elements("attributeparameter"))
            {
                var parameter = AttributeParameter.FromXElement(parameterElem);
                outlist.Add(parameter);
            }
        }

        public static AttributeParameter DuplicateOf(AttributeParameter other)
        {
            switch (other.Kind)
            {
                case AttributeParameterKind.Label:
                    return new AttributeParameterLabel((AttributeParameterLabel)other);
                case AttributeParameterKind.Literal:
                    return new AttributeParameterLiteral((AttributeParameterLiteral)other);
                case AttributeParameterKind.Sublist:
                    return new AttributeParameterSublist((AttributeParameterSublist)other);
                case AttributeParameterKind.Unknown:
                    return new AttributeParameter();
                default:
                    throw new ArgumentOutOfRangeException(nameof(other), other.Kind.ToString());
            }
        }

        public NamedTypeSpec AttributeType { get; private set; }
        public string Name
        {
            get
            {
                return AttributeType.ToString(true);
            }
            private set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
                var ts = TypeSpecParser.Parse(value);
                if (ts is NamedTypeSpec named)
                    AttributeType = named;
                else
                    throw new ArgumentOutOfRangeException($"TypeSpec for {value} is a {ts.Kind} and not a named type spec");
            }
        }
        public List<AttributeParameter> Parameters { get; private set; }
    }

    public class AttributeParameter
    {
        public static AttributeParameter FromXElement(XElement elem)
        {
            switch (elem.Attribute("kind").Value)
            {
                case "Label":
                    return new AttributeParameterLabel(elem.Attribute("Value").Value);
                case "Literal":
                    return new AttributeParameterLiteral(elem.Attribute("Value").Value);
                case "Sublist":
                    return AttributeParameterSublist.SublistFromXElement(elem);
                default:
                    return new AttributeParameter();
            }
        }

        public virtual AttributeParameterKind Kind => AttributeParameterKind.Unknown;
    }

    public class AttributeParameterLabel : AttributeParameter
    {
        public AttributeParameterLabel(string label)
        {

            ArgumentNullException.ThrowIfNull(label, nameof(label));
            Label = label;
        }

        public AttributeParameterLabel(AttributeParameterLabel other)
            : this(other.Label)
        {
        }

        public override AttributeParameterKind Kind => AttributeParameterKind.Label;
        public string Label { get; private set; }
    }

    public class AttributeParameterLiteral : AttributeParameter
    {
        public AttributeParameterLiteral(string literal)
        {
            ArgumentNullException.ThrowIfNull(literal, nameof(literal));
            Literal = literal;
        }

        public AttributeParameterLiteral(AttributeParameterLiteral other)
            : this(other.Literal)
        {
        }

        public override AttributeParameterKind Kind => AttributeParameterKind.Literal;
        public string Literal { get; private set; }
    }

    public class AttributeParameterSublist : AttributeParameter
    {
        public AttributeParameterSublist()
        {
            Parameters = new List<AttributeParameter>();
        }

        public AttributeParameterSublist(AttributeParameterSublist other)
            : this()
        {
            Parameters.AddRange(other.Parameters.Select(prm => AttributeDeclaration.DuplicateOf(prm)));
        }

        public static AttributeParameterSublist SublistFromXElement(XElement elem)
        {
            var sublist = new AttributeParameterSublist();
            var parameters = elem.Element("attributeparameterlist");
            if (parameters == null)
                return sublist;
            AttributeDeclaration.FromAttributeParameterList(parameters, sublist.Parameters);
            return sublist;
        }

        public override AttributeParameterKind Kind => AttributeParameterKind.Sublist;
        public List<AttributeParameter> Parameters { get; private set; }
    }
}
