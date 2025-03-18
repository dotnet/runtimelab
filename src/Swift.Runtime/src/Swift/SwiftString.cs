// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
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
public class SwiftString : IDisposable, ISwiftObject
{
    private static nuint _payloadSize = SwiftObjectHelper<SwiftString>.GetTypeMetadata().Size;

    public struct Buffer
    {
        public Data _payload;
    };

    private Buffer _payload;

    private bool _disposed = false;

    private static Dictionary<Type, string> _protocolConformanceSymbols;

    static SwiftString()
    {
        _protocolConformanceSymbols = new Dictionary<Type, string> { };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            var metadata = SwiftObjectHelper<SwiftString>.GetTypeMetadata();

            unsafe
            {
                fixed (void* payload = &_payload)
                {
                    metadata.ValueWitnessTable->Destroy(payload, metadata);
                }
            }
            _disposed = true;
        }
    }

    ~SwiftString()
    {
        Dispose(disposing: false);
    }

    public static nuint PayloadSize => _payloadSize;

    public Buffer Payload => _payload;

    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.Cache.GetOrAdd(typeof(SwiftString), _ => PInvoke_getMetadata());
    }

    static ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle handle)
    {
        return new SwiftString(handle);
    }

    IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
    {
        var metadata = SwiftObjectHelper<SwiftString>.GetTypeMetadata();
        unsafe
        {
            fixed (void* _payloadPtr = &_payload)
            {
                metadata.ValueWitnessTable->InitializeWithCopy((void*)swiftDest, (void*)_payloadPtr, metadata);
            }
        }
        return swiftDest;
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
    unsafe SwiftString(SwiftHandle handle)
    {
        _payload = *(Buffer*)handle;
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
                _payload = PInvoke_Create(utf8BytesPtr, utf8Bytes.Length, 1);
            }
        }
    }

    /// <summary>
    /// Gets the length of string.
    /// </summary>
    public int Length => (int)PInvoke_GetLength(_payload);

    /// <summary>
    /// Converts the SwiftString to a C# string.
    /// </summary>
    public override string ToString()
    {
        var elementType = TypeMetadata.GetTypeMetadataOrThrow<byte>();
        var resultType = TypeMetadata.GetTypeMetadataOrThrow<long>();

        var length = Length;
        if (length <= 0)
            return string.Empty;

        var contiguousArray = PInvoke_GetUtf8ContiguousArray(_payload);

#pragma warning disable CS8500
        unsafe
        {
            ToStringCallbackContext callbackContext;
            callbackContext._length = length;
            PInvoke_WithUnsafeBytes(&Callback, (IntPtr)(void*)&callbackContext, contiguousArray, elementType, resultType);
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
    [DllImport(KnownLibraries.SwiftCore, CharSet = CharSet.Unicode, EntryPoint = "$sSS21_builtinStringLiteral17utf8CodeUnitCount7isASCIISSBp_BwBi1_tcfC")]
    public static unsafe extern Buffer PInvoke_Create(byte* str, long len, byte flag);

    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSS5countSivg")]
    public static extern long PInvoke_GetLength(Buffer str);

    // https://developer.apple.com/documentation/swift/string/utf8cstring
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSS11utf8CStrings15ContiguousArrayVys4Int8VGvg")]
    public static unsafe extern IntPtr PInvoke_GetUtf8ContiguousArray(Buffer str);

    // https://developer.apple.com/documentation/swift/contiguousarray/withunsafebytes(_:)
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$ss15ContiguousArrayV15withUnsafeBytesyqd__qd__SWKXEKlF")]
    public static extern unsafe IntPtr PInvoke_WithUnsafeBytes(delegate* unmanaged[Swift]<byte*, SwiftSelf, IntPtr> callback, IntPtr context, IntPtr contiguousArray, TypeMetadata elementType, TypeMetadata resultType);

    private struct ToStringCallbackContext
    {
        public int _length;
        public string _returnString;
    }
}
