// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.InteropServices;

using Internal.Runtime.Augments;

namespace Internal.Runtime.CompilerServices
{
    // This structure is used to resolve a instance method given an object instance. To use this type
    // 1) New up an instance using one of the constructors below.
    // 2) Use the ToIntPtr() method to get the interned instance of this type. This will permanently allocate
    //    a block of memory that can be used to represent a virtual method resolution. This memory is interned
    //    so that repeated allocation of the same resolver will not leak.
    // 3) Use the ResolveMethod function to do the virtual lookup. This function takes advantage of
    //    a lockless cache so the resolution is very fast for repeated lookups.
    public unsafe struct OpenMethodResolver : IEquatable<OpenMethodResolver>
    {
        // Lazy initialized to point to the type loader method when the first `GVMResolve` resolver is created
        private static delegate*<object, RuntimeMethodHandle, nint> s_lazyGvmLookupForSlot;

        public const short DispatchResolve = 0;
        public const short GVMResolve = 1;
        public const short OpenNonVirtualResolve = 2;
        public const short OpenNonVirtualResolveLookthruUnboxing = 3;

        private readonly short _resolveType;
        private readonly GCHandle _readerGCHandle;
        private readonly int _handle;
        private readonly IntPtr _methodHandleOrSlotOrCodePointer;
        private readonly IntPtr _nonVirtualOpenInvokeCodePointer;
        private readonly MethodTable* _declaringType;

        public OpenMethodResolver(RuntimeTypeHandle declaringTypeOfSlot, int slot, GCHandle readerGCHandle, int handle)
        {
            _resolveType = DispatchResolve;
            _declaringType = declaringTypeOfSlot.ToMethodTable();
            _methodHandleOrSlotOrCodePointer = new IntPtr(slot);
            _handle = handle;
            _readerGCHandle = readerGCHandle;
            _nonVirtualOpenInvokeCodePointer = IntPtr.Zero;
        }

        public OpenMethodResolver(RuntimeTypeHandle declaringTypeOfSlot, RuntimeMethodHandle gvmSlot, GCHandle readerGCHandle, int handle)
        {
            _resolveType = GVMResolve;
            _methodHandleOrSlotOrCodePointer = *(IntPtr*)&gvmSlot;
            _declaringType = declaringTypeOfSlot.ToMethodTable();
            _handle = handle;
            _readerGCHandle = readerGCHandle;
            _nonVirtualOpenInvokeCodePointer = IntPtr.Zero;

            if (s_lazyGvmLookupForSlot == null)
                s_lazyGvmLookupForSlot = &TypeLoaderExports.GVMLookupForSlot;
        }

        public OpenMethodResolver(RuntimeTypeHandle declaringType, IntPtr codePointer, GCHandle readerGCHandle, int handle, short resolveType)
        {
            _resolveType = resolveType;
            _methodHandleOrSlotOrCodePointer = codePointer;
            _declaringType = declaringType.ToMethodTable();
            _handle = handle;
            _readerGCHandle = readerGCHandle;
            if (resolveType == OpenNonVirtualResolve)
                _nonVirtualOpenInvokeCodePointer = codePointer;
            else if (resolveType == OpenNonVirtualResolveLookthruUnboxing)
                _nonVirtualOpenInvokeCodePointer = RuntimeAugments.TypeLoaderCallbacks.ConvertUnboxingFunctionPointerToUnderlyingNonUnboxingPointer(codePointer, declaringType);
            else
                throw new NotSupportedException();
        }

        public short ResolverType
        {
            get
            {
                return _resolveType;
            }
        }

        public RuntimeTypeHandle DeclaringType
        {
            get
            {
                return new RuntimeTypeHandle(_declaringType);
            }
        }

        public RuntimeMethodHandle GVMMethodHandle
        {
            get
            {
                IntPtr localIntPtr = _methodHandleOrSlotOrCodePointer;
                IntPtr* pMethodHandle = &localIntPtr;
                return *(RuntimeMethodHandle*)pMethodHandle;
            }
        }

