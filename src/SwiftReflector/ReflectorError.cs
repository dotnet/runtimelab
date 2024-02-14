// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using SwiftReflector.ExceptionTools;
using System.Linq;

namespace SwiftReflector {

	// Range of error messages
	// 0000 - total abject failure
	// 0500 - should never, ever happen. Ever. (skipped case, argument null, argument out of range)
	// 1000 - compiler error, generating C#
	// 1500 - compiler error, C# trying to reference swift wrapper
	// 2500 - importing types from C# bindings
	// 2000 - wrapping error, wrapping swift code in C# callable wrappers
	// 3000 - decomposition error, demangling swift symbols
	// 4000 - error dropping swift symbols into inventory
	// 5000 - error reflecting on swift module
	// 6000 - error parsing TypeSpec
	// 7000 - error mapping one type to another


	public class ReflectorError {
		public const int kCantHappenBase = 500;
		public const int kCompilerBase = 1000;
		public const int kCompilerReferenceBase = 1500;
		public const int kWrappingBase = 2000;
		public const int kImportingBase = 2500;
		public const int kDecomposeBase = 3000;
		public const int kInventoryBase = 4000;
		public const int kReflectionErrorBase = 5000;
		public const int kTypeParseBase = 6000;
		public const int kTypeMapBase = 7000;

		public ReflectorError (Exception e)
		{
			if (e is RuntimeException re) {
				Exception = re;
				Error = re.Error;
			} else if (e is AggregateException ae && ae.InnerExceptions.All ((v) => v is RuntimeException)) {
				Exception = ae;
				// If any of the exceptions is an error, the whole collection is an error as well.
				Error = ae.InnerExceptions.Cast<RuntimeException> ().Any ((v) => v.Error);
			} else {
				Exception = new RuntimeException (kCantHappenBase + 54, error: true, innerException: e, message: e.Message);
				Error = true;
			}
		}

		public Exception Exception { get; private set; }
		public bool Error { get; private set; }

		public bool IsWarning => !Error;
		public string Message => Exception.Message;
	}
}
