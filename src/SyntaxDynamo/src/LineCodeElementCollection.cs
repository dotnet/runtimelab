// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace SyntaxDynamo
{
    public class LineCodeElementCollection<T> : CodeElementCollection<T> where T : ICodeElement
    {
        bool indent, prependIndents, isSingleLine;
        public LineCodeElementCollection(IEnumerable<T>? elems, bool indent, bool prependIndents)
            : this(elems, true, indent, prependIndents)
        {
        }

        public LineCodeElementCollection(bool isSingleLine, bool indent, bool prependIndents)
            : this(null, isSingleLine, indent, prependIndents)
        {
        }

        public LineCodeElementCollection(IEnumerable<T>? elems, bool isSingleLine, bool indent, bool prependIndents)
        {
            this.isSingleLine = isSingleLine;
            this.indent = indent;
            this.prependIndents = prependIndents;
            if (elems != null)
                AddRange(elems);
        }

        public override void Write(ICodeWriter writer, object? o)
        {
            if (isSingleLine)
            {
                if (indent)
                    writer.Indent();
                writer.BeginNewLine(prependIndents);
            }
        }

        protected override void OnEndWrite(WriteEventArgs args)
        {
            if (isSingleLine)
                args.Writer.EndLine();
            base.OnEndWrite(args);
        }

        public LineCodeElementCollection<T> And(T elem)
        {
            Add(elem);
            return this;
        }

    }
}
