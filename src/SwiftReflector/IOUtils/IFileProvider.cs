// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SwiftReflector.IOUtils
{
    public interface IFileProvider<T>
    {
        string ProvideFileFor(T thing);
        void NotifyFileDone(T thing, string stm);
    }
}

