// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public abstract class SLBaseExpr : DelegatedSimpleElement, ISLExpr {
		public static SLBaseExpr operator + (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.Add, lhs, rhs);
		}

		public static SLBaseExpr operator - (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.Sub, lhs, rhs);
		}

		public static SLBaseExpr operator * (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.Mul, lhs, rhs);
		}

		public static SLBaseExpr operator / (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.Div, lhs, rhs);
		}
	
		public static SLBaseExpr operator % (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.Mod, lhs, rhs);
		}

		public static SLBaseExpr operator < (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.Less, lhs, rhs);
		}

		public static SLBaseExpr operator > (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.Greater, lhs, rhs);
		}

		public static SLBaseExpr operator <= (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.LessEqual, lhs, rhs);
		}

		public static SLBaseExpr operator >= (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.GreaterEqual, lhs, rhs);
		}

		public static SLBaseExpr operator & (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.BitAnd, lhs, rhs);
		}

		public static SLBaseExpr operator | (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.BitOr, lhs, rhs);
		}

		public static SLBaseExpr operator ^ (SLBaseExpr lhs, SLBaseExpr rhs)
		{
			return new SLBinaryExpr (BinaryOp.BitXor, lhs, rhs);
		}

		public static SLBaseExpr operator << (SLBaseExpr lhs, int bits)
		{
			return new SLBinaryExpr (BinaryOp.LeftShift, lhs, SLConstant.Val (bits));
		}

		public static SLBaseExpr operator >> (SLBaseExpr lhs, int bits)
		{
			return new SLBinaryExpr (BinaryOp.RightShift, lhs, SLConstant.Val (bits));
		}

		public SLBaseExpr Dot (SLBaseExpr other)
		{
			return new SLBinaryExpr (BinaryOp.Dot, this, other);
		}
	}
}

