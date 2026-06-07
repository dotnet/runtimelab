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

        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_invoke_js_import", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial void InvokeJSImport(IntPtr fn_handle, nint data);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_bind_cs_function", StringMarshalling = StringMarshalling.Utf16)]
        public static unsafe partial void BindCSFunction(string fully_qualified_name, int fully_qualified_name_length, int signature_hash, void* signature, out int is_exception);

#if FEATURE_WASM_MANAGED_THREADS
        // Required by JavaScript/JSFunctionBinding.cs
        [LibraryImport(JSLibrary, EntryPoint = "SystemInteropJS_ReleaseCSOwnedObjectPost", StringMarshalling = StringMarshalling.Utf16)]
        internal static unsafe partial void ReleaseCSOwnedObjectPost(nint targetNativeTID, IntPtr jsHandle);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_invoke_js_function_send")]
        public static unsafe partial void InvokeJSFunctionSend(nint targetNativeTID, nint functionHandle, nint data);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_invoke_jsimport_MT")]
        public static unsafe partial void InvokeJSImportSync(nint signature, nint args);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_invoke_jsimport_sync_send")]
        public static unsafe partial void InvokeJSImportSyncSend(nint targetNativeTID, nint signature, nint args);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_invoke_jsimport_async_post")]
        public static unsafe partial void InvokeJSImportAsyncPost(nint targetNativeTID, nint signature, nint args);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_resolve_or_reject_promise_post")]
        public static unsafe partial void ResolveOrRejectPromisePost(nint targetNativeTID, nint data);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_install_js_worker_interop_wrapper")]
        public static unsafe partial void InstallWebWorkerInterop(nint proxyContextGCHandle, void* beforeSyncJSImport, void* afterSyncJSImport, void* pumpHandler);
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_uninstall_js_worker_interop")]
        public static unsafe partial void UninstallWebWorkerInterop();
        // Required by JavaScript/CancelablePromise.cs
        [LibraryImport(JSLibrary, EntryPoint = "mono_wasm_cancel_promise_post")]
        public static unsafe partial void CancelPromisePost(nint targetNativeTID, nint taskHolderGCHandle);
#endif
    }
}
