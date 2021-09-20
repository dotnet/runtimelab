// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.RegularExpressions.Symbolic
{
    /// <summary>Kinds of transition regexes</summary>
    internal enum TransitionRegexKind
    {
        Leaf,
        Conditional,
        Union
    }
}
