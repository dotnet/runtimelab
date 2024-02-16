// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang
{
    public class CSLine : DelegatedSimpleElement, ICSStatement
    {
        public CSLine(ICodeElement contents, bool addSemicolon = true)
        {
            Contents = Exceptions.ThrowOnNull(contents, nameof(contents));
            if (!(contents is ICSLineable) && addSemicolon)
                throw new ArgumentException("contents must be ILineable", nameof(contents));
            AddSemicolon = addSemicolon;
        }

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            writer.BeginNewLine(true);
            Contents.WriteAll(writer);
            if (AddSemicolon)
                writer.Write(';', false);
            writer.EndLine();
        }

        public ICodeElement Contents { get; private set; }
        public bool AddSemicolon { get; set; }
    }
}

