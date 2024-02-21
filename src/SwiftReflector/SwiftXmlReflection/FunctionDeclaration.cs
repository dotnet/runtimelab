// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using SwiftReflector.ExceptionTools;
using SwiftRuntimeLibrary;

namespace SwiftReflector.SwiftXmlReflection
{
    public class FunctionDeclaration : Member
    {
        public const string kConstructorName = ".ctor";
        public const string kDestructorName = ".dtor";
        public const string kPropertyGetterPrefix = "get_";
        public const string kPropertySetterPrefix = "set_";
        public const string kPropertyMaterializerPrefix = "materializeforset_";
        public const string kPropertySubscriptGetterName = "get_subscript";
        public const string kPropertySubscriptSetterName = "set_subscript";
        public const string kPropertySubscriptMaterializerName = "materializeforset_subscript";

        static string[] subscriptNames = new string[] {
            kPropertySubscriptGetterName,
            kPropertySubscriptSetterName,
            kPropertySubscriptMaterializerName,
        };
        static bool IsSubscriptName(string s)
        {
            return Array.IndexOf(subscriptNames, s) >= 0;
        }

        public FunctionDeclaration()
            : base()
        {
            ParameterLists = new List<List<ParameterItem>>();
        }

        public FunctionDeclaration(FunctionDeclaration other)
            : base(other)
        {
            ParameterLists = CopyOf(other.ParameterLists);
            ReturnTypeName = other.ReturnTypeName;
            IsProperty = other.IsProperty;
            IsStatic = other.IsStatic;
            IsFinal = other.IsFinal;
            HasThrows = other.HasThrows;
            IsDeprecated = other.IsDeprecated;
            IsUnavailable = other.IsUnavailable;
            OperatorType = other.OperatorType;
            IsOptional = other.IsOptional;
            ObjCSelector = other.ObjCSelector;
            IsRequired = other.IsRequired;
            IsConvenienceInit = other.IsConvenienceInit;
            IsAsync = other.IsAsync;
            foreach (var genDecl in other.Generics)
            {
                Generics.Add(new GenericDeclaration(genDecl));
            }
        }

        string returnTypeName;
        public string ReturnTypeName
        {
            get { return returnTypeName; }
            set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
                returnTypeName = value;
                try
                {
                    ReturnTypeSpec = TypeSpecParser.Parse(returnTypeName);
                }
                catch (RuntimeException ex)
                {
                    throw ErrorHelper.CreateError(ReflectorError.kReflectionErrorBase + 1, $"Unable to parse type name '{returnTypeName}': {ex.Message}");
                }
            }
        }


        // When writing an override, we need to keep around the function in the override
        // this member should never get serialized. Ever.
        public FunctionDeclaration OverrideSurrogateFunction { get; set; }


        public TypeSpec ReturnTypeSpec { get; set; }

        public bool IsRequired { get; set; }
        public string ObjCSelector { get; set; }
        public bool IsOptional { get; set; }
        public bool HasThrows { get; set; }
        public bool IsAsync { get; set; }
        public bool IsProperty { get; set; }
        public string PropertyName { get { return Name.Substring(kPropertyGetterPrefix.Length); } }
        public bool IsStatic { get; set; }
        public bool IsFinal { get; set; }
        public bool IsOperator { get { return OperatorType != OperatorType.None; } }
        public bool IsDeprecated { get; set; }
        public bool IsUnavailable { get; set; }
        public bool IsConvenienceInit { get; set; }
        public bool IsVirtualClassMethod { get { return IsStatic && Access == Accessibility.Open; } }
        public string MangledName { get; set; }
        public bool IsSubscript
        {
            get
            {
                return IsProperty && IsSubscriptName(Name);
            }
        }
        public OperatorType OperatorType { get; set; }
        public bool IsSubscriptGetter
        {
            get
            {
                return IsProperty && Name == kPropertySubscriptGetterName;
            }
        }
        public bool IsSubscriptSetter
        {
            get
            {
                return IsProperty && Name == kPropertySubscriptSetterName;
            }
        }
        // in practice, this is useless since it is near impossible to disambiguate it
        // and it has no actual use from our point of view. Materializers are code used
        // internally by the compiler and we don't really have access to this.
        public bool IsSubscriptMaterializer
        {
            get
            {
                return IsProperty && Name == kPropertySubscriptMaterializerName;
            }
        }

        public TypeSpec PropertyType
        {
            get
            {
                if (!IsProperty)
                    throw ErrorHelper.CreateError(ReflectorError.kInventoryBase + 17, $"Attempt to get property type for a function named {this.Name}");
                // newValue should be the first argument in the last argument list.
                return ParameterLists.Last()[0].TypeSpec;
            }
        }

