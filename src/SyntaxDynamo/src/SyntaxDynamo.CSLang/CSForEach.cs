// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace SyntaxDynamo.CSLang
{
    public class CSForEach : CodeElementCollection<ICodeElement>, ICSStatement
    {

        class ForElement : DelegatedSimpleElement, ICSStatement
        {
            public ForElement(CSType type, CSIdentifier ident, CSBaseExpression expr)
                : base()
            {
                Type = type;
                Ident = ident;
                Expr = expr;
            }

            public CSType Type { get; private set; }
            public CSIdentifier Ident { get; private set; }
            public CSBaseExpression Expr { get; private set; }

            protected override void LLWrite(ICodeWriter writer, object o)
            {
                writer.BeginNewLine(true);
                writer.Write("foreach (", false);
                if (Type != null)
                {
                    Type.WriteAll(writer);
                }
                else
                {
                    writer.Write("var", false);
                }
                SimpleElement.Spacer.WriteAll(writer);
                Ident.WriteAll(writer);
                writer.Write(" in ", true);
                Expr.WriteAll(writer);
                writer.Write(")", false);
                writer.EndLine();
            }
        }

        public CSForEach(CSType type, CSIdentifier ident, CSBaseExpression expr, CSCodeBlock body)
        {
            ArgumentNullException.ThrowIfNull(ident, nameof(ident));
            ArgumentNullException.ThrowIfNull(expr, nameof(expr));
            Type = type;
            Ident = ident;
            Expr = expr;
            Body = body ?? new CSCodeBlock();
            Add(new ForElement(type, ident, expr));
            Add(Body);
        }

        public CSForEach(CSType type, string ident, CSBaseExpression expr, CSCodeBlock body)
            : this(type, new CSIdentifier(ident), expr, body)
        {
        }

        public CSType Type { get; private set; }
        public CSIdentifier Ident { get; private set; }
        public CSBaseExpression Expr { get; private set; }
        public CSCodeBlock Body { get; private set; }
    }
}
