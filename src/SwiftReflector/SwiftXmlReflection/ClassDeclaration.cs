// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using SyntaxDynamo.CSLang;

namespace SwiftReflector.SwiftXmlReflection
{
    public class ClassDeclaration : TypeDeclaration
    {
        public ClassDeclaration()
            : base()
        {
            Kind = TypeKind.Class;
            CSharpMethods = new List<CSMethod>();
            CSharpProperties = new List<CSProperty>();
        }

        protected override TypeDeclaration UnrootedFactory()
        {
            return new ClassDeclaration();
        }

        // These are strictly for imported members from C# dll's.
        // These members should not get serialized.
        public bool IsImportedBinding { get; set; }
        public List<CSMethod> CSharpMethods { get; }
        public List<CSProperty> CSharpProperties { get; }
    }

}