        public List<List<ParameterItem>> ParameterLists { get; set; }

        public bool IsTypeSpecGeneric(ParameterItem item)
        {
            return IsTypeSpecGeneric(item.TypeSpec);
        }

        public int GenericParameterCount
        {
            get
            {
                return ParameterLists.Last().Sum(pi => IsTypeSpecGeneric(pi) ? 1 : 0);
            }
        }

        public bool MatchesSignature(FunctionDeclaration other, bool ignoreFirstParameterListIfPresent)
        {
            if (!TypeSpec.BothNullOrEqual(this.ReturnTypeSpec, other.ReturnTypeSpec))
                return false;
            if (this.ParameterLists.Count != other.ParameterLists.Count)
                return false;
            int startIndex = ignoreFirstParameterListIfPresent && this.ParameterLists.Count > 1 ? 1 : 0;

            for (int i = startIndex; i < this.ParameterLists.Count; i++)
            {
                if (!ParameterItem.AreEqualIgnoreNamesReferencesInvariant(this, this.ParameterLists[i], other, other.ParameterLists[i], true))
                    return false;
            }
            return true;
        }

        public bool IsConstructor { get { return Name == kConstructorName; } }
        public bool IsDestructor { get { return Name == kDestructorName; } }
        public bool IsConstructorOrDestructor { get { return IsConstructor || IsDestructor; } }
        public bool IsGetter { get { return IsProperty && Name.StartsWith(kPropertyGetterPrefix); } }
        public bool IsSetter { get { return IsProperty && Name.StartsWith(kPropertySetterPrefix); } }
        public bool IsMaterializer { get { return IsProperty && Name.StartsWith(kPropertyMaterializerPrefix); } }

        public bool IsOptionalConstructor
        {
            get
            {
                if (Name != kConstructorName)
                    return false;
                var namedSpec = ReturnTypeSpec as NamedTypeSpec;
                if (namedSpec == null)
                    return false;
                if (namedSpec.Name != "Swift.Optional")
                    return false;
                if (namedSpec.GenericParameters.Count != 1)
                    return false;
                // previously we did a name check on the parent but in the case of
                // a virtual class, the name could be the proxy class that we make
                // and won't necessarily match.
                return true;
            }
        }

        public bool IsVariadic
        {
            get
            {
                return ParameterLists.Last().Any(pi => pi.IsVariadic);
            }
        }

        public FunctionDeclaration MatchingSetter(IEnumerable<FunctionDeclaration> decls)
        {
            if (!IsProperty)
                return null;
            if (IsSubscript)
            {
                return decls.Where(f => f.IsSubscriptSetter && SubscriptParametersMatch(this, f)).FirstOrDefault();
            }
            else
            {
                return decls.Where(f => f.IsSetter && f.PropertyName == PropertyName).FirstOrDefault();
            }
        }

        static bool SubscriptParametersMatch(FunctionDeclaration getter, FunctionDeclaration setter)
        {
            if (getter.ParameterLists.Count != 2 || setter.ParameterLists.Count != 2)
                return false;
            TypeSpec returnType = getter.ReturnTypeSpec;
            if (getter.ParameterLists[1].Count != setter.ParameterLists[1].Count - 1)
                return false;
            if (!returnType.Equals(setter.ParameterLists[1][0].TypeSpec))
                return false;

            return ParameterItem.AreEqualIgnoreNamesReferencesInvariant(getter, getter.ParameterLists[1],
                setter, setter.ParameterLists[1].Skip(1).ToList(), true);
        }


        public bool ContainsBoundGenericClosure()
        {
            foreach (var arg in ParameterLists.Last())
            {
                if (arg.TypeSpec.ContainsBoundGenericClosure())
                    return true;
            }
            return ReturnTypeSpec.ContainsBoundGenericClosure();
        }


