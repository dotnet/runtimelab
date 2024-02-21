// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Xml.Linq;
using SwiftReflector.ExceptionTools;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SwiftRuntimeLibrary;

namespace SwiftReflector.SwiftXmlReflection
{
    public class PropertyDeclaration : Member
    {
        public PropertyDeclaration()
        {
        }

        public PropertyDeclaration(PropertyDeclaration other)
            : base(other)
        {
            TypeName = other.TypeName;
            Storage = other.Storage;
            IsStatic = other.IsStatic;
            IsLet = other.IsLet;
            IsDeprecated = other.IsDeprecated;
            IsUnavailable = other.IsUnavailable;
            IsOptional = other.IsOptional;
        }

        string typeName;
        public string TypeName
        {
            get
            {
                return typeName;
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
                typeName = value;
                try
                {
                    TypeSpec = TypeSpecParser.Parse(typeName);
                }
                catch (RuntimeException ex)
                {
                    throw ErrorHelper.CreateError(ReflectorError.kReflectionErrorBase + 3, $"Unable to parse type name '{typeName}': {ex.Message}");
                }
            }
        }
        public TypeSpec TypeSpec { get; private set; }
        public StorageKind Storage { get; set; }
        public bool IsStatic { get; set; }
        public bool IsLet { get; set; }
        public bool IsDeprecated { get; set; }
        public bool IsUnavailable { get; set; }
        public bool IsOptional { get; set; }
        public bool IsAsync
        {
            get
            {
                var getter = GetGetter();
                return getter == null ? false : getter.IsAsync;
            }
        }

        public FunctionDeclaration GetGetter()
        {
            return SearchForPropertyAccessor(FunctionDeclaration.kPropertyGetterPrefix);
        }

        public FunctionDeclaration GetSetter()
        {
            return SearchForPropertyAccessor(FunctionDeclaration.kPropertySetterPrefix);
        }


        FunctionDeclaration SearchForPropertyAccessor(string prefix)
        {
            var funcs = GetFunctionsToSearch();
            return funcs.FirstOrDefault(f => f.IsProperty &&
                f.IsStatic == IsStatic &&
                f.Name.StartsWith(prefix, StringComparison.Ordinal) &&
                (f.Name.Length == prefix.Length + Name.Length) &&
                string.CompareOrdinal(f.Name, prefix.Length, Name, 0, Name.Length) == 0);
        }

        IEnumerable<FunctionDeclaration> GetFunctionsToSearch()
        {
            if (Parent == null)
            {
                return Module.Functions;
            }
            else
            {
                TypeDeclaration parent = Parent as TypeDeclaration;
                if (parent == null)
                {
                    throw ErrorHelper.CreateError(ReflectorError.kReflectionErrorBase + 4, $"Expected property parent to be a TypeDeclaration, but was a {Parent.GetType().Name}");
                }
                return parent.Members.OfType<FunctionDeclaration>();
            }
        }

        public static PropertyDeclaration PropFromXElement(TypeAliasFolder folder, XElement elem, ModuleDeclaration module, BaseDeclaration parent)
        {
            var property = new PropertyDeclaration
            {
                Name = (string)elem.Attribute("name"),
                Module = module,
                Parent = parent,
                Access = TypeDeclaration.AccessibilityFromString((string)elem.Attribute("accessibility")),
                TypeName = (string)elem.Attribute("type"),
                Storage = StorageKindFromString((string)elem.Attribute("storage")),
                IsStatic = elem.BoolAttribute("isStatic"),
                IsLet = elem.BoolAttribute("isLet"),
                IsDeprecated = elem.BoolAttribute("isDeprecated"),
                IsUnavailable = elem.BoolAttribute("isUnavailable"),
                IsOptional = elem.BoolAttribute("isOptional")

            };

            property.TypeSpec = folder.FoldAlias(parent, property.TypeSpec);

            return property;
        }

        protected override XElement MakeXElement()
        {
            return new XElement("property",
                                 new XAttribute("name", Name),
                                 new XAttribute("accessibility", Access),
                                 new XAttribute("type", TypeName),
                                 new XAttribute("storage", Storage),
                                 new XAttribute("isStatic", IsStatic),
                                 new XAttribute("isLet", IsLet),
                         new XAttribute("isDeprecated", IsDeprecated),
                         new XAttribute("isUnavailable", IsUnavailable),
                                 new XAttribute("isOptional", IsOptional)
                                );
        }

        public static StorageKind StorageKindFromString(string value)
        {
            if (value == null)
                return StorageKind.Unknown;
            StorageKind storage;
            Enum.TryParse(value, out storage);
            return storage;
        }

        public override string ToString()
        {
            // Forms:
            // access [modfiers] var Name: Type { [get | set] } [throws]
            // access [modifiers] subscript Name [ args ]: Type { get [set] } [throws]
            // access [modifiers] Name<Generics>(args) -> Type [throws]

            var getter = GetGetter();
            var builder = new StringBuilder();
            builder.Append(Access).Append(" ");
            if (IsStatic)
                builder.Append("static ");
            if (getter.IsSubscript)
            {
                builder.Append("subscript ").Append(base.ToString());
                builder.Append(" [").Append(getter.ParametersToString()).Append("]:");
            }
            else
            {
                builder.Append("var ").Append(Parent.ToString()).Append(".").Append(getter.PropertyName);
                builder.Append(": ");
            }

            builder.Append(getter.ReturnTypeName);
            if (GetSetter() != null)
            {
                builder.Append(" { get set }");
            }
            else
            {
                builder.Append(" { get }");
            }
            if (getter.HasThrows)
            {
                builder.Append(" throws");
            }
            return builder.ToString();
        }

        public override bool HasDynamicSelf => this.TypeSpec.HasDynamicSelf;
        public override bool HasDynamicSelfInReturnOnly => false;
        public override bool HasDynamicSelfInArguments => HasDynamicSelf;
    }
}

