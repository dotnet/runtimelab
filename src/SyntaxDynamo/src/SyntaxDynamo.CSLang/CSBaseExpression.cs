// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang
{
    public abstract class CSBaseExpression : DelegatedSimpleElement, ICSExpression
    {
        public CSBaseExpression Dot(CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.Dot, this, rhs);
        }
        public static CSBaseExpression operator +(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.Add, lhs, rhs);
        }
        public static CSBaseExpression operator -(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.Sub, lhs, rhs);
        }
        public static CSBaseExpression operator *(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.Mul, lhs, rhs);
        }
        public static CSBaseExpression operator /(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.Div, lhs, rhs);
        }
        public static CSBaseExpression operator %(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.Mod, lhs, rhs);
        }
        public static CSBaseExpression operator <(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.Less, lhs, rhs);
        }
        public static CSBaseExpression operator >(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.Greater, lhs, rhs);
        }
        public static CSBaseExpression operator ==(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.Equal, lhs, rhs);
        }
        public static CSBaseExpression operator !=(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.NotEqual, lhs, rhs);
        }
        public static CSBaseExpression operator <=(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.LessEqual, lhs, rhs);
        }
        public static CSBaseExpression operator >=(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.GreaterEqual, lhs, rhs);
        }
        public static CSBaseExpression operator &(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.BitAnd, lhs, rhs);
        }
        public static CSBaseExpression operator |(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.BitOr, lhs, rhs);
        }
        public static CSBaseExpression operator ^(CSBaseExpression lhs, CSBaseExpression rhs)
        {
            return new CSBinaryExpression(CSBinaryOperator.BitXor, lhs, rhs);
        }
        public static CSBaseExpression operator <<(CSBaseExpression lhs, int bits)
        {
            return new CSBinaryExpression(CSBinaryOperator.LeftShift, lhs, CSConstant.Val(bits));
        }
        public static CSBaseExpression operator >>(CSBaseExpression lhs, int bits)
        {
            return new CSBinaryExpression(CSBinaryOperator.RightShift, lhs, CSConstant.Val(bits));
        }
        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            // Add your custom equality logic here

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            // Add your custom hash code logic here

            return base.GetHashCode();
        }
    }
}
