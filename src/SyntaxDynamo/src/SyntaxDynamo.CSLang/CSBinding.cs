// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;

namespace SyntaxDynamo.CSLang
{
    public class CSBinding : ICodeElementSet
    {
        public CSBinding(CSIdentifier name, ICSExpression? val = null, bool onOwnLine = false)
        {
            Name = name;
            Value = val;
            OnOwnLine = onOwnLine;
        }

        public CSBinding(string name, ICSExpression? val = null, bool onOwnLine = false)
            : this(new CSIdentifier(name), val, onOwnLine)
        {
        }

        public CSIdentifier Name { get; private set; }
        public ICSExpression? Value { get; private set; }
        public bool OnOwnLine { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}{1}{2}",
                Name.Name, Value == null ? "" : " = ", Value == null ? "" : Value.ToString());
        }

        #region ICodeElem implementation
        public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

        public event EventHandler<WriteEventArgs> End = (s, e) => { };

        public object? BeginWrite(ICodeWriter writer)
        {
            OnBegin(new WriteEventArgs(writer));
            return null;
        }

        protected virtual void OnBegin(WriteEventArgs args)
        {
            Begin(this, args);
            if (OnOwnLine)
                args.Writer.BeginNewLine(true);
        }

        public void Write(ICodeWriter writer, object? o)
        {
        }

        public void EndWrite(ICodeWriter writer, object? o)
        {
            OnEnd(new WriteEventArgs(writer));
        }

        protected virtual void OnEnd(WriteEventArgs args)
        {
            End.FireInReverse(this, args);
        }
        #endregion

        #region ICodeElemSet implementation
        public IEnumerable<ICodeElement> Elements
        {
            get
            {
                yield return Name;
                if (Value != null)
                {
                    yield return new SimpleElement(" = ", true);
                    yield return Value;
                }
            }
        }
        #endregion
    }
}
