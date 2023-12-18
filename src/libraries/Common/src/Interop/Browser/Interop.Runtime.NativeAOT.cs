// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

internal static partial class Interop
{
    // WARNING: until https://github.com/dotnet/runtime/issues/37955 is fixed
    // make sure that the native side always sets the out parameters
    // otherwise out parameters could stay un-initialized, when the method is used in inlined context
    internal static unsafe partial class Runtime
    {
#pragma warning disable SYSLIB1054
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void ReleaseCSOwnedObject(IntPtr jsHandle);
        [DllImport("*", EntryPoint = "mono_wasm_bind_js_function")]
        public static extern unsafe void BindJSFunction(string function_name, int function_name_length, string module_name, int module_name_length, void* signature, out IntPtr bound_function_js_handle, out int is_exception);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void InvokeJSFunction(IntPtr bound_function_js_handle, void* data);
        [DllImport("*", EntryPoint = "mono_wasm_invoke_import")]
        public static extern unsafe void InvokeImport(IntPtr fn_handle, void* data);
        [DllImport("*", EntryPoint = "mono_wasm_bind_cs_function")]
        public static extern unsafe void BindCSFunction(string fully_qualified_name, int fully_qualified_name_length, int signature_hash, void* signature, out int is_exception);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void ResolveOrRejectPromise(void* data);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern IntPtr RegisterGCRoot(IntPtr start, int bytesSize, IntPtr name);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void DeregisterGCRoot(IntPtr handle);
#pragma warning restore SYSLIB1054

        public static unsafe void BindJSFunction(string function_name, string module_name, void* signature, out IntPtr bound_function_js_handle, out int is_exception, out object result)
        {
            BindJSFunction(function_name, function_name.Length, module_name, module_name.Length, signature, out bound_function_js_handle, out is_exception);
            if (is_exception != 0)
                result = "Runtime.BindJSFunction failed";
            else
                result = "";
        }

        public static unsafe void BindCSFunction(in string fully_qualified_name, int signature_hash, void* signature, out int is_exception, out object result)
        {
            BindCSFunction(fully_qualified_name, fully_qualified_name.Length, signature_hash, signature, out is_exception);
            if (is_exception != 0)
                result = "Runtime.BindCSFunction failed";
            else
                result = "";
        }

        #region Legacy

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void InvokeJSWithArgsRef(IntPtr jsHandle, in string method, in object?[] parms, out int exceptionalResult, out object result);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void GetObjectPropertyRef(IntPtr jsHandle, in string propertyName, out int exceptionalResult, out object result);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void SetObjectPropertyRef(IntPtr jsHandle, in string propertyName, in object? value, bool createIfNotExists, bool hasOwnProperty, out int exceptionalResult, out object result);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void GetByIndexRef(IntPtr jsHandle, int index, out int exceptionalResult, out object result);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void SetByIndexRef(IntPtr jsHandle, int index, in object? value, out int exceptionalResult, out object result);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void GetGlobalObjectRef(in string? globalName, out int exceptionalResult, out object result);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void TypedArrayToArrayRef(IntPtr jsHandle, out int exceptionalResult, out object result);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void CreateCSOwnedObjectRef(in string className, in object[] parms, out int exceptionalResult, out object result);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void TypedArrayFromRef(int arrayPtr, int begin, int end, int bytesPerElement, int type, out int exceptionalResult, out object result);

        #endregion
    }
}
