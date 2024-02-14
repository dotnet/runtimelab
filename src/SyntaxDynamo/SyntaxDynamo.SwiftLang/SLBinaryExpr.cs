// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.SwiftLang {
	public class SLBinaryExpr : SLBaseExpr {
		public SLBinaryExpr (BinaryOp op, ISLExpr lhs, ISLExpr rhs)
		{
			Operation = op;
			CustomOperation = null;
			Left = Exceptions.ThrowOnNull (lhs, nameof(lhs));
			Right = Exceptions.ThrowOnNull (rhs, nameof(rhs));
		}

		public SLBinaryExpr (string op, ISLExpr lhs, ISLExpr rhs)
		{
			CustomOperation = Exceptions.ThrowOnNull (op, nameof (op));
			Operation = BinaryOp.None;
			Left = Exceptions.ThrowOnNull (lhs, nameof (lhs));
			Right = Exceptions.ThrowOnNull (rhs, nameof (rhs));
		}

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			Left.WriteAll (writer);
			var operation = CustomOperation ?? OpToString (Operation);
			operation = Operation == BinaryOp.Dot ? $"{operation}" : $" {operation} ";
			writer.Write (operation, true);
			Right.WriteAll (writer);
		}

		public string CustomOperation { get; private set; }
		public BinaryOp Operation { get; private set; }
		public ISLExpr Left { get; private set; }
		public ISLExpr Right { get; private set; }

		static string OpToString (BinaryOp op)
		{
			switch (op) {
			case BinaryOp.Add:
				return "+";
			case BinaryOp.AndAdd:
				return "&+";
			case BinaryOp.Sub:
				return "-";
			case BinaryOp.AndSub:
				return "&-";
			case BinaryOp.Mul:
				return "*";
			case BinaryOp.AndMul:
				return "&*";
			case BinaryOp.Div:
				return "/";
			case BinaryOp.Mod:
				return "%";
			case BinaryOp.And:
				return "&&";
			case BinaryOp.Or:
				return "||";
			case BinaryOp.Less:
				return "<";
			case BinaryOp.Greater:
				return ">";
			case BinaryOp.Equal:
				return "==";
			case BinaryOp.NotEqual:
				return "!=";
			case BinaryOp.LessEqual:
				return "<=";
			case BinaryOp.GreaterEqual:
				return ">=";
			case BinaryOp.BitAnd:
				return "&";
			case BinaryOp.BitOr:
				return "|";
			case BinaryOp.BitXor:
				return "^";
			case BinaryOp.LeftShift:
				return ">>";
			case BinaryOp.RightShift:
				return "<<";
			case BinaryOp.Dot:
				return ".";
			case BinaryOp.Assign:
				return "=";
			default:
				throw new ArgumentOutOfRangeException (nameof(op));
			}
		}
	}
}

