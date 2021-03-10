// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Internal.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Class for managing wrappers of COM IUnknown types.
    /// </summary>
    public abstract partial class ComWrappers
    {
        internal static IntPtr DefaultIUnknownVftblPtr { get; } = CreateDefaultIUnknownVftbl();

        internal static Guid IID_IUnknown = new Guid(0x00000000, 0x0000, 0x0000, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);

        private static readonly ConditionalWeakTable<object, ManagedObjectWrapperHolder> CCWTable = new ConditionalWeakTable<object, ManagedObjectWrapperHolder>();

        /// <summary>
        /// ABI for function dispatch of a COM interface.
        /// </summary>
        public unsafe partial struct ComInterfaceDispatch
        {
            internal ManagedObjectWrapper* thisPtr;

            /// <summary>
            /// Given a <see cref="System.IntPtr"/> from a generated Vtable, convert to the target type.
            /// </summary>
            /// <typeparam name="T">Desired type.</typeparam>
            /// <param name="dispatchPtr">Pointer supplied to Vtable function entry.</param>
            /// <returns>Instance of type associated with dispatched function call.</returns>
            public static unsafe T GetInstance<T>(ComInterfaceDispatch* dispatchPtr) where T : class
            {
                ManagedObjectWrapper* comInstance = ToManagedObjectWrapper(dispatchPtr);
                return Unsafe.As<T>(RuntimeImports.RhHandleGet(comInstance->Target));
            }

            internal static unsafe ManagedObjectWrapper* ToManagedObjectWrapper(ComInterfaceDispatch* dispatchPtr)
            {
                return dispatchPtr->thisPtr;
            }
        }

        internal enum CreateComInterfaceFlagsEx
        {
            None = CreateComInterfaceFlags.None,
            CallerDefinedIUnknown = CreateComInterfaceFlags.CallerDefinedIUnknown,
            TrackerSupport = CreateComInterfaceFlags.TrackerSupport,

            // Highest bits are reserved for internal usage
            LacksICustomQueryInterface = 1 << 29,
            IsComActivated = 1 << 30,
            IsPegged = 1 << 31,

            InternalMask = IsPegged | IsComActivated | LacksICustomQueryInterface,
        }

        internal unsafe struct ManagedObjectWrapper
        {
            public volatile IntPtr Target; // This is GC Handle
            public long RefCount;

            public int UserDefinedCount;
            public ComInterfaceEntry* UserDefined;
            internal ComInterfaceDispatch* Dispatches;

            internal volatile CreateComInterfaceFlagsEx Flags;
            const ulong ComRefCountMask = 0x000000007fffffffUL;
            static uint GetComCount(ulong c)
            {
                return (uint)(c & ComRefCountMask);
            }

            public uint AddRef()
            {
                return GetComCount((ulong)Interlocked.Increment(ref RefCount));
            }

            public uint Release()
            {
                if (GetComCount((ulong)RefCount) == 0)
                {
                    Debug.Fail("Over release of MOW - COM");
                    return unchecked((uint)-1);
                }

                return GetComCount((ulong)Interlocked.Decrement(ref RefCount));
            }

            public unsafe int QueryInterface(in Guid riid, out IntPtr ppvObject)
            {
                ppvObject = AsRuntimeDefined(in riid);
                if (ppvObject == IntPtr.Zero)
                {
                    ppvObject = AsUserDefined(in riid);
                    if (ppvObject == IntPtr.Zero)
                        return HResults.COR_E_INVALIDCAST;
                }

                AddRef();
                return HResults.S_OK;
            }

            public IntPtr As(in Guid riid)
            {
                // Find target interface and return dispatcher or null if not found.
                IntPtr typeMaybe = AsRuntimeDefined(in riid);
                if (typeMaybe == IntPtr.Zero)
                    typeMaybe = AsUserDefined(in riid);

                return typeMaybe;
            }

            IntPtr AsRuntimeDefined(in Guid riid)
            {
                if ((Flags & CreateComInterfaceFlagsEx.CallerDefinedIUnknown) == CreateComInterfaceFlagsEx.None)
                {
                    if (riid == IID_IUnknown)
                    {
                        return Dispatches[UserDefinedCount + 1].Vtable;
                    }
                }

                return IntPtr.Zero;
            }

            IntPtr AsUserDefined(in Guid riid)
            {
                for (int i = 0; i < UserDefinedCount; ++i)
                {
                    if (UserDefined[i].IID == riid)
                    {
                        return Dispatches[i].Vtable;
                    }
                }

                return IntPtr.Zero;
            }
        }

        internal unsafe class ManagedObjectWrapperHolder
        {
            private ManagedObjectWrapper* wrapper;
            public ManagedObjectWrapperHolder(ManagedObjectWrapper* wrapper)
            {
                this.wrapper = wrapper;
            }

            public unsafe IntPtr ComIp => this.wrapper->As(in ComWrappers.IID_IUnknown);

            ~ManagedObjectWrapperHolder()
            {
                Marshal.FreeCoTaskMem((IntPtr)this.wrapper);
            }
        }

        internal unsafe struct IUnknownVftbl
        {
            public delegate* unmanaged<IntPtr, ref Guid, out IntPtr, int> QueryInterface;
            public delegate* unmanaged<IntPtr, uint> AddRef;
            public delegate* unmanaged<IntPtr, uint> Release;
        }

        /// <summary>
        /// Globally registered instance of the ComWrappers class for reference tracker support.
        /// </summary>
        private static ComWrappers? s_globalInstanceForTrackerSupport;

        /// <summary>
        /// Globally registered instance of the ComWrappers class for marshalling.
        /// </summary>
        private static ComWrappers? s_globalInstanceForMarshalling;

        /// <summary>
        /// Create a COM representation of the supplied object that can be passed to a non-managed environment.
        /// </summary>
        /// <param name="instance">The managed object to expose outside the .NET runtime.</param>
        /// <param name="flags">Flags used to configure the generated interface.</param>
        /// <returns>The generated COM interface that can be passed outside the .NET runtime.</returns>
        /// <remarks>
        /// If a COM representation was previously created for the specified <paramref name="instance" /> using
        /// this <see cref="ComWrappers" /> instance, the previously created COM interface will be returned.
        /// If not, a new one will be created.
        /// </remarks>
        public unsafe IntPtr GetOrCreateComInterfaceForObject(object instance, CreateComInterfaceFlags flags)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (CCWTable.TryGetValue(instance, out ManagedObjectWrapperHolder ccwValue))
            {
                return ccwValue.ComIp;
            }

            ManagedObjectWrapperHolder newValue = CCWTable.GetValue(instance, (c) =>
            {
                ManagedObjectWrapper* value = CreateCCW(this, c, flags);
                return new ManagedObjectWrapperHolder(value);
            });
            return newValue.ComIp;
        }

        private static unsafe ManagedObjectWrapper* CreateCCW(ComWrappers impl, object instance, CreateComInterfaceFlags flags)
        {
            ComInterfaceEntry* userDefined = impl.ComputeVtables(instance, flags, out int userDefinedCount);

            // Maximum number of runtime supplied vtables.
            Span<ComInterfaceEntry> runtimeDefinedLocal = stackalloc ComInterfaceEntry[4];
            int runtimeDefinedCount = 0;

            // Check if the caller will provide the IUnknown table.
            if ((flags & CreateComInterfaceFlags.CallerDefinedIUnknown) == CreateComInterfaceFlags.None)
            {
                ComInterfaceEntry curr = runtimeDefinedLocal[runtimeDefinedCount++];
                curr.IID = IID_IUnknown;
                curr.Vtable = DefaultIUnknownVftblPtr;
            }

            // Check if the caller wants tracker support.
            // if ((flags & CreateComInterfaceFlags.TrackerSupport) == CreateComInterfaceFlags.TrackerSupport)
            // {
            //     ComInterfaceEntry* curr = runtimeDefinedLocal[runtimeDefinedCount++];
            //     curr->IID = __uuidof(IReferenceTrackerTarget);
            //     curr->Vtable = &ManagedObjectWrapper_IReferenceTrackerTargetImpl;
            // }

            // Compute size for ManagedObjectWrapper instance.
            int totalRuntimeDefinedSize = runtimeDefinedCount * sizeof(ComInterfaceEntry);
            int totalDefinedCount = runtimeDefinedCount + userDefinedCount;

            // Compute the total entry size of dispatch section.
            int totalDispatchSectionSize = totalDefinedCount * sizeof(ComInterfaceDispatch);

            // Allocate memory for the ManagedObjectWrapper.
            IntPtr wrapperMem = Marshal.AllocCoTaskMem(
                sizeof(ManagedObjectWrapper) + totalRuntimeDefinedSize + totalDispatchSectionSize);

            // Compute Runtime defined offset.
            IntPtr runtimeDefinedOffset = wrapperMem + totalDispatchSectionSize + sizeof(ManagedObjectWrapper);

            // Compute the dispatch section offset and ensure it is aligned.
            ManagedObjectWrapper* mow = (ManagedObjectWrapper*)wrapperMem;

            // Dispatches follow immediately after ManagedObjectWrapper
            ComInterfaceDispatch* pDispatches = (ComInterfaceDispatch*)(wrapperMem + sizeof(ManagedObjectWrapper));
            for (int i = 0; i < totalDefinedCount; i++)
            {
                pDispatches[i].Vtable = (i < userDefinedCount) ? userDefined[i].Vtable : runtimeDefinedLocal[i - userDefinedCount].Vtable;
                pDispatches[i].thisPtr = mow;
            }

            mow->Target = IntPtr.Zero;
            mow->RefCount = 1;
            mow->UserDefinedCount = userDefinedCount;
            mow->UserDefined = userDefined;
            mow->Flags = (CreateComInterfaceFlagsEx)flags;
            mow->Dispatches = pDispatches;
            return mow;
        }

        /// <summary>
        /// Get the currently registered managed object or creates a new managed object and registers it.
        /// </summary>
        /// <param name="externalComObject">Object to import for usage into the .NET runtime.</param>
        /// <param name="flags">Flags used to describe the external object.</param>
        /// <returns>Returns a managed object associated with the supplied external COM object.</returns>
        /// <remarks>
        /// If a managed object was previously created for the specified <paramref name="externalComObject" />
        /// using this <see cref="ComWrappers" /> instance, the previously created object will be returned.
        /// If not, a new one will be created.
        /// </remarks>
        public object GetOrCreateObjectForComInstance(IntPtr externalComObject, CreateObjectFlags flags)
        {
            object? obj;
            if (!TryGetOrCreateObjectForComInstanceInternal(this, externalComObject, IntPtr.Zero, flags, null, out obj))
                throw new ArgumentNullException(nameof(externalComObject));

            return obj!;
        }

        /// <summary>
        /// Get the currently registered managed object or uses the supplied managed object and registers it.
        /// </summary>
        /// <param name="externalComObject">Object to import for usage into the .NET runtime.</param>
        /// <param name="flags">Flags used to describe the external object.</param>
        /// <param name="wrapper">The <see cref="object"/> to be used as the wrapper for the external object</param>
        /// <returns>Returns a managed object associated with the supplied external COM object.</returns>
        /// <remarks>
        /// If the <paramref name="wrapper"/> instance already has an associated external object a <see cref="System.NotSupportedException"/> will be thrown.
        /// </remarks>
        public object GetOrRegisterObjectForComInstance(IntPtr externalComObject, CreateObjectFlags flags, object wrapper)
        {
            return GetOrRegisterObjectForComInstance(externalComObject, flags, wrapper, IntPtr.Zero);
        }

        /// <summary>
        /// Get the currently registered managed object or uses the supplied managed object and registers it.
        /// </summary>
        /// <param name="externalComObject">Object to import for usage into the .NET runtime.</param>
        /// <param name="flags">Flags used to describe the external object.</param>
        /// <param name="wrapper">The <see cref="object"/> to be used as the wrapper for the external object</param>
        /// <param name="inner">Inner for COM aggregation scenarios</param>
        /// <returns>Returns a managed object associated with the supplied external COM object.</returns>
        /// <remarks>
        /// This method override is for registering an aggregated COM instance with its associated inner. The inner
        /// will be released when the associated wrapper is eventually freed. Note that it will be released on a thread
        /// in an unknown apartment state. If the supplied inner is not known to be a free-threaded instance then
        /// it is advised to not supply the inner.
        ///
        /// If the <paramref name="wrapper"/> instance already has an associated external object a <see cref="System.NotSupportedException"/> will be thrown.
        /// </remarks>
        public object GetOrRegisterObjectForComInstance(IntPtr externalComObject, CreateObjectFlags flags, object wrapper, IntPtr inner)
        {
            if (wrapper == null)
                throw new ArgumentNullException(nameof(wrapper));

            object? obj;
            if (!TryGetOrCreateObjectForComInstanceInternal(this, externalComObject, inner, flags, wrapper, out obj))
                throw new ArgumentNullException(nameof(externalComObject));

            return obj!;
        }

        /// <summary>
        /// Get the currently registered managed object or creates a new managed object and registers it.
        /// </summary>
        /// <param name="impl">The <see cref="ComWrappers" /> implementation to use when creating the managed object.</param>
        /// <param name="externalComObject">Object to import for usage into the .NET runtime.</param>
        /// <param name="innerMaybe">The inner instance if aggregation is involved</param>
        /// <param name="flags">Flags used to describe the external object.</param>
        /// <param name="wrapperMaybe">The <see cref="object"/> to be used as the wrapper for the external object.</param>
        /// <param name="retValue">The managed object associated with the supplied external COM object or <c>null</c> if it could not be created.</param>
        /// <returns>Returns <c>true</c> if a managed object could be retrieved/created, <c>false</c> otherwise</returns>
        /// <remarks>
        /// If <paramref name="impl" /> is <c>null</c>, the global instance (if registered) will be used.
        /// </remarks>
        private static bool TryGetOrCreateObjectForComInstanceInternal(
            ComWrappers impl,
            IntPtr externalComObject,
            IntPtr innerMaybe,
            CreateObjectFlags flags,
            object? wrapperMaybe,
            out object? retValue)
        {
            if (externalComObject == IntPtr.Zero)
                throw new ArgumentNullException(nameof(externalComObject));

            if (flags.HasFlag(CreateObjectFlags.Aggregation))
                throw new NotImplementedException();

            // If the inner is supplied the Aggregation flag should be set.
            // if (innerMaybe != IntPtr.Zero && !flags.HasFlag(CreateObjectFlags.Aggregation))
            //    throw new InvalidOperationException(SR.InvalidOperation_SuppliedInnerMustBeMarkedAggregation);

            object? wrapperMaybeLocal = wrapperMaybe;
            retValue = null;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Register a <see cref="ComWrappers" /> instance to be used as the global instance for reference tracker support.
        /// </summary>
        /// <param name="instance">Instance to register</param>
        /// <remarks>
        /// This function can only be called a single time. Subsequent calls to this function will result
        /// in a <see cref="System.InvalidOperationException"/> being thrown.
        ///
        /// Scenarios where this global instance may be used are:
        ///  * Object tracking via the <see cref="CreateComInterfaceFlags.TrackerSupport" /> and <see cref="CreateObjectFlags.TrackerObject" /> flags.
        /// </remarks>
        public static void RegisterForTrackerSupport(ComWrappers instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (null != Interlocked.CompareExchange(ref s_globalInstanceForTrackerSupport, instance, null))
            {
                throw new InvalidOperationException(SR.InvalidOperation_ResetGlobalComWrappersInstance);
            }
        }

        /// <summary>
        /// Register a <see cref="ComWrappers" /> instance to be used as the global instance for marshalling in the runtime.
        /// </summary>
        /// <param name="instance">Instance to register</param>
        /// <remarks>
        /// This function can only be called a single time. Subsequent calls to this function will result
        /// in a <see cref="System.InvalidOperationException"/> being thrown.
        ///
        /// Scenarios where this global instance may be used are:
        ///  * Usage of COM-related Marshal APIs
        ///  * P/Invokes with COM-related types
        ///  * COM activation
        /// </remarks>
        public static void RegisterForMarshalling(ComWrappers instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (null != Interlocked.CompareExchange(ref s_globalInstanceForMarshalling, instance, null))
            {
                throw new InvalidOperationException(SR.InvalidOperation_ResetGlobalComWrappersInstance);
            }
        }

        /// <summary>
        /// Get the runtime provided IUnknown implementation.
        /// </summary>
        /// <param name="fpQueryInterface">Function pointer to QueryInterface.</param>
        /// <param name="fpAddRef">Function pointer to AddRef.</param>
        /// <param name="fpRelease">Function pointer to Release.</param>
        protected internal static unsafe void GetIUnknownImpl(out IntPtr fpQueryInterface, out IntPtr fpAddRef, out IntPtr fpRelease)
        {
            fpQueryInterface = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&ComWrappers.IUnknown_QueryInterface;
            fpAddRef = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&ComWrappers.IUnknown_AddRef;
            fpRelease = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&ComWrappers.IUnknown_Release;
        }

        internal static int CallICustomQueryInterface(object customQueryInterfaceMaybe, ref Guid iid, out IntPtr ppObject)
        {
            var customQueryInterface = customQueryInterfaceMaybe as ICustomQueryInterface;
            if (customQueryInterface is null)
            {
                ppObject = IntPtr.Zero;
                return -1; // See TryInvokeICustomQueryInterfaceResult
            }

            return (int)customQueryInterface.GetInterface(ref iid, out ppObject);
        }

        [UnmanagedCallersOnly]
        internal static unsafe int IUnknown_QueryInterface(IntPtr pThis, Guid* guid, IntPtr* ppObject)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)pThis);
            return wrapper->QueryInterface(in *guid, out *ppObject);
        }

        [UnmanagedCallersOnly]
        internal static unsafe uint IUnknown_AddRef(IntPtr pThis)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)pThis);
            return wrapper->AddRef();
        }

        [UnmanagedCallersOnly]
        internal static unsafe uint IUnknown_Release(IntPtr pThis)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)pThis);
            return wrapper->Release();
        }

        private static unsafe IntPtr CreateDefaultIUnknownVftbl()
        {
            IntPtr* vftbl = (IntPtr*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(ComWrappers), 3 * sizeof(IntPtr));
            GetIUnknownImpl(out vftbl[0], out vftbl[1], out vftbl[2]);
            return (IntPtr)vftbl;
        }
    }
}
