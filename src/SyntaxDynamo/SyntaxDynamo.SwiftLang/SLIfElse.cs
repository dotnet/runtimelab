// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.SwiftLang {
	public class SLIfElse : CodeElementCollection<ICodeElement>, ISLStatement {
		class IfElem : DelegatedSimpleElement, ISLStatement {
			public IfElem (SLBaseExpr condition, bool isCase)
				: base ()
			{
				Condition = condition;
				IsCase = isCase;
			}

			protected override void LLWrite (ICodeWriter writer, object o)
			{
				writer.BeginNewLine (true);
				writer.Write ("if ", false);
				if (IsCase)
					writer.Write ("case ", true);
				Condition.WriteAll (writer);
				writer.EndLine ();
			}

			public bool IsCase { get; private set; }
			public SLBaseExpr Condition { get; private set; }
		}

		public SLIfElse (SLBaseExpr condition, SLCodeBlock ifClause, SLCodeBlock elseClause = null, bool isCase = false)
			: base ()
		{
			Condition = new IfElem (Exceptions.ThrowOnNull (condition, nameof(condition)), isCase);
			IfClause = Exceptions.ThrowOnNull (ifClause, nameof(ifClause));
			ElseClause = elseClause;

			Add (Condition);
			Add (IfClause);
			if (ElseClause != null && ElseClause.Count > 0) {
				Add (new SimpleLineElement ("else", false, true, false));
				Add (ElseClause);
			}
		}

		public SLIfElse (SLBaseExpr expr, IEnumerable<ICodeElement> ifClause, IEnumerable<ICodeElement> elseClause, bool isCase)
			: this (expr, new SLCodeBlock (ifClause),
				elseClause != null ? new SLCodeBlock (elseClause) : null, isCase)

		{

		}

		public DelegatedSimpleElement Condition { get; private set; }
		public SLCodeBlock IfClause { get; private set; }
		public SLCodeBlock ElseClause { get; private set; }


	}
}

