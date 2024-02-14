// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLInject : SLIdentifier {
		// SLInject is a way to more formalize the notion of code that is just plain easier to
		// inject as raw text. It's not strictly necessary, but when you see a CSInject, it will make
		// it clear that you're doing something not quite on the up and up.
		public SLInject (string name)
			: base (name)
		{
		}

		public static explicit operator SLInject (string name)
		{
			return new SLInject (name);
		}
	}
}
