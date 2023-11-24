﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace System.Runtime.InteropServices.JavaScript
{
    /// <summary>
    /// Represents a bound imported or exported JavaScript function and contains information necessary to invoke it.
    /// This API supports JSImport infrastructure and is not intended to be used directly from your code.
    /// </summary>
    [CLSCompliant(false)]
    [SupportedOSPlatform("browser")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class JSFunctionBinding
    {
        /// <summary>
        /// This API supports JSImport infrastructure and is not intended to be used directly from your code.
        /// </summary>
        internal JSFunctionBinding() { }

        #region intentionally opaque internal structure
        internal unsafe JSBindingHeader* Header;
        internal unsafe JSBindingType* Sigs;// points to first arg, not exception, not result
        internal IntPtr FnHandle;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct JSBindingHeader
        {
            internal const int JSMarshalerSignatureHeaderSize = 4 + 4; // without Exception and Result

            public int Version;
            public int ArgumentCount;
            public JSBindingType Exception;
            public JSBindingType Result;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        internal struct JSBindingType
        {
            internal MarshalerType Type;
            internal MarshalerType __ReservedB1;
            internal MarshalerType __ReservedB2;
            internal MarshalerType __ReservedB3;
            internal IntPtr __Reserved;
            internal IntPtr JSCustomMarshallerCode;
            internal int JSCustomMarshallerCodeLength;
            internal MarshalerType ResultMarshalerType;
            internal MarshalerType __ReservedB4;
            internal MarshalerType __ReservedB5;
            internal MarshalerType __ReservedB6;
            internal MarshalerType Arg1MarshalerType;
            internal MarshalerType __ReservedB7;
            internal MarshalerType __ReservedB8;
            internal MarshalerType __ReservedB9;
            internal MarshalerType Arg2MarshalerType;
            internal MarshalerType __ReservedB10;
            internal MarshalerType __ReservedB11;
            internal MarshalerType __ReservedB12;
            internal MarshalerType Arg3MarshalerType;
            internal MarshalerType __ReservedB13;
            internal MarshalerType __ReservedB14;
            internal MarshalerType __ReservedB15;
        }

        internal unsafe int ArgumentCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Header[0].ArgumentCount;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Header[0].ArgumentCount = value;
            }
        }

        internal unsafe int Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Header[0].Version;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Header[0].Version = value;
            }
        }

        internal unsafe JSBindingType Result
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Header[0].Result;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Header[0].Result = value;
            }
        }

        internal unsafe JSBindingType Exception
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Header[0].Exception;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Header[0].Exception = value;
            }
        }

        // one based position of args, not exception, not result
        internal unsafe JSBindingType this[int position]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Sigs[position - 1];
            }
        }

        #endregion

        /// <summary>
        /// Invokes a previously bound JavaScript function using the provided span to transport argument and return values.
        /// This API supports JSImport infrastructure and is not intended to be used directly from your code.
        /// </summary>
        /// <param name="signature">Generated metadata about the method signature used for marshaling.</param>
        /// <param name="arguments">The intermediate buffer with marshalled arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InvokeJS(JSFunctionBinding signature, Span<JSMarshalerArgument> arguments)
        {
            InvokeImportImpl(signature.FnHandle, arguments);
        }

        /// <summary>
        /// Locates and binds a JavaScript function given name and module so that it can later be invoked by managed callers.
        /// This API supports JSImport infrastructure and is not intended to be used directly from your code.
        /// </summary>
        /// <param name="functionName">The name of the exported JavaScript function.</param>
        /// <param name="moduleName">The name of the ES6 module.</param>
        /// <param name="signatures">The metadata about the signature of the marshaled parameters.</param>
        /// <returns>The method metadata.</returns>
        /// <exception cref="PlatformNotSupportedException">The method is executed on an architecture other than WebAssembly.</exception>
        // JavaScriptExports need to be protected from trimming because they are used from C/JS code which IL linker can't see
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, "System.Runtime.InteropServices.JavaScript.JavaScriptExports", "System.Runtime.InteropServices.JavaScript")]
        // Same for legacy, but the type could be explicitly trimmed by setting WasmEnableLegacyJsInterop=false which would use ILLink.Descriptors.LegacyJsInterop.xml
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, "System.Runtime.InteropServices.JavaScript.LegacyExportsTrimmingRoot", "System.Runtime.InteropServices.JavaScript")]
        public static JSFunctionBinding BindJSFunction(string functionName, string moduleName, ReadOnlySpan<JSMarshalerType> signatures)
        {
            if (RuntimeInformation.OSArchitecture != Architecture.Wasm)
                throw new PlatformNotSupportedException();

            return BindJSFunctionImpl(functionName, moduleName, signatures);
        }

        /// <summary>
        /// Binds a specific managed function wrapper so that it can later be invoked by JavaScript callers.
        /// This API supports JSImport infrastructure and is not intended to be used directly from your code.
        /// </summary>
        /// <param name="fullyQualifiedName">The fully qualified name of the exported method.</param>
        /// <param name="signatureHash">The hash of the signature metadata.</param>
        /// <param name="signatures">The metadata about the signature of the marshaled parameters.</param>
        /// <returns>The method metadata.</returns>
        /// <exception cref="PlatformNotSupportedException">The method is executed on architecture other than WebAssembly.</exception>
        public static JSFunctionBinding BindManagedFunction(string fullyQualifiedName, int signatureHash, ReadOnlySpan<JSMarshalerType> signatures)
        {
            if (RuntimeInformation.OSArchitecture != Architecture.Wasm)
                throw new PlatformNotSupportedException();

            return BindManagedFunctionImpl(fullyQualifiedName, signatureHash, signatures);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void InvokeJSImpl(JSObject jsFunction, Span<JSMarshalerArgument> arguments)
        {
            ObjectDisposedException.ThrowIf(jsFunction.IsDisposed, jsFunction);
#if FEATURE_WASM_THREADS
            JSObject.AssertThreadAffinity(jsFunction);
#endif

            IntPtr functionJSHandle = jsFunction.JSHandle;
            fixed (JSMarshalerArgument* ptr = arguments)
            {
                Interop.Runtime.InvokeJSFunction(functionJSHandle, ptr);
                ref JSMarshalerArgument exceptionArg = ref arguments[0];
                if (exceptionArg.slot.Type != MarshalerType.None)
                {
                    JSHostImplementation.ThrowException(ref exceptionArg);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void InvokeImportImpl(IntPtr fnHandle, Span<JSMarshalerArgument> arguments)
        {
            fixed (JSMarshalerArgument* ptr = arguments)
            {
                Interop.Runtime.InvokeImport(fnHandle, ptr);
                ref JSMarshalerArgument exceptionArg = ref arguments[0];
                if (exceptionArg.slot.Type != MarshalerType.None)
                {
                    JSHostImplementation.ThrowException(ref exceptionArg);
                }
            }
        }

        internal static unsafe JSFunctionBinding BindJSFunctionImpl(string functionName, string moduleName, ReadOnlySpan<JSMarshalerType> signatures)
        {
#if FEATURE_WASM_THREADS
            JSSynchronizationContext.AssertWebWorkerContext();
#endif

            var signature = JSHostImplementation.GetMethodSignature(signatures);

            Interop.Runtime.BindJSFunction(functionName, functionName.Length, moduleName, moduleName.Length, signature.Header, out IntPtr jsFunctionHandle, out int isException);
            if (isException != 0)
                throw new JSException("Runtime.BindJSFunction failed");

            signature.FnHandle = jsFunctionHandle;

            JSHostImplementation.FreeMethodSignatureBuffer(signature);

            return signature;
        }

        internal static unsafe JSFunctionBinding BindManagedFunctionImpl(string fullyQualifiedName, int signatureHash, ReadOnlySpan<JSMarshalerType> signatures)
        {
            var signature = JSHostImplementation.GetMethodSignature(signatures);

            Interop.Runtime.BindCSFunction(fullyQualifiedName, fullyQualifiedName.Length, signatureHash, signature.Header, out int isException);
            if (isException != 0)
            {
                throw new JSException("Runtime.BindCSFunction failed");
            }

            JSHostImplementation.FreeMethodSignatureBuffer(signature);

            return signature;
        }
    }
}
