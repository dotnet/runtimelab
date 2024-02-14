// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.CodeDom.Compiler;
using System.CodeDom;

namespace SyntaxDynamo.SwiftLang {
	public class SLConstant : SLBaseExpr {
		public SLConstant (string val)
		{
			Value = Exceptions.ThrowOnNull (val, nameof(val));
		}

		public string Value { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write (Value, false);
		}

		public static SLConstant Val (byte b) { return new SLConstant (b.ToString ()); }
		public static SLConstant Val (sbyte sb) { return new SLConstant (sb.ToString ()); }
		public static SLConstant Val (ushort us) { return new SLConstant (us.ToString ()); }
		public static SLConstant Val (short s) { return new SLConstant (s.ToString ()); }
		public static SLConstant Val (uint ui) { return new SLConstant (ui.ToString ()); }
		public static SLConstant Val (int i) { return new SLConstant (i.ToString ()); }
		public static SLConstant Val (ulong ul) { return new SLConstant (ul.ToString ()); }
		public static SLConstant Val (long l) { return new SLConstant (l.ToString ()); }
		public static SLConstant Val (float f)
		{
			return new SLConstant (f.ToString ()); // AFAIK, there is no explicit way to distinguish between a float or double
							       // constant in swift
		}
		public static SLConstant Val (double d) { return new SLConstant (d.ToString ()); }
		public static SLConstant Val (bool b) { return new SLConstant (b ? "true" : "false"); }
		public static SLConstant Val (char c) { return new SLConstant (ToCharLiteral (c)); }
		public static SLConstant Val (string s) { return new SLConstant (ToStringLiteral (s)); }

		static SLConstant any = new SLConstant ("_");
		public static SLConstant Any { get { return any; } }

		static SLConstant kNil = new SLConstant ("nil");
		public static SLConstant Nil { get { return kNil; } }

		static string ToCharLiteral (char c) // uses C# semantics. Eh.
		{
			using (var writer = new StringWriter ()) {
				using (var provider = CodeDomProvider.CreateProvider ("CSharp")) {
					provider.GenerateCodeFromExpression (new CodePrimitiveExpression (c), writer, null);
					return writer.ToString ();
				}
			}
		}

		static string ToStringLiteral (string s)
		{
			using (var writer = new StringWriter ()) {
				using (var provider = CodeDomProvider.CreateProvider ("CSharp")) {
					provider.GenerateCodeFromExpression (new CodePrimitiveExpression (s), writer, null);
					return writer.ToString ();
				}
			}
		}

	}
}

