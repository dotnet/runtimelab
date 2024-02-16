// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;


namespace SyntaxDynamo.CSLang
{
    public class CSBinaryExpression : CSBaseExpression
    {
        public CSBinaryExpression(CSBinaryOperator op, ICSExpression lhs, ICSExpression rhs)
        {
            Operation = op;
            Left = Exceptions.ThrowOnNull(lhs, "lhs");
            Right = Exceptions.ThrowOnNull(rhs, "rhs");
        }

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            Left.WriteAll(writer);
            writer.Write(string.Format(Operation == CSBinaryOperator.Dot ? "{0}" : " {0} ", OpToString(Operation)), true);
            Right.WriteAll(writer);
        }

        public CSBinaryOperator Operation { get; private set; }
        public ICSExpression Left { get; private set; }
        public ICSExpression Right { get; private set; }

        static string OpToString(CSBinaryOperator op)
        {
            switch (op)
            {
                case CSBinaryOperator.Add:
                    return "+";
                case CSBinaryOperator.Sub:
                    return "-";
                case CSBinaryOperator.Mul:
                    return "*";
                case CSBinaryOperator.Div:
                    return "/";
                case CSBinaryOperator.Mod:
                    return "%";
                case CSBinaryOperator.And:
                    return "&&";
                case CSBinaryOperator.Or:
                    return "||";
                case CSBinaryOperator.Less:
                    return "<";
                case CSBinaryOperator.Greater:
                    return ">";
                case CSBinaryOperator.Equal:
                    return "==";
                case CSBinaryOperator.NotEqual:
                    return "!=";
                case CSBinaryOperator.LessEqual:
                    return "<=";
                case CSBinaryOperator.GreaterEqual:
                    return ">=";
                case CSBinaryOperator.BitAnd:
                    return "&";
                case CSBinaryOperator.BitOr:
                    return "|";
                case CSBinaryOperator.BitXor:
                    return "^";
                case CSBinaryOperator.LeftShift:
                    return ">>";
                case CSBinaryOperator.RightShift:
                    return "<<";
                case CSBinaryOperator.Dot:
                    return ".";
                case CSBinaryOperator.Is:
                    return "is";
                case CSBinaryOperator.As:
                    return "as";
                case CSBinaryOperator.NullCoalesce:
                    return "??";
                default:
                    throw new ArgumentOutOfRangeException("op");
            }
        }
    }
}

