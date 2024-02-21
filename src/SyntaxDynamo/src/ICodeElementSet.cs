// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace SyntaxDynamo
{
    public interface ICodeElementSet : ICodeElement
    {
        IEnumerable<ICodeElement> Elements { get; }
    }
}

