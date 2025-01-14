// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Dynamo;

/// <summary>
/// Interface for writing code to a file or other output. Code writers track
/// position in line and indentation level.
/// </summary>
public interface ICodeWriter {
    /// <summary>
    /// Start a new line, optionally prepending indents.
    /// </summary>
    /// <param name="prependIndents"></param>
    void BeginNewLine (bool prependIndents);

    /// <summary>
    /// Ends the current line.
    /// </summary>
    void EndLine ();

    /// <summary>
    /// Write a single character to the output.
    /// </summary>
    /// <param name="c">The character to write</param>
    /// <param name="allowSplit">If true, allows the writer to insert a line break, splitting the current line</param>
    void Write (char c, bool allowSplit);

    /// <summary>
    /// Write a string to the output.
    /// </summary>
    /// <param name="text">The text to write</param>
    /// <param name="allowSplit">If true, allows the writer to insert a line break, splitting the current line.</param>
    void Write (string text, bool allowSplit);

    /// <summary>
    /// Increase the current indentation level.
    /// </summary>
    void Indent ();

    /// <summary>
    /// Decrease the current indentation level.
    /// </summary>
    void Exdent ();

    /// <summary>
    /// Returns the current indentation level.
    /// </summary>
    int IndentLevel { get; }

    /// <summary>
    /// Returns true if the writer is at the start of a line.
    /// </summary>
    bool IsAtLineStart { get; }
}

