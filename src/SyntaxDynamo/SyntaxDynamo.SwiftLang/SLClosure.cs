// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLClosure : SLBaseExpr {
		public SLClosure (SLType type, SLTupleType parms, CodeElementCollection<ICodeElement> body, bool throws)
		{
			OuterBlock = new SLCodeBlock (null);
			Parameters = parms;
			if (parms != null) {
				OuterBlock.Add (parms);
				OuterBlock.Add (SimpleElement.Spacer);
				if (throws) {
					OuterBlock.Add (new SimpleElement ("throws "));
				}
				if (type != null) {
					Type = type;
					OuterBlock.Add (new SimpleElement ("-> "));
					OuterBlock.Add (Type);
					OuterBlock.Add (SimpleElement.Spacer);
				}
				OuterBlock.Add (new SimpleElement ("in "));
				OuterBlock.Add (body);
			}
		}

		public SLTupleType Parameters { get; private set; }
		public SLCodeBlock OuterBlock { get; private set; }
		public CodeElementCollection<ICodeElement> Body { get; private set; }
		public SLType Type { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			OuterBlock.WriteAll (writer);
		}
	}

}

