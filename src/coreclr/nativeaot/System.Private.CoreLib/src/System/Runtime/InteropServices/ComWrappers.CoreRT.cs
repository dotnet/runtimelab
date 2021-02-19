// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Internal.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Internal enumeration used by the runtime to indicate the scenario for which ComWrappers is being used.
    /// </summary>
    internal enum ComWrappersScenario
    {
        Instance = 0,
        TrackerSupportGlobalInstance = 1,
        MarshallingGlobalInstance = 2,
    }

    /// <summary>
    /// Class for managing wrappers of COM IUnknown types.
    /// </summary>
    public abstract partial class ComWrappers
    {
        internal static unsafe IUnknownVftbl DefaultIUnknownVftbl => Unsafe.AsRef<IUnknownVftbl>(DefaultIUnknownVftblPtr.ToPointer());

        internal static IntPtr DefaultIUnknownVftblPtr { get; }

        internal static Guid IID_IUnknown = new Guid(0x00000000, 0x0000, 0x0000, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);

        const int DispatchAlignmentThisPtr = 16;
        private static readonly ConditionalWeakTable<object, ManagedObjectWrapperHolder> CCWTable = new ConditionalWeakTable<object, ManagedObjectWrapperHolder>();

        /// <summary>
        /// ABI for function dispatch of a COM interface.
        /// </summary>
        public partial struct ComInterfaceDispatch
        {
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
                // See the dispatch section in the runtime for details on the masking below.
                const long DispatchThisPtrMask = ~0xfL;
                ManagedObjectWrapper* comInstance = *(ManagedObjectWrapper**)(((long)dispatchPtr) & DispatchThisPtrMask);
                return comInstance;
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

            public int RuntimeDefinedCount;
            public int UserDefinedCount;
            public ComInterfaceEntry* RuntimeDefined;
            public ComInterfaceEntry* UserDefined;
            internal DispatchSectionEntry* Dispatches;

            internal volatile CreateComInterfaceFlagsEx Flags;
            const ulong ComRefCountMask = 0x000000007fffffffUL;
            static ulong GetComCount(ulong c)
            {
                return c & ComRefCountMask;
            }

            public ulong AddRef()
            {
                return GetComCount((ulong)Interlocked.Increment(ref RefCount));
            }

            public ulong Release()
            {
                if (GetComCount((ulong)RefCount) == 0)
                {
                    Debug.Fail("Over release of MOW - COM");
                    return unchecked((ulong)-1);
                }

                return GetComCount((ulong)Interlocked.Decrement(ref RefCount));
            }

            public unsafe int QueryInterface(ref Guid riid, out IntPtr ppvObject)
            {
                ppvObject = AsRuntimeDefined(ref riid);
                if (ppvObject == IntPtr.Zero)
                {
                    ppvObject = AsUserDefined(ref riid);
                    if (ppvObject == IntPtr.Zero)
                        return HResults.COR_E_INVALIDCAST;
                }

                AddRef();
                return HResults.S_OK;
            }

            public IntPtr As(ref Guid riid)
            {
                // Find target interface and return dispatcher or null if not found.
                IntPtr typeMaybe = AsRuntimeDefined(ref riid);
                if (typeMaybe == IntPtr.Zero)
                    typeMaybe = AsUserDefined(ref riid);

                return typeMaybe;
            }

            IntPtr AsRuntimeDefined(ref Guid riid)
            {
                for (int i = 0; i < RuntimeDefinedCount; ++i)
                {
                    if (RuntimeDefined[i].IID == riid)
                    {
                        return Dispatches[i].Vtable;
                    }
                }

                return IntPtr.Zero;
            }

            IntPtr AsUserDefined(ref Guid riid)
            {
                for (int i = 0; i < UserDefinedCount; ++i)
                {
                    if (UserDefined[i].IID == riid)
                    {
                        return Dispatches[i + RuntimeDefinedCount].Vtable;
                    }
                }

                return IntPtr.Zero;
            }
        }
        internal unsafe struct EntrySet
        {
            public ComInterfaceEntry* Start;
            public int Count;

            public EntrySet(ComInterfaceEntry* start, int count)
            {
                this.Start = start;
                this.Count = count;
            }
        }

        internal unsafe class ManagedObjectWrapperHolder
        {
            private ManagedObjectWrapper* wrapper;
            public ManagedObjectWrapperHolder(ManagedObjectWrapper* wrapper)
            {
                this.wrapper = wrapper;
            }

            public unsafe IntPtr ComIp => this.wrapper->As(ref ComWrappers.IID_IUnknown);
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
            public static IUnknownVftbl AbiToProjectionVftbl => ComWrappers.DefaultIUnknownVftbl;
            public static IntPtr AbiToProjectionVftblPtr => ComWrappers.DefaultIUnknownVftblPtr;
        }

        /// <summary>
        /// Globally registered instance of the ComWrappers class for reference tracker support.
        /// </summary>
        private static ComWrappers? s_globalInstanceForTrackerSupport;

        /// <summary>
        /// Globally registered instance of the ComWrappers class for marshalling.
        /// </summary>
        private static ComWrappers? s_globalInstanceForMarshalling;

        private static long s_instanceCounter;
        private readonly long id = Interlocked.Increment(ref s_instanceCounter);

        static unsafe ComWrappers()
        {
            GetIUnknownImplInternal(out IntPtr qi, out IntPtr addRef, out IntPtr release);

            DefaultIUnknownVftblPtr = RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(IUnknownVftbl), sizeof(IUnknownVftbl));
            (*(IUnknownVftbl*)DefaultIUnknownVftblPtr) = new IUnknownVftbl
            {
                QueryInterface = (delegate* unmanaged<IntPtr, ref Guid, out IntPtr, int>)qi,
                AddRef = (delegate* unmanaged<IntPtr, uint>)addRef,
                Release = (delegate* unmanaged<IntPtr, uint>)release,
            };
        }

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
        public IntPtr GetOrCreateComInterfaceForObject(object instance, CreateComInterfaceFlags flags)
        {
            IntPtr ptr;
            if (!TryGetOrCreateComInterfaceForObjectInternal(this, instance, flags, out ptr))
                throw new ArgumentException(null, nameof(instance));

            return ptr;
        }

        /// <summary>
        /// Create a COM representation of the supplied object that can be passed to a non-managed environment.
        /// </summary>
        /// <param name="impl">The <see cref="ComWrappers" /> implementation to use when creating the COM representation.</param>
        /// <param name="instance">The managed object to expose outside the .NET runtime.</param>
        /// <param name="flags">Flags used to configure the generated interface.</param>
        /// <param name="retValue">The generated COM interface that can be passed outside the .NET runtime or IntPtr.Zero if it could not be created.</param>
        /// <returns>Returns <c>true</c> if a COM representation could be created, <c>false</c> otherwise</returns>
        /// <remarks>
        /// If <paramref name="impl" /> is <c>null</c>, the global instance (if registered) will be used.
        /// </remarks>
        private static unsafe bool TryGetOrCreateComInterfaceForObjectInternal(ComWrappers impl, object instance, CreateComInterfaceFlags flags, out IntPtr retValue)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            bool success = true;
            ManagedObjectWrapperHolder ccwValue = CCWTable.GetValue(instance, (c) =>
            {
                var value = CreateCCW(impl, c, flags);
                success = value != null;
                return new ManagedObjectWrapperHolder(value);
            });
            // I should get IUnknown implementation from MOW. It maye have behave incorrectly when using user provided IUnknown.
            retValue = ccwValue.ComIp;
            return success;
        }

        private static unsafe ManagedObjectWrapper* CreateCCW(ComWrappers impl, object instance, CreateComInterfaceFlags flags)
        {
            ComInterfaceEntry* userDefined = impl.ComputeVtables(instance, flags, out int userDefinedCount);
            // Here I should create someing like that https://github.com/dotnet/runtime/blob/9f8aab73d93156933ae65a476204bf62c02f6537/src/coreclr/interop/comwrappers.cpp#L16
            // Which would be saved to CCW cache.
            // Creation of CCW is basically ManagedObjectWrapper::Create reimplementation.

            // Maximum number of runtime supplied vtables.
            Span<ComInterfaceEntry> runtimeDefinedLocal = stackalloc ComInterfaceEntry[4];
            int runtimeDefinedCount = 0;

            // Check if the caller will provide the IUnknown table.
            if ((flags & CreateComInterfaceFlags.CallerDefinedIUnknown) == CreateComInterfaceFlags.None)
            {
                ComInterfaceEntry curr = runtimeDefinedLocal[runtimeDefinedCount++];
                curr.IID = IID_IUnknown;
                curr.Vtable = ComWrappers.DefaultIUnknownVftblPtr;
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
            int totalDispatchSectionCount = ComputeThisPtrForDispatchSection(totalDefinedCount) + totalDefinedCount;
            int totalDispatchSectionSize = totalDispatchSectionCount * sizeof(void*);

            // Allocate memory for the ManagedObjectWrapper.
            int AlignmentThisPtrMaxPadding = DispatchAlignmentThisPtr - sizeof(void*);
            IntPtr wrapperMem = Marshal.AllocCoTaskMem(
                sizeof(ManagedObjectWrapper) + totalRuntimeDefinedSize + totalDispatchSectionSize + AlignmentThisPtrMaxPadding);

            if (wrapperMem == IntPtr.Zero)
                ThrowHelper.ThrowOutOfMemoryException();

            // Compute Runtime defined offset.
            IntPtr runtimeDefinedOffset = wrapperMem + sizeof(ManagedObjectWrapper);

            // Copy in runtime supplied COM interface entries.
            ComInterfaceEntry* runtimeDefined = null;
            if (0 < runtimeDefinedCount)
            {
                // FIXME: I may have to get rid of using Span, since it's more pain to implement using that API.
                runtimeDefinedLocal.Slice(0, runtimeDefinedCount).CopyTo(new Span<ComInterfaceEntry>((void*)runtimeDefinedOffset, runtimeDefinedCount));
                runtimeDefined = (ComInterfaceEntry*)(runtimeDefinedOffset);
            }

            // Compute the dispatch section offset and ensure it is aligned.
            IntPtr dispatchSectionOffset = runtimeDefinedOffset + totalRuntimeDefinedSize;
            dispatchSectionOffset = AlignDispatchSection(dispatchSectionOffset, AlignmentThisPtrMaxPadding);
            Debug.Assert(dispatchSectionOffset != IntPtr.Zero);

            // Define the sets for the tables to insert
            EntrySet[] AllEntries =
            {
                new EntrySet(runtimeDefined, runtimeDefinedCount),
                new EntrySet(userDefined, userDefinedCount)
            };

            ManagedObjectWrapper* mow = (ManagedObjectWrapper*)wrapperMem;
            PopulateDispatchSection(
                mow,
                (DispatchSectionEntry*)dispatchSectionOffset,
                AllEntries);
            // Hope I properly understand how line below works.
            // https://github.com/dotnet/runtime/blob/9f8aab73d93156933ae65a476204bf62c02f6537/src/coreclr/interop/comwrappers.cpp#L401
            mow->Target = IntPtr.Zero;
            mow->RefCount = 1;
            mow->RuntimeDefinedCount = runtimeDefinedCount;
            mow->RuntimeDefined = runtimeDefined;
            mow->UserDefinedCount = userDefinedCount;
            mow->UserDefined = userDefined;
            mow->Flags = (CreateComInterfaceFlagsEx)flags;
            mow->Dispatches = (DispatchSectionEntry*)dispatchSectionOffset;
            return mow;
        }

        internal unsafe struct DispatchSectionEntry
        {
            public ManagedObjectWrapper* thisPtr;
            public IntPtr Vtable;
        }

        // Given a pointer and a padding allowance, attempt to find an offset into
        // the memory that is properly aligned for the dispatch section.
        static unsafe IntPtr AlignDispatchSection(IntPtr section, int extraPadding)
        {
            // If the dispatch section is not properly aligned by default, we
            // utilize the padding to make sure the dispatch section is aligned.
            while ((section.ToInt32() % DispatchAlignmentThisPtr) != 0)
            {
                // Check if there is padding to attempt an alignment.
                if (extraPadding <= 0)
                    return IntPtr.Zero;

                extraPadding -= sizeof(void*);
#if DEBUG
                // Poison unused portions of the section.
                new Span<byte>((void*)section, sizeof(void*)).Fill(0xff);
#endif

                section += sizeof(void*);
            }

            return section;
        }

        // Populate the dispatch section with the entry sets
        static unsafe void PopulateDispatchSection(
            ManagedObjectWrapper* thisPtr,
            DispatchSectionEntry* dispatchSection,
            Span<EntrySet> entrySets)
        {
            // Define dispatch section iterator.
            DispatchSectionEntry* currDisp = dispatchSection;

            // Iterate over all interface entry sets.
            foreach (var curr in entrySets)
            {
                ComInterfaceEntry* currEntry = curr.Start;
                int entryCount = curr.Count;

                // Update dispatch section with 'this' pointer and vtables.
                for (int i = 0; i < entryCount; ++i, ++currEntry, currDisp++)
                {
                    // Insert the 'this' pointer at the appropriate locations
                    currDisp->thisPtr = thisPtr;

                    // Fill in the dispatch entry
                    currDisp->Vtable = currEntry->Vtable;
                }
            }
        }


        // Called by the runtime to execute the abstract instance function
        internal static unsafe void* CallComputeVtables(ComWrappersScenario scenario, ComWrappers? comWrappersImpl, object obj, CreateComInterfaceFlags flags, out int count)
        {
            ComWrappers? impl = null;
            switch (scenario)
            {
                case ComWrappersScenario.Instance:
                    impl = comWrappersImpl;
                    break;
                case ComWrappersScenario.TrackerSupportGlobalInstance:
                    impl = s_globalInstanceForTrackerSupport;
                    break;
                case ComWrappersScenario.MarshallingGlobalInstance:
                    impl = s_globalInstanceForMarshalling;
                    break;
            }

            if (impl is null)
            {
                count = -1;
                return null;
            }

            return impl.ComputeVtables(obj, flags, out count);
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

        // Called by the runtime to execute the abstract instance function.
        internal static object? CallCreateObject(ComWrappersScenario scenario, ComWrappers? comWrappersImpl, IntPtr externalComObject, CreateObjectFlags flags)
        {
            ComWrappers? impl = null;
            switch (scenario)
            {
                case ComWrappersScenario.Instance:
                    impl = comWrappersImpl;
                    break;
                case ComWrappersScenario.TrackerSupportGlobalInstance:
                    impl = s_globalInstanceForTrackerSupport;
                    break;
                case ComWrappersScenario.MarshallingGlobalInstance:
                    impl = s_globalInstanceForMarshalling;
                    break;
            }

            if (impl == null)
                return null;

            return impl.CreateObject(externalComObject, flags);
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

        // Call to execute the virtual instance function
        internal static void CallReleaseObjects(ComWrappers? comWrappersImpl, IEnumerable objects)
            => (comWrappersImpl ?? s_globalInstanceForTrackerSupport!).ReleaseObjects(objects);

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
        protected internal static void GetIUnknownImpl(out IntPtr fpQueryInterface, out IntPtr fpAddRef, out IntPtr fpRelease)
            => GetIUnknownImplInternal(out fpQueryInterface, out fpAddRef, out fpRelease);

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

        private static unsafe int ComputeThisPtrForDispatchSection(int dispatchCount)
        {
            // For 64 bit architeture that always would be the case.
            const int EntriesPerThisPtr = 1;
            return (dispatchCount / EntriesPerThisPtr) + ((dispatchCount % EntriesPerThisPtr) == 0 ? 0 : 1);
        }

        [UnmanagedCallersOnly]
        internal static unsafe int ABI_QueryInterface(IntPtr ppObject, ref Guid guid, out IntPtr returnValue)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)ppObject);
            return wrapper->QueryInterface(ref guid, out returnValue);
        }

        [UnmanagedCallersOnly]
        internal static unsafe ulong ABI_AddRef(IntPtr ppObject)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)ppObject);
            return wrapper->AddRef();
        }

        [UnmanagedCallersOnly]
        internal static unsafe ulong ABI_Release(IntPtr ppObject)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)ppObject);
            return wrapper->Release();
        }

        internal static unsafe void GetIUnknownImplInternal(out IntPtr fpQueryInterface, out IntPtr fpAddRef, out IntPtr fpRelease)
        {
            fpQueryInterface = (IntPtr)(delegate* unmanaged<IntPtr, ref Guid, out IntPtr, int>)&ComWrappers.ABI_QueryInterface;
            fpAddRef = (IntPtr)(delegate* unmanaged<IntPtr, ulong>)&ComWrappers.ABI_AddRef;
            fpRelease = (IntPtr)(delegate* unmanaged<IntPtr, ulong>)&ComWrappers.ABI_Release;
        }
    }
}
