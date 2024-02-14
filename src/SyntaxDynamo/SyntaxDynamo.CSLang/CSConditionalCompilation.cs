// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang {
	public class CSConditionalCompilation : LineCodeElementCollection<ICodeElement> {
		CSConditionalCompilation (CSIdentifier tag, CSIdentifier condition)
			: base (true, false, false)
		{
			Add (tag);
			if ((object)condition != null) {
				Add (SimpleElement.Spacer);
				Add (condition);
			}
		}


		static CSConditionalCompilation _else = new CSConditionalCompilation (new CSIdentifier ("#else"), null);
		public static CSConditionalCompilation Else { get { return _else; } }
		static CSConditionalCompilation _endif = new CSConditionalCompilation (new CSIdentifier ("#endif"), null);
		public static CSConditionalCompilation Endif { get { return _endif; } }

		public static CSConditionalCompilation If (CSIdentifier condition)
		{
			return new CSConditionalCompilation (new CSIdentifier ("#if"), Exceptions.ThrowOnNull (condition, nameof (condition)));
		}

		public static void ProtectWithIfEndif (CSIdentifier condition, ICodeElement elem)
		{
			var @if = If (condition);
			@if.AttachBefore (elem);
			Endif.AttachAfter (elem);
		}
	}
}
