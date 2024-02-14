// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLCatch : DelegatedSimpleElement, ISLStatement {
		public SLCatch (ICodeElement catchExpr, SLCodeBlock body)
		{
			CatchExpr = catchExpr;
			Body = body ?? new SLCodeBlock (null);
		}

		public SLCatch ()
			: this ((ICodeElement)null, null)
		{
		}

		public SLCatch (string name, SLType typeMatch)
			: this (SLLetMatch.LetAs (name, typeMatch), null)
		{
		}

		public ICodeElement CatchExpr { get; private set; }
		public SLCodeBlock Body { get; private set; }
		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.BeginNewLine (true);
			writer.Write ("catch ", false);
			if (CatchExpr != null) {
				CatchExpr.WriteAll (writer);
			}
			writer.EndLine ();
			Body.WriteAll (writer);
			writer.EndLine ();
		}
	}

	public class SLDo : CodeElementCollection<ICodeElement>, ISLStatement {
		public SLDo (SLCodeBlock doBlock, params SLCatch [] catchBlocks)
		{
			DoBlock = doBlock ?? new SLCodeBlock (null);
			CatchBlocks = new CodeElementCollection<SLCatch> ();
			CatchBlocks.AddRange (catchBlocks);

			Add (new SimpleElement ("do ", true));
			Add (DoBlock);
			Add (CatchBlocks);
		}

		public SLDo (SLCodeBlock doBlock, string name, SLType catchType)
			: this (doBlock, new SLCatch (name, catchType))
		{
		}

		public SLCodeBlock DoBlock { get; private set; }
		public CodeElementCollection<SLCatch> CatchBlocks { get; private set; }

		public override void Write (ICodeWriter writer, object o)
		{
			writer.BeginNewLine (true);
			base.Write (writer, o);
		}
	}
}
