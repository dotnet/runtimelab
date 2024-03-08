// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Swift.Runtime
{
    /// <summary>
    /// Represents an instance of a Swift class object.
    /// </summary>
    public unsafe interface ISwiftObject
    {
        /// <summary>
        /// Gets the handle to the Swift object instance.
        /// </summary>
        public void* SwiftObject { get; }
    }
}
