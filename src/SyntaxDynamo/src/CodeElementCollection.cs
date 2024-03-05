// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntaxDynamo
{
    public class CodeElementCollection<T> : List<T>, ICodeElementSet where T : ICodeElement
    {
        public CodeElementCollection() : base()
        {
        }

        #region ICodeElem implementation

        public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

        public event EventHandler<WriteEventArgs> End = (s, e) => { };

        public virtual object BeginWrite(ICodeWriter writer)
        {
            OnBeginWrite(new WriteEventArgs(writer));
            return null;
        }

        protected virtual void OnBeginWrite(WriteEventArgs args)
        {
            Begin(this, args);
        }

        public virtual void Write(ICodeWriter writer, object o)
        {
        }

        public virtual void EndWrite(ICodeWriter writer, object o)
        {
            OnEndWrite(new WriteEventArgs(writer));
        }

        protected virtual void OnEndWrite(WriteEventArgs args)
        {
            End.FireInReverse(this, args);
        }

        #endregion

        #region ICodeElemSet implementation

        public System.Collections.Generic.IEnumerable<ICodeElement> Elements
        {
            get
            {
                return this.Cast<ICodeElement>();
            }
        }

        #endregion

    }
}