        public bool IsOpenNonVirtualResolve
        {
            get
            {
                switch (_resolveType)
                {
                    case OpenNonVirtualResolve:
                    case OpenNonVirtualResolveLookthruUnboxing:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public IntPtr CodePointer
        {
            get
            {
                return _methodHandleOrSlotOrCodePointer;
            }
        }

        public object Reader
        {
            get
            {
                return _readerGCHandle.Target;
            }
        }

        public int Handle
        {
            get
            {
                return _handle;
            }
        }

        private IntPtr ResolveMethod(object thisObject)
        {
            if (_resolveType == DispatchResolve)
            {
                return RuntimeImports.RhResolveDispatch(thisObject, _declaringType, (ushort)_methodHandleOrSlotOrCodePointer);
            }
            else if (_resolveType == GVMResolve)
            {
                return s_lazyGvmLookupForSlot(thisObject, GVMMethodHandle);
            }
            else
            {
                throw new NotSupportedException(); // Should never happen, in this case, the dispatch should be resolved in the other ResolveMethod function
            }
        }

        internal static IntPtr ResolveMethodWorker(IntPtr resolver, object thisObject)
        {
            return ((OpenMethodResolver*)resolver)->ResolveMethod(thisObject);
        }

        public static IntPtr ResolveMethod(IntPtr resolver, object thisObject)
        {
            IntPtr nonVirtualOpenInvokeCodePointer = ((OpenMethodResolver*)resolver)->_nonVirtualOpenInvokeCodePointer;
            if (nonVirtualOpenInvokeCodePointer != IntPtr.Zero)
                return nonVirtualOpenInvokeCodePointer;

            return TypeLoaderExports.OpenInstanceMethodLookup(resolver, thisObject);
        }

        public static IntPtr ResolveMethod(IntPtr resolverPtr, RuntimeTypeHandle thisType)
        {
            OpenMethodResolver* resolver = ((OpenMethodResolver*)resolverPtr);
            IntPtr nonVirtualOpenInvokeCodePointer = resolver->_nonVirtualOpenInvokeCodePointer;
            if (nonVirtualOpenInvokeCodePointer != IntPtr.Zero)
                return nonVirtualOpenInvokeCodePointer;

            return RuntimeImports.RhResolveDispatchOnType(thisType.ToMethodTable(), resolver->_declaringType, (ushort)resolver->_methodHandleOrSlotOrCodePointer);
        }

        private static int CalcHashCode(int hashCode1, int hashCode2, int hashCode3, int hashCode4)
        {
            int length = 4;

            int hash1 = 0x449b3ad6;
            int hash2 = (length << 3) + 0x55399219;

            hash1 = (hash1 + int.RotateLeft(hash1, 5)) ^ hashCode1;
            hash2 = (hash2 + int.RotateLeft(hash2, 5)) ^ hashCode2;
            hash1 = (hash1 + int.RotateLeft(hash1, 5)) ^ hashCode3;
            hash2 = (hash2 + int.RotateLeft(hash2, 5)) ^ hashCode4;

            hash1 += int.RotateLeft(hash1, 8);
            hash2 += int.RotateLeft(hash2, 8);

            return hash1 ^ hash2;
        }

        public override int GetHashCode()
        {
            return CalcHashCode(_resolveType, _handle, _methodHandleOrSlotOrCodePointer.GetHashCode(), _declaringType == null ? 0 : (int)_declaringType->HashCode);
        }

        public bool Equals(OpenMethodResolver other)
        {
            if (other._resolveType != _resolveType)
                return false;

            if (other._handle != _handle)
                return false;

            if (other._methodHandleOrSlotOrCodePointer != _methodHandleOrSlotOrCodePointer)
                return false;

            return other._declaringType == _declaringType;
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is OpenMethodResolver))
            {
                return false;
            }

            return ((OpenMethodResolver)obj).Equals(this);
        }

        private static LowLevelDictionary<OpenMethodResolver, IntPtr> s_internedResolverHash = new LowLevelDictionary<OpenMethodResolver, IntPtr>();

        public IntPtr ToIntPtr()
        {
            lock (s_internedResolverHash)
            {
                IntPtr returnValue;
                if (s_internedResolverHash.TryGetValue(this, out returnValue))
                    return returnValue;
                returnValue = (IntPtr)NativeMemory.Alloc((nuint)sizeof(OpenMethodResolver));
                *((OpenMethodResolver*)returnValue) = this;
                s_internedResolverHash.Add(this, returnValue);
                return returnValue;
            }
        }
    }
}
