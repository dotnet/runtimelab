// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SwiftReflector.ExceptionTools;
using SwiftRuntimeLibrary;

namespace SwiftReflector.SwiftXmlReflection
{
    public class ParameterItem : IXElementConvertible
    {
        public static string kInOutMarker = "inout ";
        public ParameterItem()
        {
        }

        public ParameterItem(ParameterItem pi)
        {
            PublicName = pi.PublicName;
            PrivateName = pi.PrivateName;
            TypeName = pi.TypeName;
            IsInOut = pi.IsInOut;
            IsVariadic = pi.IsVariadic;
        }

        public string PublicName { get; set; }
        public string PrivateName { get; set; }
        public bool NameIsRequired { get { return !String.IsNullOrEmpty(PublicName); } }
        public bool IsVariadic { get; set; }

        string typeName;
        public string TypeName
        {
            get { return typeName; }
            set
            {
                typeName = Exceptions.ThrowOnNull(value, nameof(value));
                try
                {
                    typeSpec = TypeSpecParser.Parse(typeName);
                }
                catch (RuntimeException ex)
                {
                    throw ErrorHelper.CreateError(ReflectorError.kReflectionErrorBase + 2, $"Unable to parse type name '{typeName}': {ex.Message}");
                }
            }
        }
        TypeSpec typeSpec;
        public TypeSpec TypeSpec
        {
            get { return typeSpec; }
            set
            {
                Exceptions.ThrowOnNull(value, "value");
                typeSpec = value;
                typeName = value.ToString();
            }
        }
        public bool IsInOut { get; set; }

        #region IXElementConvertible implementation

        public XElement ToXElement()
        {
            return new XElement("parameter",
                                 new XAttribute("publicName", PublicName),
                                 new XAttribute("privateName", PrivateName),
                                 new XAttribute("type", TypeName),
                                 new XAttribute("isVariadic", IsVariadic)
                                );
        }

        #endregion

        public static List<List<ParameterItem>> ParameterListListFromXElement(TypeAliasFolder folder, XElement elem)
        {
            var plists = from plelem in elem.Elements("parameterlist")
                         orderby (int)plelem.Attribute("index")
                         select ParameterListFromXElement(folder, plelem);
            return plists.ToList();
        }

        public static List<ParameterItem> ParameterListFromXElement(TypeAliasFolder folder, XElement elem)
        {
            var indexed = from pelem in elem.Elements("parameter")
                          orderby (int)pelem.Attribute("index")
                          select ParameterItem.FromXElement(folder, pelem);

            return indexed.ToList();
        }

        public static ParameterItem FromXElement(TypeAliasFolder folder, XElement elem)
        {
            ParameterItem pi = new ParameterItem
            {
                PublicName = (string)elem.Attribute("publicName"),
                PrivateName = (string)elem.Attribute("privateName"),
                TypeName = (string)elem.Attribute("type"),
                IsVariadic = elem.BoolAttribute("isVariadic"),
            };
            pi.IsInOut = pi.TypeSpec.IsInOut;
            pi.TypeSpec = folder.FoldAlias(null, pi.TypeSpec);
            return pi;
        }

        public bool EqualsIgnoreName(ParameterItem other)
        {
            if (other == null)
                return false;
            return IsVariadic == other.IsVariadic && this.TypeSpec.Equals(other.TypeSpec);
        }

        public bool EqualsIgnoreNamesPartialMatch(ParameterItem other)
        {
            if (other == null)
                return false;
            return this.TypeSpec.EqualsPartialMatch(other.TypeSpec);
        }

        public static bool AreEqualIgnoreNames(IList<ParameterItem> pl1, IList<ParameterItem> pl2)
        {
            if (pl1.Count != pl2.Count)
                return false;
            for (int i = 0; i < pl1.Count; i++)
            {
                if (!pl1[i].EqualsIgnoreName(pl2[i]))
                    return false;
            }
            return true;
        }

        public static bool AreEqualIgnoreNamesReferencesInvariant(FunctionDeclaration fn1, IList<ParameterItem> pl1,
                                      FunctionDeclaration fn2, IList<ParameterItem> pl2, bool matchPartialNames)
        {
            if (pl1.Count != pl2.Count)
            {
                return false;
            }

            for (int i = 0; i < pl1.Count; i++)
            {
                var p1 = SubstituteSelfFromParent(fn1, pl1[i]);
                var p2 = SubstituteSelfFromParent(fn2, pl2[i]);
                p1 = RecastAsReference(p1);
                p2 = RecastAsReference(p2);


                // Names invariant means TYPE names not parameter names
                if (!ParameterNamesMatch(p1, p2))
                {
                    // we give a pass on matching "self".
                    // this is done because "self" is a keyword in swift
                    // and when matching a wrapper function, we can't call
                    // a parameter "self" but have to call it "thisN" where
                    // N is either an empty string or a number.
                    // This is because there might be a real parameter named "this"
                    // and we had to rename it.
                    // The end result is that we can't use a "this" test, but we
                    // can use a "self" test.
                    var parmName1 = p1.NameIsRequired ? p1.PublicName : p1.PrivateName;
                    var parmName2 = p2.NameIsRequired ? p2.PublicName : p2.PrivateName;
                    if (parmName1 != "self" && parmName2 != "self")
                        return false;
                }
                if (fn1.IsTypeSpecGeneric(p1))
                {
                    if (!fn2.IsTypeSpecGeneric(p2))
                        return false;
                    continue;
                }
                if (!p1.EqualsIgnoreName(p2))
                {
                    if (matchPartialNames)
                    {
                        if (!p1.EqualsIgnoreNamesPartialMatch(p2))
                            return false;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        static ParameterItem SubstituteSelfFromParent(FunctionDeclaration func, ParameterItem p)
        {
            if (func.Parent == null || !p.TypeSpec.HasDynamicSelf)
                return p;
            p = new ParameterItem(p);
            p.TypeSpec = p.TypeSpec.ReplaceName("Self", func.Parent.ToFullyQualifiedNameWithGenerics());
            return p;
        }

        static ParameterItem RecastAsReference(ParameterItem p)
        {
            if (p.IsInOut)
            {
                if (!p.TypeSpec.IsInOut)
                    p.TypeSpec.IsInOut = true;
                return p;
            }
            if (p.TypeSpec is NamedTypeSpec && p.TypeSpec.ContainsGenericParameters)
            {
                NamedTypeSpec named = (NamedTypeSpec)p.TypeSpec;
                // special case - turn UnsafePointer<T> into inout T for matching purposes
                if (named.Name == "Swift.UnsafePointer" || named.Name == "Swift.UnsafeMutablePointer")
                {
                    p = new ParameterItem(p);
                    p.TypeSpec = p.TypeSpec.GenericParameters[0];
                    p.IsInOut = true;
                    p.TypeSpec.IsInOut = true;
                }
            }
            return p;
        }

        static bool ParameterNamesMatch(ParameterItem p1, ParameterItem p2)
        {
            // parameters are considered matching if and only if their public names match.
            // The following are all DISTINCT
            // public func foo (a b: Int) { }
            // public func foo (c b: Int) { }
            // public func foo (b: Int) { } - the public name is b
            // public func foo (_ b: Int) { } - the public name is null or empty

            return p1.PublicName == p2.PublicName;
        }
    }
}

