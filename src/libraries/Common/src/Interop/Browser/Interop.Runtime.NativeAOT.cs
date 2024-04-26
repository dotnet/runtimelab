// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

internal static partial class Interop
{
    internal static unsafe partial class Runtime
    {
        private const string JSLibrary = "System.Runtime.InteropServices.JavaScript";

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void ReleaseCSOwnedObject(IntPtr jsHandle);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_bind_js_import_ST", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial IntPtr BindJSImportST(void* signature);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_invoke_jsimport_ST")]
        public static unsafe partial IntPtr InvokeJSImportST(int importHandle, nint args);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void InvokeJSFunction(IntPtr bound_function_js_handle, nint data);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_invoke_js_import", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial void InvokeJSImport(IntPtr fn_handle, nint data);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_bind_cs_function", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial void BindCSFunction(string fully_qualified_name, int fully_qualified_name_length, int signature_hash, void* signature, out int is_exception);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_resolve_or_reject_promise", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial void ResolveOrRejectPromise(nint data);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern IntPtr RegisterGCRoot(void* start, int bytesSize, IntPtr name);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void DeregisterGCRoot(IntPtr handle);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void CancelPromise(IntPtr gcHandle);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void AssemblyGetEntryPoint(IntPtr assemblyNamePtr, int auto_insert_breakpoint, void** monoMethodPtrPtr);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void BindAssemblyExports(IntPtr assemblyNamePtr);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void GetAssemblyExport(IntPtr assemblyNamePtr, IntPtr namespacePtr, IntPtr classnamePtr, IntPtr methodNamePtr, IntPtr* monoMethodPtrPtr);

        public static unsafe void BindCSFunction(in string fully_qualified_name, int signature_hash, void* signature, out int is_exception, out object result)
        {
            BindCSFunction(fully_qualified_name, fully_qualified_name.Length, signature_hash, signature, out is_exception);
            if (is_exception != 0)
                result = "Runtime.BindCSFunction failed"; // TODO-LLVM-JSInterop: Marshal exception message
            else
                result = "";
        }
    }
}
