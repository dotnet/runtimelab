// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace SyntaxDynamo.CSLang {
	public class CSCatch : DelegatedSimpleElement, ICSStatement {
		public CSCatch (CSType catchType, CSIdentifier name, CSCodeBlock body)
		{
			CatchType = catchType;
			Name = name;
			Body = body ?? new CSCodeBlock ();
		}

		public CSCatch (string catchType, string name, CSCodeBlock body)
			: this (new CSSimpleType (catchType), name != null ? new CSIdentifier (name) : null, body)
		{
		}

		public CSCatch (Type catchType, string name, CSCodeBlock body)
			: this (new CSSimpleType (catchType), name != null ? new CSIdentifier (name) : null, body)
		{
		}

		public CSCatch (CSCodeBlock body)
			: this ((CSType)null, null, body)
		{
		}

		public CSType CatchType { get; private set; }
		public CSIdentifier Name { get; private set; }
		public CSCodeBlock Body { get; private set; }
		protected override void LLWrite (ICodeWriter writer, object o)
		{

			writer.BeginNewLine (true);
			writer.Write ("catch ", false);
			if ((object)CatchType != null) {
				writer.Write ("(", false);
				CatchType.WriteAll (writer);
				if ((object)Name != null) {
					SimpleElement.Spacer.WriteAll (writer);
					Name.WriteAll (writer);
				}
				writer.Write (")", false);
			}
			writer.EndLine ();
			Body.WriteAll (writer);
			writer.EndLine ();
		}
	}

	public class CSTryCatch : CodeElementCollection<ICodeElement>, ICSStatement {
		public CSTryCatch (CSCodeBlock tryBlock, params CSCatch [] catchBlocks)
		{
			TryBlock = tryBlock ?? new CSCodeBlock ();
			CatchBlocks = new CodeElementCollection<CSCatch> ();
			CatchBlocks.AddRange (catchBlocks);

			Add (new SimpleElement ("try ", true));
			Add (TryBlock);
			Add (CatchBlocks);
		}

		public CSTryCatch (CSCodeBlock tryBlock, Type catchType, string name, CSCodeBlock catchBlock)
			: this (tryBlock, new CSCatch (catchType, name, catchBlock))
		{
		}

		public override void Write (ICodeWriter writer, object o)
		{
			writer.BeginNewLine (true);
			base.Write (writer, o);
		}

		public CSCodeBlock TryBlock { get; private set; }
		public CodeElementCollection<CSCatch> CatchBlocks { get; private set; }
	}
}
