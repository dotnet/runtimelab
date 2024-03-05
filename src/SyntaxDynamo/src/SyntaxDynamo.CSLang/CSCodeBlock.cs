// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.CSLang
{
    public class CSCodeBlock : DecoratedCodeElementCollection<ICodeElement>, ICSStatement
    {
        public CSCodeBlock()
            : this(null)
        {

        }

        public CSCodeBlock(IEnumerable<ICodeElement> statements)
            : this("{", "}", statements)
        {
        }

        public static CSCodeBlock Create(params ICodeElement[] statements)
        {
            return new CSCodeBlock(statements);
        }

        public CSCodeBlock(string start, string end, IEnumerable<ICodeElement> statements)
            : base(start, end, true, true, true)
        {
            if (statements != null)
            {
                foreach (ICodeElement elem in statements)
                {
                    And(elem);
                }
            }
        }

        public CSCodeBlock And(ICodeElement elem)
        {
            if (!(elem is ICSStatement))
                throw new ArgumentException($"contents must each be an IStatement, got {elem.GetType()}");
            Add(elem);
            return this;
        }
    }

    public class CSUnsafeCodeBlock : CSCodeBlock
    {
        public CSUnsafeCodeBlock(IEnumerable<ICodeElement> statements)
            : base("unsafe {", "}", statements)
        {
        }
    }

}

