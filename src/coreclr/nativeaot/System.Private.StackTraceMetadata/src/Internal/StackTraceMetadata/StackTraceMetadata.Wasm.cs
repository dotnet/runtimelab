// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

using Internal.Reflection.Execution;
using Internal.Runtime;
using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;

namespace Internal.StackTraceMetadata
{
    internal static partial class StackTraceMetadata
    {
        private static FunctionPointerAndStackTraceIp[] _functionPointerToStackTraceIpMap;

        private static IntPtr ConvertFunctionPointerToStackTraceIp(IntPtr functionPointer)
        {
            if (RuntimeAugments.PreciseVirtualUnwind)
            {
                FunctionPointerAndStackTraceIp[] map = _functionPointerToStackTraceIpMap;
                if (map is null)
                {
                    _functionPointerToStackTraceIpMap = map = ConstructFunctionPointerToStackTraceIpMap();
                }

                int index = Array.BinarySearch(map, new FunctionPointerAndStackTraceIp() { FunctionPointer = functionPointer });
                if (index >= 0)
                {
                    return map[index].StackTraceIp;
                }
                return 0;
            }

            return ConvertFunctionPointerToStackTraceIpForNativeUnwind(functionPointer);
        }

        private static unsafe FunctionPointerAndStackTraceIp[] ConstructFunctionPointerToStackTraceIpMap()
        {
            int index = 0;
            FunctionPointerAndStackTraceIp[] map = new FunctionPointerAndStackTraceIp[200];

            void AddImpl(void* functionPointer, void* stackTraceIp)
            {
                if (index == map.Length)
                {
                    Array.Resize(ref map, 2 * index);
                }
                ref FunctionPointerAndStackTraceIp item = ref map[index++];
                item.FunctionPointer = (IntPtr)functionPointer;
                item.StackTraceIp = (IntPtr)stackTraceIp;
            }
            void Add(void* functionPointer, void* stackTraceIp)
            {
                AddImpl(functionPointer, stackTraceIp);

                // "Delegate.GetDiagnosticMethodInfo" may pass us both boxed and unboxed entrypoints, so add in
                // the unboxed entrypoint, if present (the unwind info stores a boxed one).
                IntPtr boxedEntrypoint = (IntPtr)functionPointer;
                IntPtr unboxedEntrypoint = RuntimeAugments.GetCodeTarget(boxedEntrypoint);
                if (boxedEntrypoint != unboxedEntrypoint)
                {
                    AddImpl((void*)unboxedEntrypoint, stackTraceIp);
                }
                unboxedEntrypoint = RuntimeAugments.GetTargetOfUnboxingAndInstantiatingStub(boxedEntrypoint);
                if (unboxedEntrypoint != 0)
                {
                    AddImpl((void*)unboxedEntrypoint, stackTraceIp);
                }
            }

            foreach (TypeManagerHandle module in RuntimeAugments.GetLoadedModules())
            {
                // First, see if we have any 'outlined' data.
                byte* pUnwindInfo;
                uint pUnwindInfoTotalSize;
                if (RuntimeAugments.FindBlob(module, (int)ReflectionMapBlob.BlobIdWasmPreciseVirtualUnwindInfo, (nint)(&pUnwindInfo), (nint)(&pUnwindInfoTotalSize)))
                {
                    byte* pUnwindInfoEnd = pUnwindInfo + pUnwindInfoTotalSize;
                    while (pUnwindInfo < pUnwindInfoEnd)
                    {
                        void* functionPointer;
                        int unwindInfoSize = RuntimeAugments.ParsePreciseVirtualUnwindInfo(pUnwindInfo, &functionPointer);
                        if (functionPointer != null)
                        {
                            Add(functionPointer, pUnwindInfo);
                        }
                        pUnwindInfo += unwindInfoSize;
                    }
                    Debug.Assert(pUnwindInfo == pUnwindInfoEnd);
                }

                // Thread safety: 'PerModuleMethodNameResolver' is a class, therefore, given we're seeing
                // its non-null published state here, all its fields must be initialized as well (.NET MM).
                PerModuleMethodNameResolver resolver = _perModuleMethodNameResolverHashtable.GetOrCreateValue(module.GetIntPtrUNSAFE());
                foreach (ref PerModuleMethodNameResolver.StackTraceData entry in resolver.GetStackTraceData().AsSpan())
                {
                    pUnwindInfo = (byte*)(uint)entry.Rva;

                    void* functionPointer;
                    RuntimeAugments.ParsePreciseVirtualUnwindInfo(pUnwindInfo, &functionPointer);
                    if (functionPointer != null)
                    {
                        Add(functionPointer, pUnwindInfo);
                    }
                }
            }

            Array.Resize(ref map, index);
            Array.Sort(map);
            return map;
        }

        private static unsafe int ReadMethodIp(byte* pCurrent, out void* pMethodIp)
        {
            if (RuntimeAugments.PreciseVirtualUnwind)
            {
                pMethodIp = pCurrent;
                return RuntimeAugments.ParsePreciseVirtualUnwindInfo(pCurrent);
            }
            return ReadMethodIpForNativeUnwind(pCurrent, out pMethodIp);
        }

        private sealed partial class PerModuleMethodNameResolver
        {
            public StackTraceData[] GetStackTraceData()
            {
                Debug.Assert(_stacktraceDatas != null);
                return _stacktraceDatas;
            }
        }

        private struct FunctionPointerAndStackTraceIp : IComparable<FunctionPointerAndStackTraceIp>
        {
            public IntPtr FunctionPointer;
            public IntPtr StackTraceIp;

            public readonly int CompareTo(FunctionPointerAndStackTraceIp other) => FunctionPointer.CompareTo(other.FunctionPointer);
        }
    }
}
