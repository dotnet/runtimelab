// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using System.Text;
using Swift.Runtime;
using Swift.Runtime.InteropServices;

namespace Swift;

/// <summary>
/// Represents a Swift string with Foundation.Data payload.
/// </summary>
public class SwiftString : ISwiftObject
{
    private static nuint _payloadSize = SwiftObjectHelper<SwiftString>.GetTypeMetadata().Size;

    public struct Buffer
    {
#pragma warning disable CS0169
        private Data _data;
#pragma warning restore CS0169
    }

    private SwiftSafeHandle<SwiftString> _payload;

    public SwiftSafeHandle<SwiftString> Payload => _payload;

    private static Dictionary<Type, string> _protocolConformanceSymbols;

    static SwiftString()
    {
        _protocolConformanceSymbols = new Dictionary<Type, string> { };
    }

    public unsafe PayloadBuffer<SwiftString.Buffer> PayloadBuffer => new PayloadBuffer<SwiftString.Buffer>(_payload);

    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.Cache.GetOrAdd(typeof(SwiftString), _ => PInvoke_getMetadata());
    }

    static ISwiftObject ISwiftObject.NewFromPayload(IntPtr handle)
    {
        return new SwiftString(handle);
    }

    int ISwiftObject.MarshalToSwift(ref Span<byte> swiftDestSpan)
    {
        var metadata = SwiftObjectHelper<SwiftString>.GetTypeMetadata();
        if ((int)metadata.Size > swiftDestSpan.Length)
        {
            throw new ArgumentException($"Span size does not match type size, Expected: {(int)metadata.Size}, Actual: {swiftDestSpan.Length}");
        }
        unsafe
        {
            fixed (void* swiftDest = swiftDestSpan)
            {
                // Ensure the payload is valid before making copy
                bool success = false;
                _payload.DangerousAddRef(ref success);
                try
                {
                    metadata.ValueWitnessTable->InitializeWithCopy(swiftDest, (void*)_payload.DangerousGetHandle(), metadata);
                    return (int)metadata.Size;
                }
                finally
                {
                    if (success)
                        _payload.DangerousRelease();
                }
            }
        }
    }

    /// <summary>
    /// Gets the protocol conformance descriptor for the given type.
    /// </summary>
    /// <typeparam name="TProtocol"></typeparam>
    /// <returns></returns>
    static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>()
        where TProtocol : class
    {
        if (!_protocolConformanceSymbols.TryGetValue(typeof(TProtocol), out var symbolName))
        {
            throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type SwiftString and protocol {typeof(TProtocol).Name}, but no conformance was found.");
        }
        return ProtocolConformanceDescriptor.LoadFromSymbol("/usr/lib/swift/libswiftCore.dylib", symbolName);
    }

    /// <summary>
    /// Constructs a new SwiftString from the given handle.
    /// </summary>
    unsafe SwiftString(IntPtr handle)
    {
        IntPtr bufferPtr = (IntPtr)NativeMemory.Alloc((nuint)sizeof(SwiftString.Buffer));
        *(SwiftString.Buffer*)bufferPtr = *(SwiftString.Buffer*)handle;
        _payload = new SwiftSafeHandle<SwiftString>(bufferPtr);
    }

    /// <summary>
    /// Constructs a new SwiftString from the C# string.
    /// </summary>
    public SwiftString(string str)
    {
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(str);
        unsafe
        {
            fixed (byte* utf8BytesPtr = utf8Bytes)
            {
                var result = PInvoke_Create(utf8BytesPtr, utf8Bytes.Length, 1);
                IntPtr bufferPtr = (IntPtr)NativeMemory.Alloc((nuint)sizeof(SwiftString.Buffer));
                *(SwiftString.Buffer*)bufferPtr = result;
                _payload = new SwiftSafeHandle<SwiftString>(bufferPtr);
            }
        }
    }

    /// <summary>
    /// Gets the length of string.
    /// </summary>
    public int Length
    {
        get
        {
            using PayloadBuffer<SwiftString.Buffer> disposable = PayloadBuffer;
            return (int)PInvoke_GetLength(disposable.Buffer);
        }
    }

    /// <summary>
    /// Converts the SwiftString to a C# string.
    /// </summary>
    public override string ToString()
    {
        var elementType = TypeMetadata.GetTypeMetadataOrThrow<byte>();
        var resultType = TypeMetadata.GetTypeMetadataOrThrow<long>();

        using PayloadBuffer<SwiftString.Buffer> disposable = PayloadBuffer;
        var length = Length;
        if (length <= 0)
            return string.Empty;

        var contiguousArray = PInvoke_GetUtf8ContiguousArray(disposable.Buffer);

#pragma warning disable CS8500
        unsafe
        {
            ToStringCallbackContext callbackContext;
            callbackContext._length = length;
            PInvoke_WithUnsafeBytes(&Callback, &callbackContext, contiguousArray, elementType, resultType);
            return callbackContext._returnString!;

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvSwift) })]
            static IntPtr Callback(byte* bytes, SwiftSelf context)
            {
                ToStringCallbackContext* pContext = (ToStringCallbackContext*)context.Value;
                pContext->_returnString = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(bytes, pContext->_length));
                return default;
            }
        }
#pragma warning restore CS8500
    }

    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSSMa")]
    public static extern TypeMetadata PInvoke_getMetadata();

    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSS21_builtinStringLiteral17utf8CodeUnitCount7isASCIISSBp_BwBi1_tcfC")]
    public static extern unsafe SwiftString.Buffer PInvoke_Create(byte* str, long len, byte isASCII);

    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSS5countSivg")]
    public static extern long PInvoke_GetLength(SwiftString.Buffer str);

    // https://developer.apple.com/documentation/swift/string/utf8cstring
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSS11utf8CStrings15ContiguousArrayVys4Int8VGvg")]
    public static extern IntPtr PInvoke_GetUtf8ContiguousArray(SwiftString.Buffer str);

    // https://developer.apple.com/documentation/swift/contiguousarray/withunsafebytes(_:)
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$ss15ContiguousArrayV15withUnsafeBytesyqd__qd__SWKXEKlF")]
    public static extern unsafe IntPtr PInvoke_WithUnsafeBytes(delegate* unmanaged[Swift]<byte*, SwiftSelf, IntPtr> callback, void* context, IntPtr contiguousArray, TypeMetadata elementType, TypeMetadata resultType);

    private struct ToStringCallbackContext
    {
        public int _length;
        public string _returnString;
    }
}
