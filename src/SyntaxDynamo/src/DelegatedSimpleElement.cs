// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo
{
    public abstract class DelegatedSimpleElement : ICodeElement
    {
        #region ICodeElem implementation

        public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

        public event EventHandler<WriteEventArgs> End = (s, e) => { };

        public object BeginWrite(ICodeWriter writer)
        {
            OnBegin(new WriteEventArgs(writer));
            return null;
        }

        protected virtual void OnBegin(WriteEventArgs args)
        {
            Begin(this, args);
        }

        public void Write(ICodeWriter writer, object o)
        {
            LLWrite(writer, o);
        }

        protected abstract void LLWrite(ICodeWriter writer, object o);

        public void EndWrite(ICodeWriter writer, object o)
        {
            OnEnd(new WriteEventArgs(writer));
        }

        protected virtual void OnEnd(WriteEventArgs args)
        {
            End.FireInReverse(this, args);
        }

        #endregion
    }

    public class LineBreak : DelegatedSimpleElement
    {
        protected override void LLWrite(ICodeWriter writer, object o)
        {
            writer.EndLine();
            writer.BeginNewLine(true);
        }
    }
}
