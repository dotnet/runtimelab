// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang {
	public class CSAssignment : CSBaseExpression, ICSLineable {
		public CSAssignment (CSBaseExpression lhs, CSAssignmentOperator op, CSBaseExpression rhs)
		{
			Target = Exceptions.ThrowOnNull (lhs, "lhs");
			Value = Exceptions.ThrowOnNull (rhs, "rhs");
			Operation = op;
		}

		public CSAssignment(string lhs, CSAssignmentOperator op, CSBaseExpression rhs)
			: this(new CSIdentifier(Exceptions.ThrowOnNull(lhs, "lhs")), op, rhs)
		{
		}

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			Target.WriteAll (writer);
			writer.Write (string.Format (" {0} ", ToAssignmentOpString (Operation)), true);
			Value.WriteAll (writer);
		}

		public CSBaseExpression Target { get; private set; }
		public CSBaseExpression Value { get; private set; }
		public CSAssignmentOperator Operation { get; private set; }

		static string ToAssignmentOpString(CSAssignmentOperator op)
		{
			switch (op) {
			case CSAssignmentOperator.Assign:
				return "=";
			case CSAssignmentOperator.AddAssign:
				return "+=";
			case CSAssignmentOperator.SubAssign:
				return "-=";
			case CSAssignmentOperator.MulAssign:
				return "*=";
			case CSAssignmentOperator.DivAssign:
				return "/=";
			case CSAssignmentOperator.ModAssign:
				return "%=";
			case CSAssignmentOperator.AndAssign:
				return "&=";
			case CSAssignmentOperator.OrAssign:
				return "|=";
			case CSAssignmentOperator.XorAssign:
				return "^=";
			default:
				throw new ArgumentOutOfRangeException ("op");
			}
		}

		public static CSLine Assign(string name, CSAssignmentOperator op, CSBaseExpression value)
		{
			return new CSLine (new CSAssignment (name, op, value));
		}

		public static CSLine Assign(string name, CSBaseExpression value)
		{
			return Assign (name, CSAssignmentOperator.Assign, value);
		}

		public static CSLine Assign(CSBaseExpression name, CSBaseExpression value)
		{
			return Assign (name, CSAssignmentOperator.Assign, value);
		}

		public static CSLine Assign(CSBaseExpression name, CSAssignmentOperator op, CSBaseExpression value)
		{
			return new CSLine (new CSAssignment (name, op, value));
		}
	}
}

