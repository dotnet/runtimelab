// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class Runtime
    {
        private const string JSLibrary = "System.Runtime.InteropServices.JavaScript";

        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_release_cs_owned_object", StringMarshalling = StringMarshalling.Utf16)]
        internal static unsafe partial void ReleaseCSOwnedObject(IntPtr jsHandle);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_bind_js_import_ST", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial IntPtr BindJSImportST(void* signature);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_invoke_jsimport_ST")]
        public static unsafe partial IntPtr InvokeJSImportST(int importHandle, nint args);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_invoke_js_function")]
        public static unsafe partial void InvokeJSFunction(IntPtr bound_function_js_handle, nint data);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_invoke_js_import", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial void InvokeJSImport(IntPtr fn_handle, nint data);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_bind_cs_function", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial void BindCSFunction(string fully_qualified_name, int fully_qualified_name_length, int signature_hash, void* signature, out int is_exception);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_resolve_or_reject_promise", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial void ResolveOrRejectPromise(nint data);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_cancel_promise", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial void CancelPromise(IntPtr gcHandle);

        #region Not used by NativeAOT
        public static IntPtr RegisterGCRoot(void* start, int bytesSize, IntPtr name) => throw new NotImplementedException();
        public static void DeregisterGCRoot(IntPtr handle) => throw new NotImplementedException();
        public static void AssemblyGetEntryPoint(IntPtr assemblyNamePtr, int auto_insert_breakpoint, void** monoMethodPtrPtr) => throw new NotImplementedException();
        public static void BindAssemblyExports(IntPtr assemblyNamePtr) => throw new NotImplementedException();
        public static void GetAssemblyExport(IntPtr assemblyNamePtr, IntPtr namespacePtr, IntPtr classnamePtr, IntPtr methodNamePtr, IntPtr* monoMethodPtrPtr) => throw new NotImplementedException();
        #endregion
    }
}
