// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SwiftReflector.TypeMapping;

namespace SwiftReflector.SwiftXmlReflection
{
    public class ModuleDeclaration
    {
        public ModuleDeclaration()
        {
            Declarations = new List<BaseDeclaration>();
            Extensions = new List<ExtensionDeclaration>();
            Operators = new List<OperatorDeclaration>();
            TypeAliases = new List<TypeAliasDeclaration>();
        }

        public ModuleDeclaration(string name)
            : this()
        {
            Name = name;
        }

        public string Name { get; set; }
        public bool IsUnrooted { get; private set; }

        public ModuleDeclaration MakeUnrooted()
        {
            if (IsUnrooted)
                return this;
            ModuleDeclaration unrooted = new ModuleDeclaration();
            unrooted.IsUnrooted = true;
            unrooted.Name = Name;
            return unrooted;
        }

        public List<BaseDeclaration> Declarations { get; set; }
        public List<TypeAliasDeclaration> TypeAliases { get; private set; }

        public static ModuleDeclaration FromXElement(XElement elem, TypeDatabase typeDatabase)
        {
            ModuleDeclaration decl = new ModuleDeclaration
            {
                Name = (string)elem.Attribute("name"),
                SwiftCompilerVersion = new Version((string)elem.Attribute("swiftVersion") ?? "3.1")
            };

            decl.TypeAliases.AddRange(elem.Descendants("typealias").Select(al => TypeAliasDeclaration.FromXElement(decl.Name, al)));
            var folder = new TypeAliasFolder(decl.TypeAliases);
            folder.AddDatabaseAliases(typeDatabase);

            // non extensions
            foreach (var child in elem.Elements())
            {
                if (child.Name == "extension")
                {
                    decl.Extensions.Add(ExtensionDeclaration.FromXElement(folder, child, decl));
                }
                else if (child.Name == "operator")
                {
                    decl.Operators.Add(OperatorDeclaration.FromXElement(child, child.Attribute("moduleName")?.Value));
                }
                else
                {
                    decl.Declarations.Add(BaseDeclaration.FromXElement(folder, child, decl, null));
                }
            }
            return decl;
        }

        public Version SwiftCompilerVersion { get; set; }
        public bool IsEmpty()
        {
            return Declarations.Count == 0 && Extensions.Count == 0;
        }

        public IEnumerable<ClassDeclaration> Classes { get { return Declarations.OfType<ClassDeclaration>().Where(cd => !(cd is ProtocolDeclaration)); } }
        public IEnumerable<StructDeclaration> Structs { get { return Declarations.OfType<StructDeclaration>(); } }
        public IEnumerable<EnumDeclaration> Enums { get { return Declarations.OfType<EnumDeclaration>(); } }
        public IEnumerable<ProtocolDeclaration> Protocols { get { return Declarations.OfType<ProtocolDeclaration>(); } }
        public IEnumerable<FunctionDeclaration> Functions { get { return Declarations.OfType<FunctionDeclaration>(); } }
        public IEnumerable<PropertyDeclaration> Properties { get { return Declarations.OfType<PropertyDeclaration>(); } }
        public IEnumerable<FunctionDeclaration> TopLevelFunctions { get { return Functions.Where(f => f.Parent == null && f.Access == Accessibility.Public || f.Access == Accessibility.Open); } }
        public IEnumerable<PropertyDeclaration> TopLevelProperties { get { return Properties.Where(p => p.Parent == null && p.Access == Accessibility.Public || p.Access == Accessibility.Open); } }
        public List<ExtensionDeclaration> Extensions { get; private set; }
        public List<OperatorDeclaration> Operators { get; private set; }


        public bool IsCompilerCompatibleWith(Version targetCompilerVersion)
        {
            // yes, this could be an equality comparison, but I expect some
            // level of backwards compatability at some point, so flesh it out now.
            switch (SwiftCompilerVersion.Major)
            {
                case 2:
                    return false; // No. Just no.
                case 3:
                    return targetCompilerVersion.Major == 3;
                case 4:
                    return targetCompilerVersion.Major == 4;
                case 5:
                    return targetCompilerVersion.Major == 5;
                default:
                    return false; // not yet, thanks.
            }
        }

        public List<ClassDeclaration> AllClasses
        {
            get
            {
                return AllFooHelper<ClassDeclaration>();
            }
        }

        public List<StructDeclaration> AllStructs
        {
            get
            {
                return AllFooHelper<StructDeclaration>();
            }
        }

        public List<EnumDeclaration> AllEnums
        {
            get
            {
                return AllFooHelper<EnumDeclaration>();
            }
        }

        public List<ProtocolDeclaration> AllProtocols
        {
            get
            {
                // no chicanery here - all protocol definitions are top-level
                return Protocols.ToList();
            }
        }

        List<T> AllFooHelper<T>() where T : TypeDeclaration
        {
            List<T> ts = new List<T>();
            AddAllInto<T>(Classes, ts);
            AddAllInto<T>(Structs, ts);
            AddAllInto<T>(Enums, ts);
            return ts;
        }


        void AddAllInto<T>(IEnumerable<TypeDeclaration> someTypes, List<T> repository) where T : TypeDeclaration
        {
            foreach (TypeDeclaration t in someTypes)
            {
                if (t is T)
                    repository.Add((T)t);
                AddAllInto(t.InnerClasses, repository);
                AddAllInto(t.InnerStructs, repository);
                AddAllInto(t.InnerEnums, repository);
            }
        }


        public List<BaseDeclaration> AllTypesAndTopLevelDeclarations
        {
            get
            {
                List<BaseDeclaration> decls = new List<BaseDeclaration>();
                AddAllDeclsInto(Declarations, decls);
                return decls;
            }
        }

        void AddAllDeclsInto(IEnumerable<BaseDeclaration> someDecls, List<BaseDeclaration> allDecls)
        {
            foreach (BaseDeclaration d in someDecls)
            {
                allDecls.Add(d);
                TypeDeclaration t = d as TypeDeclaration;
                if (t != null)
                {
                    AddAllDeclsInto(t.InnerClasses, allDecls);
                    AddAllDeclsInto(t.InnerStructs, allDecls);
                    AddAllDeclsInto(t.InnerEnums, allDecls);
                }
            }
        }

        public List<TypeDeclaration> AllTypes
        {
            get
            {
                List<TypeDeclaration> types = new List<TypeDeclaration>();
                AddAllTypesInto(Declarations.OfType<TypeDeclaration>(), types);
                return types;
            }
        }

        void AddAllTypesInto(IEnumerable<TypeDeclaration> someTypes, List<TypeDeclaration> allTypes)
        {
            foreach (TypeDeclaration t in someTypes)
            {
                allTypes.Add(t);
                AddAllTypesInto(t.InnerClasses, allTypes);
                AddAllTypesInto(t.InnerStructs, allTypes);
                AddAllTypesInto(t.InnerEnums, allTypes);
            }
        }
    }
}

