// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SwiftReflector.ExceptionTools;
using System.Xml.Linq;

namespace SwiftReflector.SwiftXmlReflection
{
    public class EnumDeclaration : TypeDeclaration
    {
        public EnumDeclaration()
            : base()
        {
            Kind = TypeKind.Enum;
            Elements = new List<EnumElement>();
        }

        protected override TypeDeclaration UnrootedFactory()
        {
            return new EnumDeclaration();
        }

        protected override void CompleteUnrooting(TypeDeclaration unrooted)
        {
            base.CompleteUnrooting(unrooted);
            EnumDeclaration enumDecl = unrooted as EnumDeclaration;
            if (enumDecl == null)
                throw new ArgumentException("unrooted type was not constructed by EnumDeclaration");
            enumDecl.Elements.AddRange(Elements);
        }

        public List<EnumElement> Elements { get; private set; }
        public bool ElementIsGeneric(EnumElement element)
        {
            return IsTypeSpecGeneric(element.TypeSpec);
        }

        public bool HasRawType { get { return rawTypeName != null && RawTypeSpec != null; } }

        string rawTypeName;
        public string RawTypeName
        {
            get
            {
                return rawTypeName;
            }
            set
            {
                rawTypeName = value;
                if (value == null)
                    RawTypeSpec = null;
                else
                {
                    try
                    {
                        RawTypeSpec = TypeSpecParser.Parse(rawTypeName);
                    }
                    catch (RuntimeException ex)
                    {
                        throw ErrorHelper.CreateError(ReflectorError.kReflectionErrorBase + 8, $"Unable to parse type name '{rawTypeName}': {ex.Message}");
                    }
                }
            }
        }
        public TypeSpec RawTypeSpec { get; private set; }

        public bool IsTrivial
        {
            get
            {
                if (HasRawType)
                    return false;
                if (Inheritance.Count > 0)
                    return false;
                foreach (EnumElement elem in Elements)
                {
                    if (elem.HasType)
                        return false;
                }
                return true;
            }
        }

        public bool IsIntegral
        {
            get
            {
                if (HasRawType)
                {
                    return TypeSpec.IsIntegral(RawTypeSpec);
                }
                foreach (EnumElement elem in Elements)
                {
                    if (elem.HasType && !TypeSpec.IsIntegral(elem.TypeSpec))
                        return false;
                }
                return true;
            }
        }

        public bool IsHomogenous
        {
            get
            {
                if (Elements.Count == 0)
                    return true;
                TypeSpec firstSpec = Elements[0].TypeSpec;
                for (int i = 1; i < Elements.Count; i++)
                {
                    TypeSpec nextSpec = Elements[i].TypeSpec;
                    if (firstSpec == null)
                    {
                        if (firstSpec != nextSpec)
                            return false;
                    }
                    else
                    {
                        if (!firstSpec.Equals(nextSpec))
                            return false;
                    }
                }
                return true;
            }
        }

        public EnumElement this[string s]
        {
            get
            {
                return Elements.FirstOrDefault(elem => elem.Name == s);
            }
        }


        protected override void GatherXObjects(List<XObject> xobjects)
        {
            base.GatherXObjects(xobjects);
            if (HasRawType)
                xobjects.Add(new XAttribute("rawType", RawTypeName));
            IEnumerable<XElement> elems = Elements.Select(e => e.ToXElement());
            xobjects.Add(new XElement("elements", elems.ToArray()));
        }

    }
}

