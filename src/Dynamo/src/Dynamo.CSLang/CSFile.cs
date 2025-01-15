// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dynamo;

namespace Dynamo.CSLang;

/// <summary>
/// Represents a C# file.
/// </summary>
public class CSFile : ICodeElementSet
{
    /// <summary>
    /// Construct a new C# file with the given namespace declaration.
    /// </summary>
    /// <param name="namespace">the declared namespace</param>
    public CSFile(CSNamespace @namespace)
    {
        Header = new CSCommentBlock();
        Using = new CSUsingPackages();
        Namespace = @namespace;
        Declarations = new CSTopLevelDeclarations();
    }

    /// <summary>
    /// Construct a new C# file with the given namespace declaration.
    /// </summary>
    /// <param name="namespace">the namespace for the file</param>
    public CSFile(string @namespace)
        : this(new CSNamespace(@namespace))
    {
    }

    /// <summary>
    /// The header comment block for the file.
    /// </summary>
    public CSCommentBlock Header { get; private set; }

    /// <summary>
    /// The using packages for the file.
    /// </summary>
    public CSUsingPackages Using { get; private set; }

    /// <summary>
    /// The namespace declaration for the file.
    /// </summary>
    public CSNamespace Namespace { get; private set; }

    /// <summary>
    /// The top-level declarations for the file.
    /// </summary>
    public CSTopLevelDeclarations Declarations { get; private set; }

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
    /// <param name="e">the event args for the event</param>
    protected void OnBegin(WriteEventArgs e)
    {
        Begin(this, e);
    }

    /// <inheritdoc/>
    public void Write(ICodeWriter writer, object memento)
    {
    }

    /// <inheritdoc/>
    public void EndWrite(ICodeWriter writer, object memento)
    {
        OnEnd(new WriteEventArgs(writer));
    }

    /// <summary>
    /// Fire the End event.
    /// </summary>
    /// <param name="e">the event args for the event</param>
    protected virtual void OnEnd(WriteEventArgs e)
    {
        End(this, e);
    }

    /// <inheritdoc/>
    public IEnumerable<ICodeElement> Elements
    {
        get
        {
            yield return Header;
            yield return Using;
            yield return Namespace;
            yield return Declarations;
        }
    }
}
