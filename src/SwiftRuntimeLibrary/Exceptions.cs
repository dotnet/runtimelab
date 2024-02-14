// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SwiftRuntimeLibrary {
	public static class Exceptions {
		public static T ThrowOnNull<T>(T o, string name, string message = null) where T : class
		{
			if (o == null)
				ThrowArgumentNull(name, message);
			return o!;
		}

		static void ThrowArgumentNull(string name, string message = null)
		{
			name = name ?? "::no name supplied::";
			if (message == null)
				throw new ArgumentNullException(name);
			else
				throw new ArgumentNullException(name, message);
		}
	}
}
