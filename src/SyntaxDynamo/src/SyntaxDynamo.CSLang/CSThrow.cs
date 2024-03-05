// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.CSLang
{
    public class CSThrow : CSBaseExpression, ICSStatement, ICSLineable
    {
        public CSThrow(CSBaseExpression expr)
        {
            ArgumentNullException.ThrowIfNull(expr, nameof(expr));
            Expr = expr;
        }

        public CSBaseExpression Expr { get; private set; }

        #region implemented abstract members of DelegatedSimpleElem

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            writer.Write("throw ", true);
            Expr.WriteAll(writer);
        }

        #endregion

        public static CSLine ThrowLine<T>(T exType, CommaListElementCollection<CSBaseExpression> args) where T : Exception
        {
            return new CSLine(new CSThrow(new CSFunctionCall(new CSIdentifier(exType.GetType().Name), args, true)));
        }

        public static CSLine ThrowLine<T>(T exType, string message) where T : Exception
        {
            CommaListElementCollection<CSBaseExpression> args = new CommaListElementCollection<CSBaseExpression>();
            if (message != null)
                args.Add(CSConstant.Val(message));
            return ThrowLine(exType, args);
        }

        public static CSLine ThrowLine<T>(T exType, CSBaseExpression expr) where T : Exception
        {
            ArgumentNullException.ThrowIfNull(expr, nameof(expr));
            CommaListElementCollection<CSBaseExpression> args = new CommaListElementCollection<CSBaseExpression>();
            args.Add(expr);
            return ThrowLine(exType, args);
        }
    }
}
