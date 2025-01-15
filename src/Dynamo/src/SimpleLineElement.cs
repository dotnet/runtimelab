// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Dynamo;

/// <summary>
/// Represents a code element that is exists on a single line.
/// </summary>
[DebuggerDisplay("{Contents}")]
public class SimpleLineElement : ICodeElement
{
    bool indent, prependIndents, allowSplit;

    /// <summary>
    /// Creates a new SimpleLineElement with the specified contents.
    /// </summary>
    /// <param name="contents">The contents of the line</param>
    /// <param name="indent">if true, indent the line before writing</param>
    /// <param name="prependIdents">if true, prepend the indents before the new line</param>
    /// <param name="allowSplit">if true, allow a line break in the contents</param>
    public SimpleLineElement(string contents, bool indent, bool prependIndents, bool allowSplit)
    {
        Contents = contents;
        this.indent = indent;
        this.prependIndents = prependIndents;
        this.allowSplit = allowSplit;
    }

    /// <summary>
    /// The contents of the line.
    /// </summary>
    public string Contents { get; private set; }

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
    public void Write(ICodeWriter writer, object memento)
    {
        if (indent)
            writer.Indent();
        writer.BeginNewLine(prependIndents);
        writer.Write(Contents, allowSplit);
        writer.EndLine();
    }

    /// <inheritdoc/>
    public void EndWrite(ICodeWriter writer, object memento)
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
        return Contents;
    }
}


