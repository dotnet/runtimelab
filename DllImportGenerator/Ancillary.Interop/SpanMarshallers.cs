
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices.GeneratedMarshalling
{
    [GenericContiguousCollectionMarshaller]
    public unsafe ref struct ReadOnlySpanMarshaller<T>
    {
        private ReadOnlySpan<T> managedSpan;
        private readonly int sizeOfNativeElement;
        private IntPtr allocatedMemory;

        public ReadOnlySpanMarshaller(int sizeOfNativeElement)
            : this()
        {
            this.sizeOfNativeElement = sizeOfNativeElement;
        }

        public ReadOnlySpanMarshaller(ReadOnlySpan<T> managed, int sizeOfNativeElement)
        {
            allocatedMemory = default;
            this.sizeOfNativeElement = sizeOfNativeElement;
            if (managed.Length == 0)
            {
                managedSpan = default;
                NativeValueStorage = default;
                return;
            }
            managedSpan = managed;
            this.sizeOfNativeElement = sizeOfNativeElement;
            int spaceToAllocate = managed.Length * sizeOfNativeElement;
            allocatedMemory = Marshal.AllocCoTaskMem(managed.Length);
            NativeValueStorage = new Span<byte>((void*)allocatedMemory, spaceToAllocate);
        }

        public ReadOnlySpanMarshaller(ReadOnlySpan<T> managed, Span<byte> stackSpace, int sizeOfNativeElement)
        {
            allocatedMemory = default;
            this.sizeOfNativeElement = sizeOfNativeElement;
            if (managed.Length == 0)
            {
                managedSpan = default;
                NativeValueStorage = default;
                return;
            }
            managedSpan = managed;
            int spaceToAllocate = managed.Length * sizeOfNativeElement;
            if (spaceToAllocate < stackSpace.Length)
            {
                NativeValueStorage = stackSpace[0..spaceToAllocate];
            }
            else
            {
                allocatedMemory = Marshal.AllocCoTaskMem(spaceToAllocate);
                NativeValueStorage = new Span<byte>((void*)allocatedMemory, spaceToAllocate);
            }
        }

        /// <summary>
        /// Stack-alloc threshold set to 256 bytes to enable small arrays to be passed on the stack.
        /// Number kept small to ensure that P/Invokes with a lot of array parameters doesn't
        /// blow the stack since this is a new optimization in the code-generated interop.
        /// </summary>
        public const int StackBufferSize = 0x200;

        public Span<T> ManagedValues => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(managedSpan), managedSpan.Length);

        public Span<byte> NativeValueStorage { get; private set; }

        public ref byte GetPinnableReference() => ref MemoryMarshal.GetReference(NativeValueStorage);

        public void SetUnmarshalledCollectionLength(int length)
        {
            managedSpan = new T[length];
        }

        public byte* Value
        {
            get
            {
                Debug.Assert(managedSpan.IsEmpty || allocatedMemory != IntPtr.Zero);
                return (byte*)allocatedMemory;
            }
            set
            {
                if (value == null)
                {
                    managedSpan = null;
                    NativeValueStorage = default;
                }
                else
                {
                    allocatedMemory = (IntPtr)value;
                    NativeValueStorage = new Span<byte>(value, managedSpan.Length * sizeOfNativeElement);
                }
            }
        }

        public ReadOnlySpan<T> ToManaged() => managedSpan;

        public void FreeNative()
        {
            if (allocatedMemory != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(allocatedMemory);
            }
        }
    }

    [GenericContiguousCollectionMarshaller]
    public unsafe ref struct SpanMarshaller<T>
    {
        private ReadOnlySpanMarshaller<T> inner;

        public SpanMarshaller(int sizeOfNativeElement)
            : this()
        {
            inner = new ReadOnlySpanMarshaller<T>(sizeOfNativeElement);
        }

        public SpanMarshaller(Span<T> managed, int sizeOfNativeElement)
        {
            inner = new ReadOnlySpanMarshaller<T>(managed, sizeOfNativeElement);
        }

        public SpanMarshaller(Span<T> managed, Span<byte> stackSpace, int sizeOfNativeElement)
        {
            inner = new ReadOnlySpanMarshaller<T>(managed, stackSpace, sizeOfNativeElement);
        }

        /// <summary>
        /// Stack-alloc threshold set to 256 bytes to enable small arrays to be passed on the stack.
        /// Number kept small to ensure that P/Invokes with a lot of array parameters doesn't
        /// blow the stack since this is a new optimization in the code-generated interop.
        /// </summary>
        public const int StackBufferSize = ReadOnlySpanMarshaller<T>.StackBufferSize;

        public Span<T> ManagedValues => inner.ManagedValues;

        public Span<byte> NativeValueStorage
        {
            get => inner.NativeValueStorage;
        }

        public ref byte GetPinnableReference() => ref inner.GetPinnableReference();

        public void SetUnmarshalledCollectionLength(int length)
        {
            inner.SetUnmarshalledCollectionLength(length);
        }

        public byte* Value
        {
            get => inner.Value;
            set => inner.Value = value;
        }

        public Span<T> ToManaged()
        {
            ReadOnlySpan<T> managedInner = inner.ToManaged();
            return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(managedInner), managedInner.Length);
        }

        public void FreeNative()
        {
            inner.FreeNative();
        }
    }

    [GenericContiguousCollectionMarshaller]
    public unsafe ref struct NeverNullSpanMarshaller<T>
    {
        private SpanMarshaller<T> inner;

        public NeverNullSpanMarshaller(int sizeOfNativeElement)
            : this()
        {
            inner = new SpanMarshaller<T>(sizeOfNativeElement);
        }

        public NeverNullSpanMarshaller(Span<T> managed, int sizeOfNativeElement)
        {
            inner = new SpanMarshaller<T>(managed, sizeOfNativeElement);
        }

        public NeverNullSpanMarshaller(Span<T> managed, Span<byte> stackSpace, int sizeOfNativeElement)
        {
            inner = new SpanMarshaller<T>(managed, stackSpace, sizeOfNativeElement);
        }

        /// <summary>
        /// Stack-alloc threshold set to 256 bytes to enable small spans to be passed on the stack.
        /// Number kept small to ensure that P/Invokes with a lot of span parameters doesn't
        /// blow the stack.
        /// </summary>
        public const int StackBufferSize = SpanMarshaller<T>.StackBufferSize;

        public Span<T> ManagedValues => inner.ManagedValues;

        public Span<byte> NativeValueStorage
        {
            get => inner.NativeValueStorage;
        }

        public ref byte GetPinnableReference()
        {
            if (inner.ManagedValues.Length == 0)
            {
                return ref *(byte*)0x1;
            }
            return ref inner.GetPinnableReference();
        }

        public void SetUnmarshalledCollectionLength(int length)
        {
            inner.SetUnmarshalledCollectionLength(length);
        }

        public byte* Value
        {
            get
            {
                if (inner.ManagedValues.Length == 0)
                {
                    return (byte*)0x1;
                }
                return inner.Value;
            }

            set => inner.Value = value;
        }

        public Span<T> ToManaged() => inner.ToManaged();

        public void FreeNative()
        {
            inner.FreeNative();
        }
    }

    [GenericContiguousCollectionMarshaller]
    public unsafe ref struct NeverNullReadOnlySpanMarshaller<T>
    {
        private ReadOnlySpanMarshaller<T> inner;

        public NeverNullReadOnlySpanMarshaller(int sizeOfNativeElement)
            : this()
        {
            inner = new ReadOnlySpanMarshaller<T>(sizeOfNativeElement);
        }

        public NeverNullReadOnlySpanMarshaller(ReadOnlySpan<T> managed, int sizeOfNativeElement)
        {
            inner = new ReadOnlySpanMarshaller<T>(managed, sizeOfNativeElement);
        }

        public NeverNullReadOnlySpanMarshaller(ReadOnlySpan<T> managed, Span<byte> stackSpace, int sizeOfNativeElement)
        {
            inner = new ReadOnlySpanMarshaller<T>(managed, stackSpace, sizeOfNativeElement);
        }

        /// <summary>
        /// Stack-alloc threshold set to 256 bytes to enable small spans to be passed on the stack.
        /// Number kept small to ensure that P/Invokes with a lot of span parameters doesn't
        /// blow the stack.
        /// </summary>
        public const int StackBufferSize = SpanMarshaller<T>.StackBufferSize;

        public Span<T> ManagedValues => inner.ManagedValues;

        public Span<byte> NativeValueStorage
        {
            get => inner.NativeValueStorage;
        }

        public ref byte GetPinnableReference()
        {
            if (inner.ManagedValues.Length == 0)
            {
                return ref *(byte*)0x1;
            }
            return ref inner.GetPinnableReference();
        }

        public void SetUnmarshalledCollectionLength(int length)
        {
            inner.SetUnmarshalledCollectionLength(length);
        }

        public byte* Value
        {
            get
            {
                if (inner.ManagedValues.Length == 0)
                {
                    return (byte*)0x1;
                }
                return inner.Value;
            }

            set => inner.Value = value;
        }

        public ReadOnlySpan<T> ToManaged() => inner.ToManaged();

        public void FreeNative()
        {
            inner.FreeNative();
        }
    }

    [GenericContiguousCollectionMarshaller]
    public unsafe ref struct DirectSpanMarshaller<T>
        where T : unmanaged
    {
        private int unmarshalledLength;
        private T* allocatedMemory;
        private Span<T> data;

        public DirectSpanMarshaller(int sizeOfNativeElement)
            :this()
        {
            // This check is not exhaustive, but it will catch the majority of cases.
            if (typeof(T) == typeof(bool) || typeof(T) == typeof(char) || Unsafe.SizeOf<T>() != sizeOfNativeElement)
            {
                throw new ArgumentException("This marshaller only supports blittable element types. The provided type parameter must be blittable", nameof(T));
            }
        }

        public DirectSpanMarshaller(Span<T> managed, int sizeOfNativeElement)
            :this(sizeOfNativeElement)
        {
            if (managed.Length == 0)
            {
                return;
            }

            int spaceToAllocate = managed.Length * Unsafe.SizeOf<T>();
            allocatedMemory = (T*)Marshal.AllocCoTaskMem(spaceToAllocate);
            data = managed;
        }

        public DirectSpanMarshaller(Span<T> managed, Span<byte> stackSpace, int sizeOfNativeElement)
            :this(sizeOfNativeElement)
        {
            data = managed;
        }

        /// <summary>
        /// Stack-alloc threshold set to 0 so that the generator can use the constructor that takes a stackSpace to let the marshaller know that the original data span can be used and safely pinned.
        /// </summary>
        public const int StackBufferSize = 0;

        public Span<T> ManagedValues => data;

        public Span<byte> NativeValueStorage => allocatedMemory != null
            ? new Span<byte>(allocatedMemory, data.Length * Unsafe.SizeOf<T>())
            : MemoryMarshal.Cast<T, byte>(data);

        public ref T GetPinnableReference() => ref data.GetPinnableReference();

        public void SetUnmarshalledCollectionLength(int length)
        {
            unmarshalledLength = length;
        }

        public T* Value
        {
            get
            {
                Debug.Assert(data.IsEmpty || allocatedMemory != null);
                return allocatedMemory;
            }
            set
            {
                // We don't save the pointer assigned here to be freed
                // since this marshaller passes back the actual memory span from native code
                // back to managed code.
                allocatedMemory = null;
                data = new Span<T>(value, unmarshalledLength);
            }
        }

        public Span<T> ToManaged()
        {
            return data;
        }

        public void FreeNative()
        {
            if (allocatedMemory != null)
            {
                Marshal.FreeCoTaskMem((IntPtr)allocatedMemory);
            }
        }
    }
}