        public static FunctionDeclaration FuncFromXElement(TypeAliasFolder folder, XElement elem, ModuleDeclaration module, BaseDeclaration parent)
        {
            ArgumentNullException.ThrowIfNull((string)elem.Attribute("returnType"), "returnType");
            FunctionDeclaration decl = new FunctionDeclaration
            {
                Name = (string)elem.Attribute("name"),
                Module = module,
                Parent = parent,
                Access = TypeDeclaration.AccessibilityFromString((string)elem.Attribute("accessibility")),
                ReturnTypeName = (string)elem.Attribute("returnType"),
                IsAsync = elem.BoolAttribute("isAsync"),
                IsProperty = elem.BoolAttribute("isProperty"),
                IsStatic = elem.BoolAttribute("isStatic"),
                IsFinal = elem.BoolAttribute("isFinal"),
                OperatorType = OperatorTypeFromElement((string)elem.Attribute("operatorKind")),
                HasThrows = elem.BoolAttribute("hasThrows"),
                IsDeprecated = elem.BoolAttribute("isDeprecated"),
                IsUnavailable = elem.BoolAttribute("isUnavailable"),
                IsOptional = elem.BoolAttribute("isOptional"),
                ObjCSelector = (string)elem.Attribute("objcSelector"),
                IsRequired = elem.BoolAttribute("isRequired"),
                IsConvenienceInit = elem.BoolAttribute("isConvenienceInit")
            };
            decl.ReturnTypeSpec = folder.FoldAlias(parent, decl.ReturnTypeSpec);
            decl.ParameterLists.AddRange(ParameterItem.ParameterListListFromXElement(folder, elem.Element("parameterlists")));
            if (decl.IsProperty && (decl.IsSetter || decl.IsSubscriptSetter))
            {
                decl.ParameterLists[decl.ParameterLists.Count - 1] =
                        MassageLastPropertySetterParameterList(decl.ParameterLists.Last());
            }
            return decl;
        }

        static List<ParameterItem> MassageLastPropertySetterParameterList(List<ParameterItem> list)
        {
            if (list.Count == 0)
                return list; // should never happen, but...

            if (list.Any(pi => pi.PublicName == "newValue"))
                return list; // also should never happen, but...

            var firstParam = list[0];
            var firstParamName = firstParam.NameIsRequired ? firstParam.PublicName : firstParam.PrivateName;
            // why the check on both value and newValue? Because we want both the public and private names to be newValue
            if (firstParamName == "value" || firstParamName == "newValue") // because swift reflects this incorrectly
            {
                firstParam.PublicName = firstParam.PrivateName = "newValue";
            }

            return list;
        }


        protected override XElement MakeXElement()
        {
            XElement theFunc = new XElement("func",
                                             new XAttribute("name", Name),
                                             new XAttribute("accessibility", TypeDeclaration.ToString(Access)),
                                             new XAttribute("returnType", ReturnTypeName),
                             new XAttribute("isAsync", BoolString(IsAsync)),
                                             new XAttribute("isProperty", BoolString(IsProperty)),
                                             new XAttribute("isStatic", BoolString(IsStatic)),
                                             new XAttribute("isFinal", BoolString(IsFinal)),
                                             new XAttribute("isDeprecated", BoolString(IsDeprecated)),
                                             new XAttribute("isUnavailable", BoolString(IsUnavailable)),
                                             new XAttribute("isOptional", BoolString(IsOptional)),
                                             new XAttribute("operatorKind", OperatorType.ToString()),
                                             new XAttribute("hasThrows", BoolString(HasThrows)),
                                             new XAttribute("isRequired", BoolString(IsRequired)),
                                  new XAttribute("isConvenienceInit", BoolString(IsConvenienceInit)),
                             new XElement("parameterlists", MakeParamListXElement()));
            if (!String.IsNullOrEmpty(ObjCSelector))
                theFunc.Add(new XAttribute("objcSelector", ObjCSelector));
            return theFunc;
        }

        XElement[] MakeParamListXElement()
        {
            List<XElement> plists = new List<XElement>();
            int index = 0;
            foreach (List<ParameterItem> list in ParameterLists)
            {
                XElement thisList = new XElement("parameterlist",
                    new XAttribute("index", index),
                    list.Select((pi, i) =>
                    {
                        XElement elem = pi.ToXElement();
                        elem.Add(new XAttribute("index", i));
                        return elem;
                    }).ToArray());
                plists.Add(thisList);
                index++;
            }
            return plists.ToArray();
        }

        static List<List<ParameterItem>> CopyOf(List<List<ParameterItem>> src)
        {
            List<List<ParameterItem>> dst = new List<List<ParameterItem>>();
            dst.AddRange(src.Select(l => CopyOf(l)));
            return dst;
        }

        static List<ParameterItem> CopyOf(List<ParameterItem> src)
        {
            List<ParameterItem> dst = new List<ParameterItem>();
            dst.AddRange(src.Select(pi => new ParameterItem(pi)));
            return dst;
        }

        public static OperatorType OperatorTypeFromElement(string type)
        {
            var enumType = OperatorType.None;
            if (Enum.TryParse(type, out enumType))
                return enumType;
            return OperatorType.None;
        }

        static string BoolString(bool b)
        {
            return b ? "true" : "false";
        }

