// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Dynamo;

/// <summary>
/// SimpleElement is a basic code element that contains a single string label.
/// </summary>
[DebuggerDisplay("{Label}")]
public class SimpleElement : ICodeElement
{
    bool allowSplit;
    public SimpleElement(string label, bool allowSplit = false)
    {
        Label = label;
        this.allowSplit = allowSplit;
    }

    /// <summary>
    /// The label for the element.
    /// </summary>
    public string Label { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

    /// <inheritdoc/>
    public event EventHandler<WriteEventArgs> End = (s, e) => { };

    /// <inheritdoc/>
    public object BeginWrite(ICodeWriter writer)
    {
        OnBegin(new WriteEventArgs(writer));
        return new object();
    }

    /// <summary>
    /// Fire the Begin event.
    /// </summary>
    /// <param name="args">the event arguments for the Begin event</param>    
    protected virtual void OnBegin(WriteEventArgs args)
    {
        Begin(this, args);
    }

    /// <inheritdoc/>
    public void Write(ICodeWriter writer, object o)
    {
        writer.Write(Label, allowSplit);
    }

    /// <inheritdoc/>
    public void EndWrite(ICodeWriter writer, object o)
    {
        OnEnd(new WriteEventArgs(writer));
    }

    /// <summary>
    /// Fire the End event.
    /// </summary>
    /// <param name="args">the event arguments for the End event</param>
    protected virtual void OnEnd(WriteEventArgs args)
    {
        End.FireInReverse(this, args);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Label;
    }

    static SimpleElement spacer = new SimpleElement(" ", true);

    /// <summary>
    /// A simple element that represents a space.
    /// </summary>
    public static SimpleElement Spacer { get { return spacer; } }
}

