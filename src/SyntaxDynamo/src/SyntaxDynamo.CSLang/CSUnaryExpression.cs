// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.CSLang
{
    public class CSUnaryExpression : CSBaseExpression
    {
        public CSUnaryExpression(CSUnaryOperator op, ICSExpression expr)
        {
            ArgumentNullException.ThrowIfNull(expr, nameof(expr));
            Operation = op;
            Expr = expr;
        }
        protected override void LLWrite(ICodeWriter writer, object o)
        {
            if (IsPostfix(Operation))
            {
                Expr.WriteAll(writer);
                writer.Write(OperatorToString(Operation), true);
            }
            else
            {
                writer.Write(OperatorToString(Operation), true);
                Expr.WriteAll(writer);
            }
        }

        public CSUnaryOperator Operation { get; private set; }
        public ICSExpression Expr { get; private set; }

        static string OperatorToString(CSUnaryOperator op)
        {
            switch (op)
            {
                case CSUnaryOperator.At:
                    return "@";
                case CSUnaryOperator.BitNot:
                    return "~";
                case CSUnaryOperator.Neg:
                    return "-";
                case CSUnaryOperator.Not:
                    return "!";
                case CSUnaryOperator.Out:
                    return "out ";
                case CSUnaryOperator.Pos:
                    return "+";
                case CSUnaryOperator.Ref:
                    return "ref ";
                case CSUnaryOperator.AddressOf:
                    return "&";
                case CSUnaryOperator.Indirection:
                    return "*";
                case CSUnaryOperator.Await:
                    return "await ";
                case CSUnaryOperator.PostBang:
                    return "!";
                case CSUnaryOperator.Question:
                    return "?";
                default:
                    throw new ArgumentOutOfRangeException(nameof(op));
            }
        }

        static bool IsPostfix(CSUnaryOperator op)
        {
            return op == CSUnaryOperator.PostBang || op == CSUnaryOperator.Question;
        }

        public static CSUnaryExpression AddressOf(ICSExpression expr)
        {
            return new CSUnaryExpression(CSUnaryOperator.AddressOf, expr);
        }

        public static CSUnaryExpression Star(ICSExpression expr)
        {
            return new CSUnaryExpression(CSUnaryOperator.Indirection, expr);
        }

        public static CSUnaryExpression Out(CSIdentifier id)
        {
            return new CSUnaryExpression(CSUnaryOperator.Out, id);
        }

        public static CSUnaryExpression Out(string id)
        {
            return Out(new CSIdentifier(id));
        }

        public static CSUnaryExpression Ref(CSIdentifier id)
        {
            return new CSUnaryExpression(CSUnaryOperator.Ref, id);
        }

        public static CSUnaryExpression Ref(string id)
        {
            return Ref(new CSIdentifier(id));
        }

        public static CSUnaryExpression Await(ICSExpression expr)
        {
            return new CSUnaryExpression(CSUnaryOperator.Await, expr);
        }

        public static CSUnaryExpression PostBang(ICSExpression expr)
        {
            return new CSUnaryExpression(CSUnaryOperator.PostBang, expr);
        }

        public static CSUnaryExpression Question(ICSExpression expr)
        {
            return new CSUnaryExpression(CSUnaryOperator.Question, expr);
        }
    }
}
