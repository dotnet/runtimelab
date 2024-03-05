// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo;
using System.Linq;
using System.Collections.Generic;

namespace SyntaxDynamo.CSLang
{
    public class CSNamespace : LabeledCodeElementCollection<ICSTopLevelDeclaration>
    {
        public CSNamespace()
            : base(new SimpleLineElement(string.Empty, false, false, false),
                new DecoratedCodeElementCollection<ICSTopLevelDeclaration>(string.Empty, string.Empty, false, false, false))
        {
        }

        public CSNamespace(string nameSpace)
            : base(new SimpleLineElement(string.Format("namespace {0}", nameSpace),
                                           false, true, false),
                new DecoratedCodeElementCollection<ICSTopLevelDeclaration>("{", "}", true, true, true))
        {
        }
    }

    public class CSNamespaceBlock : CodeElementCollection<CSNamespace>
    {
        public CSNamespaceBlock(params string[] nameSpaces)
            : base()
        {
            this.AddRange(nameSpaces.Select(s => new CSNamespace(s)));
        }

        public CSNamespaceBlock(IEnumerable<CSNamespace> nameSpaces)
            : base()
        {
            this.AddRange(nameSpaces);
        }

        public CSNamespaceBlock And(string s)
        {
            return And(new CSNamespace(s));
        }

        public CSNamespaceBlock And(CSNamespace ns)
        {
            ArgumentNullException.ThrowIfNull(ns, nameof(ns));
            Add(ns);
            return this;
        }
    }
}

