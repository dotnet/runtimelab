// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    public static partial class Marshal
    {
        internal static int SizeOfHelper(Type t, bool throwIfNotMarshalable)
        {
            Debug.Assert(throwIfNotMarshalable);
            return RuntimeAugments.InteropCallbacks.GetStructUnsafeStructSize(t.TypeHandle);
        }

        public static IntPtr OffsetOf(Type t, string fieldName)
        {
            if (t == null)
                throw new ArgumentNullException(nameof(t));

            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));

            if (t.TypeHandle.IsGenericTypeDefinition())
                throw new ArgumentException(SR.Argument_NeedNonGenericType, nameof(t));

            return new IntPtr(RuntimeAugments.InteropCallbacks.GetStructFieldOffset(t.TypeHandle, fieldName));
        }

        private static object PtrToStructureHelper(IntPtr ptr, Type structureType)
        {
            object boxedStruct = InteropExtensions.RuntimeNewObject(structureType.TypeHandle);
            PtrToStructureImpl(ptr, boxedStruct);
            return boxedStruct;
        }

        private static void PtrToStructureHelper(IntPtr ptr, object structure, bool allowValueClasses)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));

            if (structure == null)
                throw new ArgumentNullException(nameof(structure));

            if (!allowValueClasses && structure.EETypePtr.IsValueType)
            {
                throw new ArgumentException(nameof(structure), SR.Argument_StructMustNotBeValueClass);
            }

            PtrToStructureImpl(ptr, structure);
        }

        internal static unsafe void PtrToStructureImpl(IntPtr ptr, object structure)
        {
            RuntimeTypeHandle structureTypeHandle = structure.GetType().TypeHandle;

            IntPtr unmarshalStub;
            if (structureTypeHandle.IsBlittable())
            {
                if (!RuntimeAugments.InteropCallbacks.TryGetStructUnmarshalStub(structureTypeHandle, out unmarshalStub))
                {
                    unmarshalStub = IntPtr.Zero;
                }
            }
            else
            {
                unmarshalStub = RuntimeAugments.InteropCallbacks.GetStructUnmarshalStub(structureTypeHandle);
            }

            if (unmarshalStub != IntPtr.Zero)
            {
                if (structureTypeHandle.IsValueType())
                {
                    ((delegate*<ref byte, ref byte, void>)unmarshalStub)(ref *(byte*)ptr, ref structure.GetRawData());
                }
                else
                {
                    ((delegate*<ref byte, object, void>)unmarshalStub)(ref *(byte*)ptr, structure);
                }
            }
            else
            {
                nuint size = (nuint)RuntimeAugments.InteropCallbacks.GetStructUnsafeStructSize(structureTypeHandle);

                Buffer.Memmove(ref structure.GetRawData(), ref *(byte*)ptr, size);
            }
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available. Use the DestroyStructure<T> overload instead.")]
        public static unsafe void DestroyStructure(IntPtr ptr, Type structuretype)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));

            if (structuretype == null)
                throw new ArgumentNullException(nameof(structuretype));

            RuntimeTypeHandle structureTypeHandle = structuretype.TypeHandle;

            if (structureTypeHandle.IsGenericType() || structureTypeHandle.IsGenericTypeDefinition())
                throw new ArgumentException(SR.Argument_NeedNonGenericType, nameof(structuretype));

            if (structureTypeHandle.IsEnum() ||
                structureTypeHandle.IsInterface() ||
                InteropExtensions.AreTypesAssignable(typeof(Delegate).TypeHandle, structureTypeHandle))
            {
                throw new ArgumentException(SR.Format(SR.Argument_MustHaveLayoutOrBeBlittable, structureTypeHandle.LastResortToString));
            }

            if (structureTypeHandle.IsBlittable())
            {
                // ok to call with blittable structure, but no work to do in this case.
                return;
            }

            IntPtr destroyStructureStub = RuntimeAugments.InteropCallbacks.GetDestroyStructureStub(structureTypeHandle, out bool hasInvalidLayout);
            if (hasInvalidLayout)
                throw new ArgumentException(SR.Format(SR.Argument_MustHaveLayoutOrBeBlittable, structureTypeHandle.LastResortToString));
            // DestroyStructureStub == IntPtr.Zero means its fields don't need to be destroyed
            if (destroyStructureStub != IntPtr.Zero)
            {
                ((delegate*<ref byte, void>)destroyStructureStub)(ref *(byte*)ptr);
            }
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available. Use the StructureToPtr<T> overload instead.")]
        public static unsafe void StructureToPtr(object structure, IntPtr ptr, bool fDeleteOld)
        {
            if (structure == null)
                throw new ArgumentNullException(nameof(structure));

            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));

            if (fDeleteOld)
            {
                DestroyStructure(ptr, structure.GetType());
            }

            RuntimeTypeHandle structureTypeHandle = structure.GetType().TypeHandle;

            if (structureTypeHandle.IsGenericType() || structureTypeHandle.IsGenericTypeDefinition())
            {
                throw new ArgumentException(SR.Argument_NeedNonGenericObject, nameof(structure));
            }

            IntPtr marshalStub;
            if (structureTypeHandle.IsBlittable())
            {
                if (!RuntimeAugments.InteropCallbacks.TryGetStructMarshalStub(structureTypeHandle, out marshalStub))
                {
                    marshalStub = IntPtr.Zero;
                }
            }
            else
            {
                marshalStub = RuntimeAugments.InteropCallbacks.GetStructMarshalStub(structureTypeHandle);
            }

            if (marshalStub != IntPtr.Zero)
            {
                if (structureTypeHandle.IsValueType())
                {
                    ((delegate*<ref byte, ref byte, void>)marshalStub)(ref structure.GetRawData(), ref *(byte*)ptr);
                }
                else
                {
                    ((delegate*<object, ref byte, void>)marshalStub)(structure, ref *(byte*)ptr);
                }
            }
            else
            {
                nuint size = (nuint)RuntimeAugments.InteropCallbacks.GetStructUnsafeStructSize(structureTypeHandle);

                Buffer.Memmove(ref *(byte*)ptr, ref structure.GetRawData(), size);
            }
        }

        internal static Exception GetExceptionForHRInternal(int errorCode, IntPtr errorInfo)
        {
            switch (errorCode)
            {
                case HResults.S_OK:
                case HResults.S_FALSE:
                    return null;
                case HResults.COR_E_AMBIGUOUSMATCH:
                    return new System.Reflection.AmbiguousMatchException();
                case HResults.COR_E_APPLICATION:
                    return new System.ApplicationException();
                case HResults.COR_E_ARGUMENT:
                    return new System.ArgumentException();
                case HResults.COR_E_ARGUMENTOUTOFRANGE:
                    return new System.ArgumentOutOfRangeException();
                case HResults.COR_E_ARITHMETIC:
                    return new System.ArithmeticException();
                case HResults.COR_E_ARRAYTYPEMISMATCH:
                    return new System.ArrayTypeMismatchException();
                case HResults.COR_E_BADEXEFORMAT:
                    return new System.BadImageFormatException();
                case HResults.COR_E_BADIMAGEFORMAT:
                    return new System.BadImageFormatException();
                //case HResults.COR_E_CODECONTRACTFAILED:
                //return new System.Diagnostics.Contracts.ContractException ();
                //case HResults.COR_E_COMEMULATE:
                case HResults.COR_E_CUSTOMATTRIBUTEFORMAT:
                    return new System.Reflection.CustomAttributeFormatException();
                case HResults.COR_E_DATAMISALIGNED:
                    return new System.DataMisalignedException();
                case HResults.COR_E_DIRECTORYNOTFOUND:
                    return new System.IO.DirectoryNotFoundException();
                case HResults.COR_E_DIVIDEBYZERO:
                    return new System.DivideByZeroException();
                case HResults.COR_E_DLLNOTFOUND:
                    return new System.DllNotFoundException();
                case HResults.COR_E_DUPLICATEWAITOBJECT:
                    return new System.DuplicateWaitObjectException();
                case HResults.COR_E_ENDOFSTREAM:
                    return new System.IO.EndOfStreamException();
                case HResults.COR_E_ENTRYPOINTNOTFOUND:
                    return new System.EntryPointNotFoundException();
                case HResults.COR_E_EXCEPTION:
                    return new System.Exception();
                case HResults.COR_E_EXECUTIONENGINE:
#pragma warning disable 618
                    return new System.ExecutionEngineException();
#pragma warning restore 618
                case HResults.COR_E_FIELDACCESS:
                    return new System.FieldAccessException();
                case HResults.COR_E_FILELOAD:
                    return new System.IO.FileLoadException();
                case HResults.COR_E_FILENOTFOUND:
                    return new System.IO.FileNotFoundException();
                case HResults.COR_E_FORMAT:
                    return new System.FormatException();
                case HResults.COR_E_INDEXOUTOFRANGE:
                    return new System.IndexOutOfRangeException();
                case HResults.COR_E_INSUFFICIENTEXECUTIONSTACK:
                    return new System.InsufficientExecutionStackException();
                case HResults.COR_E_INVALIDCAST:
                    return new System.InvalidCastException();
                case HResults.COR_E_INVALIDFILTERCRITERIA:
                    return new System.Reflection.InvalidFilterCriteriaException();
                case HResults.COR_E_INVALIDOLEVARIANTTYPE:
                    return new System.Runtime.InteropServices.InvalidOleVariantTypeException();
                case HResults.COR_E_INVALIDOPERATION:
                    return new System.InvalidOperationException();
                case HResults.COR_E_INVALIDPROGRAM:
                    return new System.InvalidProgramException();
                case HResults.COR_E_IO:
                    return new System.IO.IOException();
                case HResults.COR_E_MARSHALDIRECTIVE:
                    return new System.Runtime.InteropServices.MarshalDirectiveException();
                case HResults.COR_E_MEMBERACCESS:
                    return new System.MemberAccessException();
                case HResults.COR_E_METHODACCESS:
                    return new System.MethodAccessException();
                case HResults.COR_E_MISSINGFIELD:
                    return new System.MissingFieldException();
                case HResults.COR_E_MISSINGMANIFESTRESOURCE:
                    return new System.Resources.MissingManifestResourceException();
                case HResults.COR_E_MISSINGMEMBER:
                    return new System.MissingMemberException();
                case HResults.COR_E_MISSINGMETHOD:
                    return new System.MissingMethodException();
                case HResults.COR_E_MULTICASTNOTSUPPORTED:
                    return new System.MulticastNotSupportedException();
                case HResults.COR_E_NOTFINITENUMBER:
                    return new System.NotFiniteNumberException();
                case HResults.COR_E_NOTSUPPORTED:
                    return new System.NotSupportedException();
                case HResults.E_POINTER:
                    return new System.NullReferenceException();
                case HResults.COR_E_OBJECTDISPOSED:
                    return new System.ObjectDisposedException("");
                case HResults.COR_E_OPERATIONCANCELED:
                    return new System.OperationCanceledException();
                case HResults.COR_E_OUTOFMEMORY:
                    return new System.OutOfMemoryException();
                case HResults.COR_E_OVERFLOW:
                    return new System.OverflowException();
                case HResults.COR_E_PATHTOOLONG:
                    return new System.IO.PathTooLongException();
                case HResults.COR_E_PLATFORMNOTSUPPORTED:
                    return new System.PlatformNotSupportedException();
                case HResults.COR_E_RANK:
                    return new System.RankException();
                case HResults.COR_E_REFLECTIONTYPELOAD:
                    return new System.MissingMethodException();
                case HResults.COR_E_RUNTIMEWRAPPED:
                    return new System.MissingMethodException();
                case HResults.COR_E_SECURITY:
                    return new System.Security.SecurityException();
                case HResults.COR_E_SERIALIZATION:
                    return new System.Runtime.Serialization.SerializationException();
                case HResults.COR_E_STACKOVERFLOW:
                    return new System.StackOverflowException();
                case HResults.COR_E_SYNCHRONIZATIONLOCK:
                    return new System.Threading.SynchronizationLockException();
                case HResults.COR_E_SYSTEM:
                    return new System.SystemException();
                case HResults.COR_E_TARGET:
                    return new System.Reflection.TargetException();
                case HResults.COR_E_TARGETINVOCATION:
                    return new System.MissingMethodException();
                case HResults.COR_E_TARGETPARAMCOUNT:
                    return new System.Reflection.TargetParameterCountException();
                case HResults.COR_E_THREADABORTED:
                    return new System.Threading.ThreadAbortException();
                case HResults.COR_E_THREADINTERRUPTED:
                    return new System.Threading.ThreadInterruptedException();
                case HResults.COR_E_THREADSTART:
                    return new System.Threading.ThreadStartException();
                case HResults.COR_E_THREADSTATE:
                    return new System.Threading.ThreadStateException();
                case HResults.COR_E_TYPEACCESS:
                    return new System.TypeAccessException();
                case HResults.COR_E_TYPEINITIALIZATION:
                    return new System.TypeInitializationException("");
                case HResults.COR_E_TYPELOAD:
                    return new System.TypeLoadException();
                case HResults.COR_E_TYPEUNLOADED:
                    return new System.TypeUnloadedException();
                case HResults.COR_E_UNAUTHORIZEDACCESS:
                    return new System.UnauthorizedAccessException();
                //case HResults.COR_E_UNSUPPORTEDFORMAT:
                case HResults.COR_E_VERIFICATION:
                    return new System.Security.VerificationException();
                //case HResults.E_INVALIDARG:
                case HResults.E_NOTIMPL:
                    return new System.NotImplementedException();
                //case HResults.E_POINTER:
                case HResults.RO_E_CLOSED:
                    return new System.ObjectDisposedException("");

                default:
                    return new COMException("", errorCode);
            }
        }

        private static void PrelinkCore(MethodInfo m)
        {
            // Note: This method is effectively a no-op in ahead-of-time compilation scenarios. In CoreCLR and Desktop, this will pre-generate
            // the P/Invoke, but everything is pre-generated in CoreRT.
        }

        internal static Delegate GetDelegateForFunctionPointerInternal(IntPtr ptr, Type t)
        {
            return PInvokeMarshal.GetDelegateForFunctionPointer(ptr, t.TypeHandle);
        }

        internal static IntPtr GetFunctionPointerForDelegateInternal(Delegate d)
        {
            return PInvokeMarshal.GetFunctionPointerForDelegate(d);
        }

        public static int GetLastWin32Error()
        {
            return PInvokeMarshal.GetLastWin32Error();
        }

        internal static void SetLastWin32Error(int errorCode)
        {
            PInvokeMarshal.SetLastWin32Error(errorCode);
        }

        internal static bool IsPinnable(object o)
        {
            return (o == null) || o.EETypePtr.MightBeBlittable();
        }

        public static int GetExceptionCode()
        {
            // Obsolete
            throw new PlatformNotSupportedException();
        }

        public static IntPtr GetExceptionPointers()
        {
            throw new PlatformNotSupportedException();
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available")]
        public static unsafe byte ReadByte(object ptr, int ofs)
        {
            return ReadValueSlow<byte>(ptr, ofs, &ReadByte);
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available")]
        public static unsafe short ReadInt16(object ptr, int ofs)
        {
            return ReadValueSlow<short>(ptr, ofs, &ReadInt16);
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available")]
        public static unsafe int ReadInt32(object ptr, int ofs)
        {
            return ReadValueSlow<int>(ptr, ofs, &ReadInt32);
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available")]
        public static unsafe long ReadInt64(object ptr, int ofs)
        {
            return ReadValueSlow<long>(ptr, ofs, &ReadInt64);
        }

        //====================================================================
        // Read value from marshaled object (marshaled using AsAny)
        // It's quite slow and can return back dangling pointers
        // It's only there for backcompact
        // People should instead use the IntPtr overloads
        //====================================================================
        [RequiresDynamicCode("Marshalling code for the object might not be available")]
        private static unsafe T ReadValueSlow<T>(object ptr, int ofs, delegate*<IntPtr, int, T> readValueHelper)
        {
            // Consumers of this method are documented to throw AccessViolationException on any AV
            if (ptr is null)
            {
                throw new AccessViolationException();
            }

            if (ptr.EETypePtr.IsArray ||
                ptr is string ||
                ptr is StringBuilder)
            {
                // We could implement these if really needed.
                throw new PlatformNotSupportedException();
            }

            // We are going to assume this is a Sequential or Explicit layout type because
            // we don't want to touch reflection metadata for this.
            // If we're wrong, this will throw the exception we get for missing interop data
            // instead of an ArgumentException.
            // That's quite acceptable for an obsoleted API.

            Type structType = ptr.GetType();

            int size = SizeOf(structType);

            // Compat note: CLR wouldn't bother with a range check. If someone does this,
            // they're likely taking dependency on some CLR implementation detail quirk.
            if (checked(ofs + Unsafe.SizeOf<T>()) > size)
                throw new ArgumentOutOfRangeException(nameof(ofs));

            IntPtr nativeBytes = AllocCoTaskMem(size);
            Buffer.ZeroMemory((byte*)nativeBytes, (nuint)size);

            try
            {
                StructureToPtr(ptr, nativeBytes, false);
                return readValueHelper(nativeBytes, ofs);
            }
            finally
            {
                DestroyStructure(nativeBytes, structType);
                FreeCoTaskMem(nativeBytes);
            }
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available")]
        public static unsafe void WriteByte(object ptr, int ofs, byte val)
        {
            WriteValueSlow(ptr, ofs, val, &WriteByte);
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available")]
        public static unsafe void WriteInt16(object ptr, int ofs, short val)
        {
            WriteValueSlow(ptr, ofs, val, &WriteInt16);
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available")]
        public static unsafe void WriteInt32(object ptr, int ofs, int val)
        {
            WriteValueSlow(ptr, ofs, val, &WriteInt32);
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available")]
        public static unsafe void WriteInt64(object ptr, int ofs, long val)
        {
            WriteValueSlow(ptr, ofs, val, &WriteInt64);
        }

        [RequiresDynamicCode("Marshalling code for the object might not be available")]
        private static unsafe void WriteValueSlow<T>(object ptr, int ofs, T val, delegate*<IntPtr, int, T, void> writeValueHelper)
        {
            // Consumers of this method are documented to throw AccessViolationException on any AV
            if (ptr is null)
            {
                throw new AccessViolationException();
            }

            if (ptr.EETypePtr.IsArray ||
                ptr is string ||
                ptr is StringBuilder)
            {
                // We could implement these if really needed.
                throw new PlatformNotSupportedException();
            }

            // We are going to assume this is a Sequential or Explicit layout type because
            // we don't want to touch reflection metadata for this.
            // If we're wrong, this will throw the exception we get for missing interop data
            // instead of an ArgumentException.
            // That's quite acceptable for an obsoleted API.

            Type structType = ptr.GetType();

            int size = SizeOf(structType);

            // Compat note: CLR wouldn't bother with a range check. If someone does this,
            // they're likely taking dependency on some CLR implementation detail quirk.
            if (checked(ofs + Unsafe.SizeOf<T>()) > size)
                throw new ArgumentOutOfRangeException(nameof(ofs));

            IntPtr nativeBytes = AllocCoTaskMem(size);
            Buffer.ZeroMemory((byte*)nativeBytes, (nuint)size);

            try
            {
                StructureToPtr(ptr, nativeBytes, false);
                writeValueHelper(nativeBytes, ofs, val);
                PtrToStructureImpl(nativeBytes, ptr);
            }
            finally
            {
                DestroyStructure(nativeBytes, structType);
                FreeCoTaskMem(nativeBytes);
            }
        }
    }
}
