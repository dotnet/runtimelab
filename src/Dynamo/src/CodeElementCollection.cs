// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Dynamo;

/// <summary>
/// Represents a collection of code elements.
/// </summary>
/// <typeparam name="T">The type of the collection element; must be an ICodeElement</typeparam>
public class CodeElementCollection<T> : List<T>, ICodeElementSet where T : ICodeElement
{
    /// <summary>
    /// Constructs a new empty collection.
    /// </summary>
    public CodeElementCollection() : base()
    {
    }

    /// <inheritdoc/>
    public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

    /// <inheritdoc/>
    public event EventHandler<WriteEventArgs> End = (s, e) => { };

    /// <inheritdoc/>
    public virtual object BeginWrite(ICodeWriter writer)
    {
        OnBeginWrite(new WriteEventArgs(writer));
        return new object();
    }

    /// <summary>
    /// Fire the Begin event
    /// </summary>
    /// <param name="args"></param>
    protected virtual void OnBeginWrite(WriteEventArgs args)
    {
        Begin(this, args);
    }

    /// <inheritdoc/>
    public virtual void Write(ICodeWriter writer, object memento)
    {
    }

    /// <inheritdoc/>
    public virtual void EndWrite(ICodeWriter writer, object memento)
    {
        OnEndWrite(new WriteEventArgs(writer));
    }

    /// <summary>
    /// Fire the End event
    /// </summary>
    /// <param name="args"></param>
    protected virtual void OnEndWrite(WriteEventArgs args)
    {
        End.FireInReverse(this, args);
    }

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<ICodeElement> Elements
    {
        get
        {
            return this.Cast<ICodeElement>();
        }
    }
}
