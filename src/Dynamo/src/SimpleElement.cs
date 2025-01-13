// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Dynamo;

[DebuggerDisplay("{Label}")]
public class SimpleElement : ICodeElement
{
    bool allowSplit;
    public SimpleElement(string label, bool allowSplit = false)
    {
        Label = label;
        this.allowSplit = allowSplit;
    }

    public string Label { get; private set; }

    public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

    public event EventHandler<WriteEventArgs> End = (s, e) => { };

    public object BeginWrite(ICodeWriter writer)
    {
        OnBegin(new WriteEventArgs(writer));
        return new object();
    }

    protected virtual void OnBegin(WriteEventArgs args)
    {
        Begin(this, args);
    }

    public void Write(ICodeWriter writer, object o)
    {
        writer.Write(Label, allowSplit);
    }

    public void EndWrite(ICodeWriter writer, object o)
    {
        OnEnd(new WriteEventArgs(writer));
    }

    protected virtual void OnEnd(WriteEventArgs args)
    {
        End.FireInReverse(this, args);
    }

    public override string ToString()
    {
        return Label;
    }

    static SimpleElement spacer = new SimpleElement(" ", true);
    public static SimpleElement Spacer { get { return spacer; } }
}

