// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.SwiftLang {
	public enum ImportKind {
		None,
		TypeAlias,
		Struct,
		Class,
		Enum,
		Protocol,
		Var,
		Func,
	}

	public enum SLParameterKind {
		None,
		Var,
		InOut,
	}

	public enum BinaryOp {
		None = -1,
		Add,
		Sub,
		Mul,
		Div,
		Mod,
		And,
		Or,
		Less,
		Greater,
		Equal,
		NotEqual,
		LessEqual,
		GreaterEqual,
		BitAnd,
		BitOr,
		BitXor,
		LeftShift,
		RightShift,
		Dot,
		AndAdd,
		AndSub,
		AndMul,
		Assign,
	}

	public enum UnaryOp {
		Neg = 0,
		Pos,
		At,
		Not,
		BitNot,
	}

	public enum Visibility {
		None,
		Public,
		Private,
		Internal,
		Open,
		FilePrivate,
	}

	[Flags]
	public enum FunctionKind {
		None = 0,
		Override = 1 << 0,
		Final = 1 << 1,
		Static = 1 << 2,
		Throws = 1 << 3,
		Constructor = 1 << 4,
		Class = 1 << 5,
		Rethrows = 1 << 6,
		Required = 1 << 7,
		Async = 1 << 8,
	}

	public enum NamedType {
		Class = 0,
		Struct,
		Extension,
		Actor,
	}
}

