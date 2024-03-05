// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.CSLang
{
    public class CSGenericConstraint : DelegatedSimpleElement
    {
        public CSGenericConstraint(CSIdentifier name, CSIdentifier isA)
        {
            ArgumentNullException.ThrowIfNull(name, nameof(name));
            ArgumentNullException.ThrowIfNull(isA, nameof(isA));
            Name = name;
            IsA = new CommaListElementCollection<CSIdentifier>();
            IsA.Add(isA);
        }

        public CSGenericConstraint(CSIdentifier name, IEnumerable<CSIdentifier> multiIs)
        {
            ArgumentNullException.ThrowIfNull(name, nameof(name));
            Name = name;
            IsA = new CommaListElementCollection<CSIdentifier>();
            if (multiIs != null)
                IsA.AddRange(multiIs);
        }

        public CSIdentifier Name { get; private set; }
        public CommaListElementCollection<CSIdentifier> IsA { get; private set; }

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            writer.Write("where ", true);
            Name.Write(writer, o);
            writer.Write(" : ", true);
            IsA.WriteAll(writer);
        }
    }

    public class CSGenericConstraintCollection : List<CSGenericConstraint>, ICodeElementSet
    {
        public IEnumerable<ICodeElement> Elements
        {
            get
            {
                bool first = true;
                foreach (CSGenericConstraint tc in this)
                {
                    if (!first)
                    {
                        yield return new LineBreak();
                    }
                    first = false;
                    yield return tc;
                }
            }
        }

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

    }

}
