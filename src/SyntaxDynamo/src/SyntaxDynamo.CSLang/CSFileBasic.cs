// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.CSLang
{
    public class CSFileBasic : ICodeElementSet
    {
        string nameSpace;

        public CSFileBasic(string nameSpace)
        {
            ArgumentNullException.ThrowIfNull(nameSpace, nameof(nameSpace));
            this.nameSpace = nameSpace;
            Using = new CSUsingPackages();
            Classes = new CSClasses();
        }

        #region ICodeElem implementation

        public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

        public event EventHandler<WriteEventArgs> End = (s, e) => { };

        public CSClasses Classes { get; private set; }
        public CSUsingPackages Using { get; private set; }

        public object BeginWrite(ICodeWriter writer)
        {
            return null;
        }

        public void Write(ICodeWriter writer, object o)
        {
        }

        public void EndWrite(ICodeWriter writer, object o)
        {
        }

        protected virtual void OnBegin(WriteEventArgs e)
        {
            Begin(this, e);
        }

        protected virtual void OnEnd(WriteEventArgs e)
        {
            End(this, e);
        }

        #endregion

        #region ICodeElemSet implementation

        public System.Collections.Generic.IEnumerable<ICodeElement> Elements
        {
            get
            {
                yield return Using;
                CSNamespace ns = new CSNamespace(nameSpace);
                ns.Block.AddRange(Classes);
                yield return ns;
            }
        }

        #endregion
    }
}
