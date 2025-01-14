// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Dynamo;

/// <summary>
/// Represents a code element that can be written to a code writer.
/// </summary>
public interface ICodeElement
{
    /// <summary>
    /// When implemented, this method should write the code element to the code writer.
    /// BeginWrite should also fire the Begin event before writing anything.
    /// </summary>
    /// <param name="writer">The writer for the code</param>
    /// <returns>A memento that will be passed back to Write and EndWrite</returns>
    object BeginWrite(ICodeWriter writer);

    /// <summary>
    /// When implemented, this method should write the code element to the code writer.
    /// </summary>
    /// <param name="writer">The writer for the code</param>
    /// <param name="memento">The memento returned from BeginWrite</param>
    void Write(ICodeWriter writer, object memento);

    /// <summary>
    /// When implemented, this method should perform any cleanup or final writing of the code element to the code writer.
    /// EndWrite should also fire the End event after writing everything in *reverse* order.
    /// This can be done by calling GetInvocationList on the event and iterating in reverse order.
    /// </summary>
    /// <param name="writer">The writer for the code</param>
    /// <param name="memento">The memento returned from BeginWrite</param>
    void EndWrite(ICodeWriter writer, object memento);

    // These events seem redundant, but they are intended for use for non-structural code elements
    // such as block comments or #region or #if/#else/#endif

    /// <summary>
    /// Event that is fired before writing the code element.
    /// </summary>
    event EventHandler<WriteEventArgs> Begin;
    /// <summary>
    /// Event that is fired after writing the code element.
    /// </summary>
    event EventHandler<WriteEventArgs> End;
}

