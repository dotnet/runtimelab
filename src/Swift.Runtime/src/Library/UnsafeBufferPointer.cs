// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Swift.Runtime
{
    // <summary>
    // Represents Swift UnsafeBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeBufferPointer<T> where T : unmanaged
    {
        private readonly T* _baseAddress;
        private readonly nint _count;
        public UnsafeBufferPointer(T* baseAddress, nint count)
        {
            _baseAddress = baseAddress;
            _count = count;
        }

        public T* BaseAddress => _baseAddress;
        public nint Count => _count;
    }

    // <summary>
    // Represents Swift UnsafeMutableBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeMutableBufferPointer<T> where T : unmanaged
    {
        private readonly T* _baseAddress;
        private readonly nint _count;
        public UnsafeMutableBufferPointer(T* baseAddress, nint count)
        {
            _baseAddress = baseAddress;
            _count = count;
        }

        public T* BaseAddress => _baseAddress;
        public nint Count => _count;
    }

    // <summary>
    // Represents Swift UnsafeRawBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeRawBufferPointer
    {
        private readonly void* _baseAddress;
        private readonly void* _end;
        public UnsafeRawBufferPointer(void* baseAddress, nint count)
        {
            _baseAddress = baseAddress;
            _end = (byte*)baseAddress + count;
        }

        public void* BaseAddress => _baseAddress;
        public nint Count => (nint)((byte*)_end - (byte*)_baseAddress);
    }

    // <summary>
    // Represents Swift UnsafeMutableRawBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeMutableRawBufferPointer
    {
        private readonly void* _baseAddress;
        private readonly void* _end;
        public UnsafeMutableRawBufferPointer(void* baseAddress, nint count)
        {
            _baseAddress = baseAddress;
            _end = (byte*)baseAddress + count;
        }

        public void* BaseAddress => _baseAddress;
        public nint Count => (nint)((byte*)_end - (byte*)_baseAddress);
    }
}
