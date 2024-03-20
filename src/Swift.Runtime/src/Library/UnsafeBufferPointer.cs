// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Swift.Runtime
{
    // <summary>
    // Represents Swift UnsafeRawBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeRawBufferPointer
    {
        private readonly void* _Position;
        private readonly void* _End;
        public UnsafeRawBufferPointer(void* start, nint count)
        {
            _Position = start;
            _End = (byte*)start + count;
        }

        public void* BaseAddress => _Position;
        public nint Count => (nint)((byte*)_End - (byte*)_Position);
    }


    // <summary>
    // Represents Swift UnsafeMutableRawBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeMutableRawBufferPointer
    {
        private readonly void* _Position;
        private readonly void* _End;
        public UnsafeMutableRawBufferPointer(void* start, nint count)
        {
            _Position = start;
            _End = (byte*)start + count;
        }

        public void* BaseAddress => _Position;
        public nint Count => (nint)((byte*)_End - (byte*)_Position);
    }

    // <summary>
    // Represents Swift UnsafeBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeBufferPointer
    {
        private readonly void* _Position;
        public readonly nint Count;
        public UnsafeBufferPointer(void* start, nint count)
        {
            _Position = start;
            Count = count;
        }

        public void* BaseAddress => _Position;
    }

    // <summary>
    // Represents Swift UnsafeMutableBufferPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeMutableBufferPointer
    {
        private readonly void* _Position;
        public readonly nint Count;
        public UnsafeMutableBufferPointer(void* start, nint count)
        {
            _Position = start;
            Count = count;
        }

        public void* BaseAddress => _Position;
    }
}
