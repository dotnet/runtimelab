// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Swift.Runtime;

/// <summary>
/// SwiftRuntimeException is thrown when an error is encountered in the runtime handling of Swift types,
/// marshaling or other runtime operations.
/// </summary>
public class SwiftRuntimeException : Exception {
    public SwiftRuntimeException(string message) : base(message) {
    }
}