// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo {
	public class Exceptions {
		public static T ThrowOnNull<T> (T o, string name, string message = null) where T : class
		{
			name = name ?? "::no name supplied::";
			if (o == null) {
				if (message == null)
					throw new ArgumentNullException (name);
				else
					throw new ArgumentNullException (name, message);
			}
			return o;
		}
	}
}

