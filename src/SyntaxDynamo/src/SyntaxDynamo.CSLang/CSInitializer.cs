// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.CSLang
{
    public class CSInitializer : CSBaseExpression
    {
        public CSInitializer(IEnumerable<CSBaseExpression> parameters, bool appendNewlineAfterEach)
        {
            Parameters = new CommaListElementCollection<CSBaseExpression>("", "", parameters, appendNewlineAfterEach);
        }

        public CommaListElementCollection<CSBaseExpression> Parameters { get; private set; }

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            writer.Write("{ ", false);
            Parameters.WriteAll(writer);
            writer.Write(" }", false);
        }
    }

    public class CSInitializedType : CSBaseExpression
    {
        public CSInitializedType(CSFunctionCall call, CSInitializer initializer)
        {
            ArgumentNullException.ThrowIfNull(call, nameof(call));
            ArgumentNullException.ThrowIfNull(initializer, nameof(initializer));
            Call = call;
            Initializer = initializer;
        }

        public CSInitializedType(CSFunctionCall call, IEnumerable<CSBaseExpression> parameters, bool appendNewlineAfterEach)
            : this(call, new CSInitializer(parameters, appendNewlineAfterEach))
        {
        }

        public CSFunctionCall Call { get; private set; }
        public CSInitializer Initializer { get; private set; }

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            Call.WriteAll(writer);
            SimpleElement.Spacer.WriteAll(writer);
            Initializer.WriteAll(writer);
        }
    }
}
