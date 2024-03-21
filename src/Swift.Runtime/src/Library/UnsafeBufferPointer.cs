// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Swift.Runtime
{
    // <summary>
    // Represents Swift UnsafeBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeBufferPointer<T>
    {
        private readonly void* _position;
        private readonly nint _count;
        public UnsafeBufferPointer(void* start, nint count)
        {
            _position = start;
            _count = count;
        }

        public void* BaseAddress => _position;
        public nint Count => _count;
    }

    // <summary>
    // Represents Swift UnsafeMutableBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeMutableBufferPointer<T>
    {
        private readonly void* _position;
        private readonly nint _count;
        public UnsafeMutableBufferPointer(void* start, nint count)
        {
            _position = start;
            _count = count;
        }

        public void* BaseAddress => _position;
        public nint Count => _count;
    }

    // <summary>
    // Represents Swift UnsafeRawBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeRawBufferPointer
    {
        private readonly void* _position;
        private readonly void* _end;
        public UnsafeRawBufferPointer(void* start, nint count)
        {
            _position = start;
            _end = (byte*)start + count;
        }

        public void* BaseAddress => _position;
        public nint Count => (nint)((byte*)_end - (byte*)_position);
    }

    // <summary>
    // Represents Swift UnsafeMutableRawBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeMutableRawBufferPointer
    {
        private readonly void* _position;
        private readonly void* _end;
        public UnsafeMutableRawBufferPointer(void* start, nint count)
        {
            _position = start;
            _end = (byte*)start + count;
        }

        public void* BaseAddress => _position;
        public nint Count => (nint)((byte*)_end - (byte*)_position);
    }
}
