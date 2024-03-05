// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang
{
    public class CSFile : ICodeElementSet
    {
        public CSFile(CSUsingPackages use, IEnumerable<CSNamespace> ns)
        {
            Using = use ?? new CSUsingPackages();
            ns = ns ?? new CSNamespace[0];
            Namespaces = new CSNamespaceBlock(ns);
        }

        public static CSFile Create(CSUsingPackages use, params CSNamespace[] ns)
        {
            return new CSFile(use, ns);
        }

        public CSUsingPackages Using { get; private set; }
        public CSNamespaceBlock Namespaces { get; private set; }

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
            End(this, args);
        }

        #endregion

        #region ICodeElemSet implementation

        public System.Collections.Generic.IEnumerable<ICodeElement> Elements
        {
            get
            {
                yield return Using;
                yield return Namespaces;
            }
        }

        #endregion
    }
}
