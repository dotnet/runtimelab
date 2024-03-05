// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Swift.Runtime
{
    public unsafe interface ISwiftObject : IDisposable
    {
        void* SwiftObject { get; }
    }
}
