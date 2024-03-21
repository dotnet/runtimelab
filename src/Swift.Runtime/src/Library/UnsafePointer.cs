// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Swift.Runtime
{
    // <summary>
    // Represents Swift UnsafePointer in C#.
    // </summary>
    public unsafe readonly struct UnsafePointer<T>
    {
        private readonly void* _rawValue;
        public UnsafePointer(void* _rawValue)
        {
            this._rawValue = _rawValue;
        }

        public void* Pointee => _rawValue;
    }

    // <summary>
    // Represents Swift UnsafeMutablePointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeMutablePointer<T>
    {
        private readonly void* _rawValue;
        public UnsafeMutablePointer(void* _rawValue)
        {
            this._rawValue = _rawValue;
        }

        public void* Pointee => _rawValue;
    }

    // <summary>
    // Represents Swift UnsafeRawPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeRawPointer
    {
        private readonly void* _rawValue;
        public UnsafeRawPointer(void* _rawValue)
        {
            this._rawValue = _rawValue;
        }

        public void* Pointee => _rawValue;
    }

    // <summary>
    // Represents Swift UnsafeMutableRawPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeMutableRawPointer
    {
        private readonly void* _rawValue;
        public UnsafeMutableRawPointer(void* _rawValue)
        {
            this._rawValue = _rawValue;
        }

        public void* Pointee => _rawValue;
    }
}
