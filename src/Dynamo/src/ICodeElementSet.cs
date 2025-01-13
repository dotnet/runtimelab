// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

#nullable enable

namespace Dynamo;

/// <summary>
/// Represents a set of code elements
/// </summary>
public interface ICodeElementSet : ICodeElement
{
    /// <summary>
    /// Returns the code elements in the set.
    /// </summary>
    IEnumerable<ICodeElement> Elements { get; }
}

