// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo
{
    public interface ICodeElement
    {
        // these three methods represent the memento pattern. The object returned is only ever used by
        // the ICodeElem.
        object BeginWrite(ICodeWriter writer);
        void Write(ICodeWriter writer, object o);
        void EndWrite(ICodeWriter writer, object o);

        // These events seem redundant, but they are intended for use for non-structural code elements
        // such as block comments or #region or #if/#else/#endif

        // Begin should be fired by BeginWrite BEFORE anything is written
        event EventHandler<WriteEventArgs> Begin;
        // Note, when you implement End, it should use GetInvocationList and fire in reverse order.
        // End should be fired by EndWrite AFTER anything is written
        event EventHandler<WriteEventArgs> End;
    }
}
