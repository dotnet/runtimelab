// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLFunctionCall : SLBaseExpr, ISLLineable {
		public SLFunctionCall (SLIdentifier ident, DelegatedCommaListElemCollection<SLArgument> paramList)
		{
			Name = ident;
			Parameters = paramList ?? new DelegatedCommaListElemCollection<SLArgument> (WriteElement);
		}

		public SLFunctionCall (string identifier, bool isConstructor, params SLArgument [] parameters)
			: this (new SLIdentifier (Exceptions.ThrowOnNull (identifier, "identifier")),
			       isConstructor ? new DelegatedCommaListElemCollection<SLArgument> (WriteAllElements, parameters) :
				new DelegatedCommaListElemCollection<SLArgument> (WriteElement, parameters))
		{
		}

		public SLFunctionCall (string identifier, bool isConstructor, bool writeAllParameterParts, params SLArgument [] parameters)
			: this (new SLIdentifier (Exceptions.ThrowOnNull (identifier, "identifier")),
				writeAllParameterParts ? new DelegatedCommaListElemCollection<SLArgument> (WriteAllElements, parameters) :
				new DelegatedCommaListElemCollection<SLArgument> (WriteElement, parameters))
		{
		}


		protected override void LLWrite (ICodeWriter writer, object o)
		{
			Name.WriteAll (writer);
			writer.Write ("(", false);
			Parameters.WriteAll (writer);
			writer.Write (")", false);
		}

		public SLIdentifier Name { get; private set; }
		public DelegatedCommaListElemCollection<SLArgument> Parameters { get; private set; }
		public bool IncludeFirstParameterLabel { get; set; }

		public static void WriteElement (ICodeWriter writer, int i, SLArgument arg)
		{
			if (i == 0 && !arg.IdentifierIsRequired) {
				arg.Expr.WriteAll (writer);
			} else {
				arg.WriteAll (writer);
			}
		}

		public static void WriteAllElements (ICodeWriter writer, int i, SLArgument arg)
		{
			arg.WriteAll (writer);
		}


		public static SLLine FunctionCallLine (SLIdentifier identifier, params SLArgument [] parameters)
		{
			return new SLLine (new SLFunctionCall (identifier,
				new DelegatedCommaListElemCollection<SLArgument> (WriteElement, parameters)));
		}

		public static SLLine FunctionCallLine (string identifier, params SLArgument [] parameters)
		{
			return new SLLine (new SLFunctionCall (new SLIdentifier (Exceptions.ThrowOnNull (identifier, "identifier")),
				new DelegatedCommaListElemCollection<SLArgument> (WriteElement, parameters)));
		}

	}
}

