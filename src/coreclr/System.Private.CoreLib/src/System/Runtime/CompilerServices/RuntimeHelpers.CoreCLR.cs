// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace System.Runtime.CompilerServices
{
    internal struct ExecutionAndSyncBlockStore
    {
        // Store current ExecutionContext and SynchronizationContext as "previousXxx".
        // This allows us to restore them and undo any Context changes made in stateMachine.MoveNext
        // so that they won't "leak" out of the first await.
        public ExecutionContext? _previousExecutionCtx;
        public SynchronizationContext? _previousSyncCtx;
        public Thread _thread;

        public void Push()
        {
            _thread = Thread.CurrentThread;
            _previousExecutionCtx = _thread._executionContext;
            _previousSyncCtx = _thread._synchronizationContext;
        }

        public void Pop()
        {
            // The common case is that these have not changed, so avoid the cost of a write barrier if not needed.
            if (_previousSyncCtx != _thread._synchronizationContext)
            {
                // Restore changed SynchronizationContext back to previous
                _thread._synchronizationContext = _previousSyncCtx;
            }

            ExecutionContext? currentExecutionCtx = _thread._executionContext;
            if (_previousExecutionCtx != currentExecutionCtx)
            {
                ExecutionContext.RestoreChangedContextToThread(_thread, _previousExecutionCtx, currentExecutionCtx);
            }
        }
    }

    public static partial class RuntimeHelpers
    {
        [Intrinsic]
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void InitializeArray(Array array, RuntimeFieldHandle fldHandle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern unsafe void* GetSpanDataFrom(
            RuntimeFieldHandle fldHandle,
            RuntimeTypeHandle targetTypeHandle,
            out int count);

        // GetObjectValue is intended to allow value classes to be manipulated as 'Object'
        // but have aliasing behavior of a value class.  The intent is that you would use
        // this function just before an assignment to a variable of type 'Object'.  If the
        // value being assigned is a mutable value class, then a shallow copy is returned
        // (because value classes have copy semantics), but otherwise the object itself
        // is returned.
        //
        // Note: VB calls this method when they're about to assign to an Object
        // or pass it as a parameter.  The goal is to make sure that boxed
        // value types work identical to unboxed value types - ie, they get
        // cloned when you pass them around, and are always passed by value.
        // Of course, reference types are not cloned.
        //
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: NotNullIfNotNull(nameof(obj))]
        public static extern object? GetObjectValue(object? obj);

        // RunClassConstructor causes the class constructor for the given type to be triggered
        // in the current domain.  After this call returns, the class constructor is guaranteed to
        // have at least been started by some thread.  In the absence of class constructor
        // deadlock conditions, the call is further guaranteed to have completed.
        //
        // This call will generate an exception if the specified class constructor threw an
        // exception when it ran.

        [LibraryImport(QCall, EntryPoint = "ReflectionInvocation_RunClassConstructor")]
        private static partial void RunClassConstructor(QCallTypeHandle type);

        [RequiresUnreferencedCode("Trimmer can't guarantee existence of class constructor")]
        public static void RunClassConstructor(RuntimeTypeHandle type)
        {
            RuntimeType rt = type.GetRuntimeType();
            if (rt is null)
                throw new ArgumentException(SR.InvalidOperation_HandleIsNotInitialized, nameof(type));

            RunClassConstructor(new QCallTypeHandle(ref rt));
        }

        // RunModuleConstructor causes the module constructor for the given type to be triggered
        // in the current domain.  After this call returns, the module constructor is guaranteed to
        // have at least been started by some thread.  In the absence of module constructor
        // deadlock conditions, the call is further guaranteed to have completed.
        //
        // This call will generate an exception if the specified module constructor threw an
        // exception when it ran.

        [LibraryImport(QCall, EntryPoint = "ReflectionInvocation_RunModuleConstructor")]
        private static partial void RunModuleConstructor(QCallModule module);

        public static void RunModuleConstructor(ModuleHandle module)
        {
            RuntimeModule rm = module.GetRuntimeModule();
            if (rm is null)
                throw new ArgumentException(SR.InvalidOperation_HandleIsNotInitialized, nameof(module));

            RunModuleConstructor(new QCallModule(ref rm));
        }

        [LibraryImport(QCall, EntryPoint = "ReflectionInvocation_CompileMethod")]
        internal static partial void CompileMethod(RuntimeMethodHandleInternal method);

        [LibraryImport(QCall, EntryPoint = "ReflectionInvocation_PrepareMethod")]
        private static unsafe partial void PrepareMethod(RuntimeMethodHandleInternal method, IntPtr* pInstantiation, int cInstantiation);

        public static void PrepareMethod(RuntimeMethodHandle method) => PrepareMethod(method, null);

        public static unsafe void PrepareMethod(RuntimeMethodHandle method, RuntimeTypeHandle[]? instantiation)
        {
            IRuntimeMethodInfo methodInfo = method.GetMethodInfo();
            if (methodInfo == null)
                throw new ArgumentException(SR.InvalidOperation_HandleIsNotInitialized, nameof(method));

            // defensive copy of user-provided array, per CopyRuntimeTypeHandles contract
            instantiation = (RuntimeTypeHandle[]?)instantiation?.Clone();

            ReadOnlySpan<IntPtr> instantiationHandles = RuntimeTypeHandle.CopyRuntimeTypeHandles(instantiation, stackScratch: stackalloc IntPtr[8]);
            fixed (IntPtr* pInstantiation = instantiationHandles)
            {
                PrepareMethod(methodInfo.Value, pInstantiation, instantiationHandles.Length);
                GC.KeepAlive(instantiation);
                GC.KeepAlive(methodInfo);
            }
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void PrepareDelegate(Delegate d);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int GetHashCode(object? o);

        /// <summary>
        /// If a hash code has been assigned to the object, it is returned. Otherwise zero is
        /// returned.
        /// </summary>
        /// <remarks>
        /// The advantage of this over <see cref="GetHashCode" /> is that it avoids assigning a hash
        /// code to the object if it does not already have one.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern int TryGetHashCode(object o);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern new bool Equals(object? o1, object? o2);

        [Obsolete("OffsetToStringData has been deprecated. Use string.GetPinnableReference() instead.")]
        public static int OffsetToStringData
        {
            // This offset is baked in by string indexer intrinsic, so there is no harm
            // in getting it baked in here as well.
            [NonVersionable]
            get =>
                // Number of bytes from the address pointed to by a reference to
                // a String to the first 16-bit character in the String.  Skip
                // over the MethodTable pointer, & String
                // length.  Of course, the String reference points to the memory
                // after the sync block, so don't count that.
                // This property allows C#'s fixed statement to work on Strings.
                // On 64 bit platforms, this should be 12 (8+4) and on 32 bit 8 (4+4).
#if TARGET_64BIT
                12;
#else // 32
                8;
#endif // TARGET_64BIT

        }

        // This method ensures that there is sufficient stack to execute the average Framework function.
        // If there is not enough stack, then it throws System.InsufficientExecutionStackException.
        // Note: this method is not part of the CER support, and is not to be confused with ProbeForSufficientStack.
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void EnsureSufficientExecutionStack();

        // This method ensures that there is sufficient stack to execute the average Framework function.
        // If there is not enough stack, then it return false.
        // Note: this method is not part of the CER support, and is not to be confused with ProbeForSufficientStack.
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool TryEnsureSufficientExecutionStack();

        public static object GetUninitializedObject(
            // This API doesn't call any constructors, but the type needs to be seen as constructed.
            // A type is seen as constructed if a constructor is kept.
            // This obviously won't cover a type with no constructor. Reference types with no
            // constructor are an academic problem. Valuetypes with no constructors are a problem,
            // but IL Linker currently treats them as always implicitly boxed.
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
            Type type)
        {
            if (type is not RuntimeType rt)
            {
                ArgumentNullException.ThrowIfNull(type);
                throw new SerializationException(SR.Format(SR.Serialization_InvalidType, type));
            }

            object? obj = null;
            GetUninitializedObject(new QCallTypeHandle(ref rt), ObjectHandleOnStack.Create(ref obj));
            return obj!;
        }

        [LibraryImport(QCall, EntryPoint = "ReflectionSerialization_GetUninitializedObject")]
        private static partial void GetUninitializedObject(QCallTypeHandle type, ObjectHandleOnStack retObject);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern object AllocateUninitializedClone(object obj);

        /// <returns>true if given type is reference type or value type that contains references</returns>
        [Intrinsic]
        public static bool IsReferenceOrContainsReferences<T>()
        {
            // The body of this function will be replaced by the EE with unsafe code!!!
            // See getILIntrinsicImplementationForRuntimeHelpers for how this happens.
            throw new InvalidOperationException();
        }

        /// <returns>true if given type is bitwise equatable (memcmp can be used for equality checking)</returns>
        /// <remarks>
        /// Only use the result of this for Equals() comparison, not for CompareTo() comparison.
        /// </remarks>
        [Intrinsic]
        internal static bool IsBitwiseEquatable<T>()
        {
            // The body of this function will be replaced by the EE with unsafe code!!!
            // See getILIntrinsicImplementationForRuntimeHelpers for how this happens.
            throw new InvalidOperationException();
        }

        [Intrinsic]
        internal static bool EnumEquals<T>(T x, T y) where T : struct, Enum
        {
            // The body of this function will be replaced by the EE with unsafe code
            // See getILIntrinsicImplementation for how this happens.
            return x.Equals(y);
        }

        [Intrinsic]
        internal static int EnumCompareTo<T>(T x, T y) where T : struct, Enum
        {
            // The body of this function will be replaced by the EE with unsafe code
            // See getILIntrinsicImplementation for how this happens.
            return x.CompareTo(y);
        }

        internal static ref byte GetRawData(this object obj) =>
            ref Unsafe.As<RawData>(obj).Data;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe nuint GetRawObjectDataSize(object obj)
        {
            MethodTable* pMT = GetMethodTable(obj);

            // See comment on RawArrayData for details
            nuint rawSize = pMT->BaseSize - (nuint)(2 * sizeof(IntPtr));
            if (pMT->HasComponentSize)
                rawSize += (uint)Unsafe.As<RawArrayData>(obj).Length * (nuint)pMT->ComponentSize;

            GC.KeepAlive(obj); // Keep MethodTable alive

            return rawSize;
        }

        // Returns array element size.
        // Callers are required to keep obj alive
        internal static unsafe ushort GetElementSize(this Array array)
        {
            Debug.Assert(ObjectHasComponentSize(array));
            return GetMethodTable(array)->ComponentSize;
        }

        // Returns pointer to the multi-dimensional array bounds.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ref int GetMultiDimensionalArrayBounds(Array array)
        {
            Debug.Assert(GetMultiDimensionalArrayRank(array) > 0);
            // See comment on RawArrayData for details
            return ref Unsafe.As<byte, int>(ref Unsafe.As<RawArrayData>(array).Data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe int GetMultiDimensionalArrayRank(Array array)
        {
            int rank = GetMethodTable(array)->MultiDimensionalArrayRank;
            GC.KeepAlive(array); // Keep MethodTable alive
            return rank;
        }

        // Returns true iff the object has a component size;
        // i.e., is variable length like System.String or Array.
        // Callers are required to keep obj alive
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe bool ObjectHasComponentSize(object obj)
        {
            return GetMethodTable(obj)->HasComponentSize;
        }

        /// <summary>
        /// Boxes a given value using an input <see cref="MethodTable"/> to determine its type.
        /// </summary>
        /// <param name="methodTable">The <see cref="MethodTable"/> pointer to use to create the boxed instance.</param>
        /// <param name="data">A reference to the data to box.</param>
        /// <returns>A boxed instance of the value at <paramref name="data"/>.</returns>
        /// <remarks>This method includes proper handling for nullable value types as well.</remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern unsafe object? Box(MethodTable* methodTable, ref byte data);

        // Given an object reference, returns its MethodTable*.
        //
        // WARNING: The caller has to ensure that MethodTable* does not get unloaded. The most robust way
        // to achieve this is by using GC.KeepAlive on the object that the MethodTable* was fetched from, e.g.:
        //
        // MethodTable* pMT = GetMethodTable(o);
        //
        // ... work with pMT ...
        //
        // GC.KeepAlive(o);
        //
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Intrinsic]
        internal static unsafe MethodTable* GetMethodTable(object obj)
        {
            // The body of this function will be replaced by the EE with unsafe code
            // See getILIntrinsicImplementationForRuntimeHelpers for how this happens.

            return (MethodTable *)Unsafe.Add(ref Unsafe.As<byte, IntPtr>(ref obj.GetRawData()), -1);
        }


        [LibraryImport(QCall, EntryPoint = "MethodTable_AreTypesEquivalent")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool AreTypesEquivalent(MethodTable* pMTa, MethodTable* pMTb);

        /// <summary>
        /// Allocate memory that is associated with the <paramref name="type"/> and
        /// will be freed if and when the <see cref="Type"/> is unloaded.
        /// </summary>
        /// <param name="type">Type associated with the allocated memory.</param>
        /// <param name="size">Amount of memory in bytes to allocate.</param>
        /// <returns>The allocated memory</returns>
        public static IntPtr AllocateTypeAssociatedMemory(Type type, int size)
        {
            if (type is not RuntimeType rt)
                throw new ArgumentException(SR.Arg_MustBeType, nameof(type));

            ArgumentOutOfRangeException.ThrowIfNegative(size);

            return AllocateTypeAssociatedMemory(new QCallTypeHandle(ref rt), (uint)size);
        }

        [LibraryImport(QCall, EntryPoint = "RuntimeTypeHandle_AllocateTypeAssociatedMemory")]
        private static partial IntPtr AllocateTypeAssociatedMemory(QCallTypeHandle type, uint size);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern IntPtr AllocTailCallArgBuffer(int size, IntPtr gcDesc);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern unsafe TailCallTls* GetTailCallInfo(IntPtr retAddrSlot, IntPtr* retAddr);

        [StackTraceHidden]
        private static unsafe void DispatchTailCalls(
            IntPtr callersRetAddrSlot,
            delegate*<IntPtr, ref byte, PortableTailCallFrame*, void> callTarget,
            ref byte retVal)
        {
            IntPtr callersRetAddr;
            TailCallTls* tls = GetTailCallInfo(callersRetAddrSlot, &callersRetAddr);
            PortableTailCallFrame* prevFrame = tls->Frame;
            if (callersRetAddr == prevFrame->TailCallAwareReturnAddress)
            {
                prevFrame->NextCall = callTarget;
                return;
            }

            PortableTailCallFrame newFrame;
            // GC uses NextCall to keep LoaderAllocator alive after we link it below,
            // so we must null it out before that.
            newFrame.NextCall = null;

            try
            {
                tls->Frame = &newFrame;

                do
                {
                    callTarget(tls->ArgBuffer, ref retVal, &newFrame);
                    callTarget = newFrame.NextCall;
                } while (callTarget != null);
            }
            finally
            {
                tls->Frame = prevFrame;

                // If the arg buffer is reporting inst argument, it is safe to abandon it now
                if (tls->ArgBuffer != IntPtr.Zero && *(int*)tls->ArgBuffer == 1 /* TAILCALLARGBUFFER_INSTARG_ONLY */)
                {
                    *(int*)tls->ArgBuffer = 2 /* TAILCALLARGBUFFER_ABANDONED */;
                }
            }
        }

        private static Continuation AllocContinuation(Continuation prevContinuation, nuint numGCRefs, nuint dataSize)
        {
            Continuation newContinuation = new Continuation { Data = new byte[dataSize], GCData = new object[numGCRefs] };
            prevContinuation.Next = newContinuation;
            return newContinuation;
        }

        private struct RuntimeAsyncAwaitState
        {
            public Continuation? SentinelContinuation;
            public INotifyCompletion? Notifier;
        }

        [ThreadStatic]
        private static RuntimeAsyncAwaitState t_runtimeAsyncAwaitState;

        [Intrinsic]
        private static void SuspendAsync2(Continuation continuation) => throw new UnreachableException();

        private struct AwaitableProxy : ICriticalNotifyCompletion
        {
            private readonly INotifyCompletion _notifier;

            public AwaitableProxy(INotifyCompletion notifier)
            {
                _notifier = notifier;
            }

            public bool IsCompleted => false;

            public void OnCompleted(Action action)
            {
                _notifier!.OnCompleted(action);
            }

            public AwaitableProxy GetAwaiter() { return this; }

            public void UnsafeOnCompleted(Action action)
            {
                if (_notifier is ICriticalNotifyCompletion criticalNotification)
                {
                    criticalNotification.UnsafeOnCompleted(action);
                }
                else
                {
                    _notifier!.OnCompleted(action);
                }
            }

            public void GetResult() {}
        }

        private static Continuation UnlinkHeadContinuation(out AwaitableProxy awaitableProxy)
        {
            ref RuntimeAsyncAwaitState state = ref t_runtimeAsyncAwaitState;
            awaitableProxy = new AwaitableProxy(state.Notifier!);
            state.Notifier = null;

            Continuation sentinelContinuation = state.SentinelContinuation!;
            Continuation head = sentinelContinuation.Next!;
            sentinelContinuation.Next = null;
            return head;
        }

        private static async Task<T> FinalizeTaskReturningThunk<T>(Continuation continuation)
        {
            Continuation finalContinuation = new Continuation();

            // Note that the exact location the return value is placed is tied
            // into getAsyncResumptionStub in the VM, so do not change this
            // without also changing that code (and the JIT).
            if (IsReferenceOrContainsReferences<T>())
            {
                finalContinuation.Flags = CorInfoContinuationFlags.CORINFO_CONTINUATION_RESULT_IN_GCDATA | CorInfoContinuationFlags.CORINFO_CONTINUATION_NEEDS_EXCEPTION;
                finalContinuation.GCData = new object[1];
            }
            else
            {
                finalContinuation.Flags = CorInfoContinuationFlags.CORINFO_CONTINUATION_NEEDS_EXCEPTION;
                finalContinuation.Data = new byte[Unsafe.SizeOf<T>()];
            }

            continuation.Next = finalContinuation;

            while (true)
            {
                Continuation headContinuation = UnlinkHeadContinuation(out var awaitableProxy);
                await awaitableProxy;
                Continuation? finalResult = DispatchContinuations(headContinuation);
                if (finalResult != null)
                {
                    Debug.Assert(finalResult == finalContinuation);
                    if (IsReferenceOrContainsReferences<T>())
                    {
                        return (T)finalResult.GCData![0];
                    }
                    else
                    {
                        return Unsafe.As<byte, T>(ref finalResult.Data![0]);
                    }
                }
            }
        }

        private static async Task FinalizeTaskReturningThunk(Continuation continuation)
        {
            Continuation finalContinuation = new Continuation
            {
                Flags = CorInfoContinuationFlags.CORINFO_CONTINUATION_NEEDS_EXCEPTION,
            };
            continuation.Next = finalContinuation;

            while (true)
            {
                Continuation headContinuation = UnlinkHeadContinuation(out var awaitableProxy);
                await awaitableProxy;
                Continuation? finalResult = DispatchContinuations(headContinuation);
                if (finalResult != null)
                {
                    Debug.Assert(finalResult == finalContinuation);
                    return;
                }
            }
        }

        private static async ValueTask<T> FinalizeValueTaskReturningThunk<T>(Continuation continuation)
        {
            Continuation finalContinuation = new Continuation();

            // Note that the exact location the return value is placed is tied
            // into getAsyncResumptionStub in the VM, so do not change this
            // without also changing that code (and the JIT).
            if (IsReferenceOrContainsReferences<T>())
            {
                finalContinuation.Flags = CorInfoContinuationFlags.CORINFO_CONTINUATION_RESULT_IN_GCDATA | CorInfoContinuationFlags.CORINFO_CONTINUATION_NEEDS_EXCEPTION;
                finalContinuation.GCData = new object[1];
            }
            else
            {
                finalContinuation.Flags = CorInfoContinuationFlags.CORINFO_CONTINUATION_NEEDS_EXCEPTION;
                finalContinuation.Data = new byte[Unsafe.SizeOf<T>()];
            }

            continuation.Next = finalContinuation;

            while (true)
            {
                Continuation headContinuation = UnlinkHeadContinuation(out var awaitableProxy);
                await awaitableProxy;
                Continuation? finalResult = DispatchContinuations(headContinuation);
                if (finalResult != null)
                {
                    Debug.Assert(finalResult == finalContinuation);
                    if (IsReferenceOrContainsReferences<T>())
                    {
                        return (T)finalResult.GCData![0];
                    }
                    else
                    {
                        return Unsafe.As<byte, T>(ref finalResult.Data![0]);
                    }
                }
            }
        }

        private static async ValueTask FinalizeValueTaskReturningThunk(Continuation continuation)
        {
            Continuation finalContinuation = new Continuation
            {
                Flags = CorInfoContinuationFlags.CORINFO_CONTINUATION_NEEDS_EXCEPTION,
            };
            continuation.Next = finalContinuation;

            while (true)
            {
                Continuation headContinuation = UnlinkHeadContinuation(out var awaitableProxy);
                await awaitableProxy;
                Continuation? finalResult = DispatchContinuations(headContinuation);
                if (finalResult != null)
                {
                    Debug.Assert(finalResult == finalContinuation);
                    return;
                }
            }
        }

        // Return a continuation object if that is the one which has the final
        // result of the Task, if the real output of the series of continuations was
        // an exception, it is allowed to propagate out.
        // OR
        // return NULL to indicate that this isn't yet done.
        private static unsafe Continuation? DispatchContinuations(Continuation? continuation)
        {
            Debug.Assert(continuation != null);

            while (true)
            {
                Continuation? newContinuation;
                try
                {
                    newContinuation = continuation.Resume(continuation);
                }
                catch (Exception ex)
                {
                    continuation = UnwindToPossibleHandler(continuation);
                    if (continuation.Resume == null)
                    {
                        throw;
                    }

                    continuation.GCData![(continuation.Flags & CorInfoContinuationFlags.CORINFO_CONTINUATION_RESULT_IN_GCDATA) != 0 ? 1 : 0] = ex;
                    continue;
                }

                if (newContinuation != null)
                {
                    newContinuation.Next = continuation.Next;
                    return null;
                }

                continuation = continuation.Next;
                Debug.Assert(continuation != null);

                if (continuation.Resume == null)
                {
                    return continuation; // Return the result containing Continuation
                }
            }
        }

        private static Continuation UnwindToPossibleHandler(Continuation continuation)
        {
            while (true)
            {
                Debug.Assert(continuation.Next != null);
                continuation = continuation.Next;
                if ((continuation.Flags & CorInfoContinuationFlags.CORINFO_CONTINUATION_NEEDS_EXCEPTION) != 0)
                    return continuation;
            }
        }
    }

    // Helper class to assist with unsafe pinning of arbitrary objects.
    // It's used by VM code.
    [NonVersionable] // This only applies to field layout
    internal sealed class RawData
    {
        public byte Data;
    }

    // CLR arrays are laid out in memory as follows (multidimensional array bounds are optional):
    // [ sync block || pMethodTable || num components || MD array bounds || array data .. ]
    //                 ^               ^                 ^                  ^ returned reference
    //                 |               |                 \-- ref Unsafe.As<RawArrayData>(array).Data
    //                 \-- array       \-- ref Unsafe.As<RawData>(array).Data
    // The BaseSize of an array includes all the fields before the array data,
    // including the sync block and method table. The reference to RawData.Data
    // points at the number of components, skipping over these two pointer-sized fields.
    [NonVersionable] // This only applies to field layout
    internal sealed class RawArrayData
    {
        public uint Length; // Array._numComponents padded to IntPtr
#if TARGET_64BIT
        public uint Padding;
#endif
        public byte Data;
    }

    // Subset of src\vm\methodtable.h
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct MethodTable
    {
        /// <summary>
        /// The low WORD of the first field is the component size for array and string types.
        /// </summary>
        [FieldOffset(0)]
        public ushort ComponentSize;

        /// <summary>
        /// The flags for the current method table (only for not array or string types).
        /// </summary>
        [FieldOffset(0)]
        private uint Flags;

        /// <summary>
        /// The base size of the type (used when allocating an instance on the heap).
        /// </summary>
        [FieldOffset(4)]
        public uint BaseSize;

        // See additional native members in methodtable.h, not needed here yet.
        // 0x8: m_wFlags2 (additional flags)
        // 0xA: m_wToken (class token if it fits in 16 bits)
        // 0xC: m_wNumVirtuals

        /// <summary>
        /// The number of interfaces implemented by the current type.
        /// </summary>
        [FieldOffset(0x0E)]
        public ushort InterfaceCount;

        // For DEBUG builds, there is a conditional field here (see methodtable.h again).
        // 0x10: debug_m_szClassName (display name of the class, for the debugger)

        /// <summary>
        /// A pointer to the parent method table for the current one.
        /// </summary>
        [FieldOffset(ParentMethodTableOffset)]
        public MethodTable* ParentMethodTable;

        // Additional conditional fields (see methodtable.h).
        // m_pLoaderModule
        // m_pWriteableData
        // union {
        //   m_pEEClass (pointer to the EE class)
        //   m_pCanonMT (pointer to the canonical method table)
        // }

        /// <summary>
        /// This element type handle is in a union with additional info or a pointer to the interface map.
        /// Which one is used is based on the specific method table being in used (so this field is not
        /// always guaranteed to actually be a pointer to a type handle for the element type of this type).
        /// </summary>
        [FieldOffset(ElementTypeOffset)]
        public void* ElementType;

        /// <summary>
        /// This interface map is a union with a multipurpose slot, so should be checked before use.
        /// </summary>
        [FieldOffset(InterfaceMapOffset)]
        public MethodTable** InterfaceMap;

        // WFLAGS_LOW_ENUM
        private const uint enum_flag_GenericsMask = 0x00000030;
        private const uint enum_flag_GenericsMask_NonGeneric = 0x00000000; // no instantiation
        private const uint enum_flag_GenericsMask_GenericInst = 0x00000010; // regular instantiation, e.g. List<String>
        private const uint enum_flag_GenericsMask_SharedInst = 0x00000020; // shared instantiation, e.g. List<__Canon> or List<MyValueType<__Canon>>
        private const uint enum_flag_GenericsMask_TypicalInst = 0x00000030; // the type instantiated at its formal parameters, e.g. List<T>
        private const uint enum_flag_HasDefaultCtor = 0x00000200;
        private const uint enum_flag_IsByRefLike = 0x00001000;

        // WFLAGS_HIGH_ENUM
        private const uint enum_flag_ContainsPointers = 0x01000000;
        private const uint enum_flag_HasComponentSize = 0x80000000;
        private const uint enum_flag_HasTypeEquivalence = 0x02000000;
        private const uint enum_flag_Category_Mask = 0x000F0000;
        private const uint enum_flag_Category_ValueType = 0x00040000;
        private const uint enum_flag_Category_Nullable = 0x00050000;
        private const uint enum_flag_Category_ValueType_Mask = 0x000C0000;
        private const uint enum_flag_Category_Interface = 0x000C0000;
        // Types that require non-trivial interface cast have this bit set in the category
        private const uint enum_flag_NonTrivialInterfaceCast = 0x00080000 // enum_flag_Category_Array
                                                             | 0x40000000 // enum_flag_ComObject
                                                             | 0x00400000 // enum_flag_ICastable;
                                                             | 0x10000000 // enum_flag_IDynamicInterfaceCastable;
                                                             | 0x00040000; // enum_flag_Category_ValueType

        private const int DebugClassNamePtr = // adjust for debug_m_szClassName
#if DEBUG
#if TARGET_64BIT
            8
#else
            4
#endif
#else
            0
#endif
            ;

        private const int ParentMethodTableOffset = 0x10 + DebugClassNamePtr;

#if TARGET_64BIT
        private const int ElementTypeOffset = 0x30 + DebugClassNamePtr;
#else
        private const int ElementTypeOffset = 0x20 + DebugClassNamePtr;
#endif

#if TARGET_64BIT
        private const int InterfaceMapOffset = 0x38 + DebugClassNamePtr;
#else
        private const int InterfaceMapOffset = 0x24 + DebugClassNamePtr;
#endif

        public bool HasComponentSize => (Flags & enum_flag_HasComponentSize) != 0;

        public bool ContainsGCPointers => (Flags & enum_flag_ContainsPointers) != 0;

        public bool NonTrivialInterfaceCast => (Flags & enum_flag_NonTrivialInterfaceCast) != 0;

        public bool HasTypeEquivalence => (Flags & enum_flag_HasTypeEquivalence) != 0;

        internal static bool AreSameType(MethodTable* mt1, MethodTable* mt2) => mt1 == mt2;

        public bool HasDefaultConstructor => (Flags & (enum_flag_HasComponentSize | enum_flag_HasDefaultCtor)) == enum_flag_HasDefaultCtor;

        public bool IsMultiDimensionalArray
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(HasComponentSize);
                // See comment on RawArrayData for details
                return BaseSize > (uint)(3 * sizeof(IntPtr));
            }
        }

        // Returns rank of multi-dimensional array rank, 0 for sz arrays
        public int MultiDimensionalArrayRank
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(HasComponentSize);
                // See comment on RawArrayData for details
                return (int)((BaseSize - (uint)(3 * sizeof(IntPtr))) / (uint)(2 * sizeof(int)));
            }
        }

        public bool IsInterface => (Flags & enum_flag_Category_Mask) == enum_flag_Category_Interface;

        public bool IsValueType => (Flags & enum_flag_Category_ValueType_Mask) == enum_flag_Category_ValueType;

        public bool IsNullable => (Flags & enum_flag_Category_Mask) == enum_flag_Category_Nullable;

        public bool IsByRefLike => (Flags & (enum_flag_HasComponentSize | enum_flag_IsByRefLike)) == enum_flag_IsByRefLike;

        public bool HasInstantiation => (Flags & enum_flag_HasComponentSize) == 0 && (Flags & enum_flag_GenericsMask) != enum_flag_GenericsMask_NonGeneric;

        public bool IsGenericTypeDefinition => (Flags & (enum_flag_HasComponentSize | enum_flag_GenericsMask)) == enum_flag_GenericsMask_TypicalInst;

        public bool IsConstructedGenericType
        {
            get
            {
                uint genericsFlags = Flags & (enum_flag_HasComponentSize | enum_flag_GenericsMask);
                return genericsFlags == enum_flag_GenericsMask_GenericInst || genericsFlags == enum_flag_GenericsMask_SharedInst;
            }
        }

        /// <summary>
        /// Gets a <see cref="TypeHandle"/> for the element type of the current type.
        /// </summary>
        /// <remarks>This method should only be called when the current <see cref="MethodTable"/> instance represents an array or string type.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TypeHandle GetArrayElementTypeHandle()
        {
            Debug.Assert(HasComponentSize);

            return new(ElementType);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern uint GetNumInstanceFieldBytes();
    }

    /// <summary>
    /// A type handle, which can wrap either a pointer to a <c>TypeDesc</c> or to a <see cref="MethodTable"/>.
    /// </summary>
    internal unsafe struct TypeHandle
    {
        // Subset of src\vm\typehandle.h

        /// <summary>
        /// The address of the current type handle object.
        /// </summary>
        private readonly void* m_asTAddr;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TypeHandle(void* tAddr)
        {
            m_asTAddr = tAddr;
        }

        /// <summary>
        /// Gets whether the current instance wraps a <see langword="null"/> pointer.
        /// </summary>
        public bool IsNull => m_asTAddr is null;

        /// <summary>
        /// Gets whether or not this <see cref="TypeHandle"/> wraps a <c>TypeDesc</c> pointer.
        /// Only if this returns <see langword="false"/> it is safe to call <see cref="AsMethodTable"/>.
        /// </summary>
        public bool IsTypeDesc
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ((nint)m_asTAddr & 2) != 0;
        }

        /// <summary>
        /// Gets the <see cref="MethodTable"/> pointer wrapped by the current instance.
        /// </summary>
        /// <remarks>This is only safe to call if <see cref="IsTypeDesc"/> returned <see langword="false"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodTable* AsMethodTable()
        {
            Debug.Assert(!IsTypeDesc);

            return (MethodTable*)m_asTAddr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TypeHandle TypeHandleOf<T>()
        {
            return new TypeHandle((void*)RuntimeTypeHandle.ToIntPtr(typeof(T).TypeHandle));
        }
    }

    // Helper structs used for tail calls via helper.
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PortableTailCallFrame
    {
        public IntPtr TailCallAwareReturnAddress;
        public delegate*<IntPtr, ref byte, PortableTailCallFrame*, void> NextCall;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct TailCallTls
    {
        public PortableTailCallFrame* Frame;
        public IntPtr ArgBuffer;
    }

    [Flags]
    internal enum CorInfoContinuationFlags
    {
        // Whether or not the continuation expects the result to be boxed and
        // placed in the GCData array at index 0. Not set if the callee is void.
        CORINFO_CONTINUATION_RESULT_IN_GCDATA = 1,
        // If this bit is set the continuation resumes inside a try block and thus
        // if an exception is being propagated, needs to be resumed. The exception
        // should be placed at index 0 or 1 depending on whether the continuation
        // also expects a result.
        CORINFO_CONTINUATION_NEEDS_EXCEPTION = 2,
        // If this bit is set the continuation has an OSR IL offset saved in the
        // beginning of 'Data'.
        CORINFO_CONTINUATION_OSR_IL_OFFSET_IN_DATA = 4,
    }

    internal sealed unsafe class Continuation
    {
        public Continuation? Next;
        public delegate*<Continuation, Continuation?> Resume;
        public uint State;
        public CorInfoContinuationFlags Flags;

        // Data and GCData contain the state of the continuation.
        // Note: The JIT is ultimately responsible for laying out these arrays.
        // However, other parts of the system depend on the layout to
        // know where to locate or place various pieces of data:
        //
        // 1. Resumption stubs need to know where to place the return value
        // inside the next continuation. If the return value has GC references
        // then it is boxed and placed at GCData[0]; otherwise, it is placed
        // inside Data at offset 0 if
        // CORINFO_CONTINUATION_OSR_IL_OFFSET_IN_DATA is NOT set and otherwise
        // at offset 4.
        //
        // 2. Likewise, Finalize[Value]TaskReturningThunk needs to know from
        // where to extract the return value.
        //
        // 3. The dispatcher needs to know where to place the exception inside
        // the next continuation with a handler. Continuations with handlers
        // have CORINFO_CONTINUATION_NEEDS_EXCEPTION set. The exception is
        // placed at GCData[0] if CORINFO_CONTINUATION_RESULT_IN_GCDATA is NOT
        // set, and otherwise at GCData[1].
        //
        public byte[]? Data;
        public object[]? GCData;
    }
}