        string PropertyKindString
        {
            get
            {
                if (IsSubscriptGetter || IsGetter)
                {
                    return "get";
                }
                else if (IsSubscriptSetter || IsSetter)
                {
                    return "set";
                }
                else if (IsSubscriptMaterializer || IsMaterializer)
                {
                    return "materialize";
                }
                return "";
            }
        }

        public override bool HasDynamicSelf
        {
            get
            {
                var types = ParameterLists.Last().Select(p => p.TypeSpec).ToList();
                if (!TypeSpec.IsNullOrEmptyTuple(ReturnTypeSpec))
                    types.Add(ReturnTypeSpec);
                return TypeSpec.AnyHasDynamicSelf(types);
            }
        }

        public override bool HasDynamicSelfInReturnOnly
        {
            get
            {
                if (IsProperty && !IsSubscript)
                    return false;
                if (TypeSpec.IsNullOrEmptyTuple(ReturnTypeSpec) || !ReturnTypeSpec.HasDynamicSelf)
                    return false;
                var types = ParameterLists.Last().Select(p => p.TypeSpec).ToList();
                return !TypeSpec.AnyHasDynamicSelf(types);
            }
        }

        public override bool HasDynamicSelfInArguments
        {
            get
            {
                return TypeSpec.AnyHasDynamicSelf(ParameterLists.Last().Select(p => p.TypeSpec).ToList());
            }
        }

        public FunctionDeclaration MacroReplaceType(string toFind, string replaceWith, bool skipThisArgument)
        {
            var newFunc = new FunctionDeclaration(this);
            if (!TypeSpec.IsNullOrEmptyTuple(newFunc.ReturnTypeSpec))
            {
                newFunc.ReturnTypeName = newFunc.ReturnTypeSpec.ReplaceName(toFind, replaceWith).ToString();
            }
            for (int i = 0; i < newFunc.ParameterLists.Last().Count; i++)
            {
                var arg = newFunc.ParameterLists.Last()[i];
                if (skipThisArgument && arg.PublicName == "this")
                    continue;
                arg.TypeSpec = arg.TypeSpec.ReplaceName(toFind, replaceWith);
            }
            return newFunc;
        }

        internal string ParametersToString()
        {
            var builder = new StringBuilder();
            var first = true;
            foreach (var parm in ParameterLists.Last())
            {
                if (!first)
                {
                    builder.Append(", ");
                }
                else
                {
                    first = false;
                }
                // forms
                // public_name private_name: [inout] Type
                // public_name: [inout] Type
                // _ private_name: [inout] Type
                if (parm.PublicName == parm.PrivateName)
                {
                    builder.Append(parm.PublicName);
                }
                else if (parm.NameIsRequired)
                {
                    builder.Append(parm.PublicName).Append(" ").Append(parm.PrivateName);
                }
                else
                {
                    builder.Append("_ ").Append(parm.PrivateName);
                }
                builder.Append(": ");
                if (parm.IsInOut || parm.TypeSpec.IsInOut)
                    builder.Append("inout ");
                builder.Append(parm.TypeSpec);
            }
            return builder.ToString();
        }

        public override string ToString()
        {
            // Forms:
            // access [modfiers] var Name: Type { [get | set] } [throws]
            // access [modifiers] subscript Name [ args ]: Type { get [set] } [throws]
            // access [modifiers] Name<Generics>(args) -> Type [throws]

            var builder = new StringBuilder();
            builder.Append(Access).Append(" ");
            if (IsFinal)
                builder.Append("final ");
            if (IsStatic)
                builder.Append("static ");


            if (IsProperty)
            {
                if (IsSubscript)
                {
                    builder.Append(Parent.ToString()).Append(".subscript");
                    builder.Append(" [").Append(ParametersToString()).Append("] -> ");
                }
                else
                {
                    builder.Append("var ").Append(Parent.ToString()).Append(".").Append(PropertyName);
                    builder.Append(": ");
                }
                builder.Append(ReturnTypeName).Append(" { ").Append(PropertyKindString).Append(" }");
                if (HasThrows)
                {
                    builder.Append(" throws");
                }
            }
            else
            {
                builder.Append(base.ToString());
                builder.Append(" (").Append(ParametersToString()).Append(")");
                if (HasThrows)
                {
                    builder.Append(" throws");
                }

                builder.Append(" -> ");
                if (TypeSpec.IsNullOrEmptyTuple(ReturnTypeSpec))
                {
                    builder.Append("()");
                }
                else
                {
                    builder.Append(ReturnTypeSpec);
                }
            }
            return builder.ToString();
        }
    }
}

