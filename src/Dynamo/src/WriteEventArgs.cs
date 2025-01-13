// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Dynamo;

/// <summary>
/// Represents event args for a code writer.
/// </summary>
public class WriteEventArgs : EventArgs
{
    public WriteEventArgs(ICodeWriter writer)
    {
        Writer = writer;
    }

    /// <summary>
    /// Gets the code writer.
    /// </summary>
    public ICodeWriter Writer { get; private set; }
}


