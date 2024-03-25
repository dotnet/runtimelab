// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Swift.Runtime
{
    // <summary>
    // Represents Swift UnsafePointer in C#.
    // </summary>
    public unsafe readonly struct UnsafePointer<T> where T : unmanaged
    {
        private readonly T* _pointee;
        public UnsafePointer(T* pointee)
        {
            this._pointee = pointee;
        }

        public T* Pointee => _pointee;

        public static implicit operator T*(UnsafePointer<T> pointer) => pointer.Pointee;

        public static implicit operator UnsafePointer<T>(T* pointee) => new UnsafePointer<T>(pointee);
    }

    // <summary>
    // Represents Swift UnsafeMutablePointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeMutablePointer<T> where T : unmanaged
    {
        private readonly T* _pointee;
        public UnsafeMutablePointer(T* pointee)
        {
            _pointee = pointee;
        }

        public T* Pointee => _pointee;

        public static implicit operator T*(UnsafeMutablePointer<T> pointer) => pointer.Pointee;

        public static implicit operator UnsafeMutablePointer<T>(T* pointee) => new UnsafeMutablePointer<T>(pointee);
    }

    // <summary>
    // Represents Swift UnsafeRawPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeRawPointer
    {
        private readonly void* _pointee;
        public UnsafeRawPointer(void* pointee)
        {
            _pointee = pointee;
        }

        public void* Pointee => _pointee;

        public static implicit operator void*(UnsafeRawPointer pointer) => pointer.Pointee;

        public static implicit operator UnsafeRawPointer(void* pointee) => new UnsafeRawPointer(pointee);
    }

    // <summary>
    // Represents Swift UnsafeMutableRawPointer in C#.
    // </summary>
    public unsafe readonly struct UnsafeMutableRawPointer
    {
        private readonly void* _pointee;
        public UnsafeMutableRawPointer(void* pointee)
        {
            _pointee = pointee;
        }

        public void* Pointee => _pointee;

        public static implicit operator void*(UnsafeMutableRawPointer pointer) => pointer.Pointee;

        public static implicit operator UnsafeMutableRawPointer(void* pointee) => new UnsafeMutableRawPointer(pointee);
    }
}
