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
public class SwiftString : IDisposable, ISwiftObject
{
    private static nuint _payloadSize = SwiftObjectHelper<SwiftString>.GetTypeMetadata().Size;

    public struct TypeBuffer
    {
        public long _flags;
        public IntPtr _object;
    }

    private TypeBuffer _buffer;

    private SwiftHandle _payload;

    public SwiftHandle Payload => _payload;

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
        if (!_payload.IsInvalid)
        {
            unsafe
            {
                _payload.SetMetadata(SwiftObjectHelper<SwiftString>.GetTypeMetadata());
                fixed (void* buffer = &_buffer)
                {
                    _payload.Handle = (IntPtr)buffer;
                    _payload.Dispose();
                }
            }
        }
    }

    ~SwiftString()
    {
        Dispose(disposing: false);
    }

    public static nuint PayloadSize => _payloadSize;

    public TypeBuffer Buffer => _buffer;

    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.Cache.GetOrAdd(typeof(SwiftString), _ => PInvoke_getMetadata());
    }

    static ISwiftObject ISwiftObject.NewFromPayload(IntPtr handle)
    {
        return new SwiftString(handle);
    }

    void ISwiftObject.MarshalToSwift(Span<byte> swiftDestSpan)
    {
        var metadata = SwiftObjectHelper<SwiftString>.GetTypeMetadata();
        Debug.Assert((int)metadata.Size == swiftDestSpan.Length, $"Span size does not match type size, Expected: {(int)metadata.Size}, Actual: {swiftDestSpan.Length}");
        unsafe
        {
            fixed (void* buffer = &_buffer)
            fixed (void* swiftDest = swiftDestSpan)
            {
                metadata.ValueWitnessTable->InitializeWithCopy((void*)swiftDest, buffer, metadata);
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
        _buffer = *(TypeBuffer*)handle;
        fixed (void* buffer = &_buffer)
            _payload = new SwiftHandle((IntPtr)buffer);
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
                _buffer = PInvoke_Create(utf8BytesPtr, utf8Bytes.Length, 1);
                fixed (void* buffer = &_buffer)
                    _payload = new SwiftHandle((IntPtr)buffer);
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
            bool _success = false;
            _payload.DangerousAddRef(ref _success);
            int result = (int)PInvoke_GetLength(_buffer);
            if (_success)
                _payload.DangerousRelease();
            return result;
        }
    }

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

        bool _success = false;
        _payload.DangerousAddRef(ref _success);

        var contiguousArray = PInvoke_GetUtf8ContiguousArray(_buffer);

#pragma warning disable CS8500
        unsafe
        {
            ToStringCallbackContext callbackContext;
            callbackContext._length = length;
            PInvoke_WithUnsafeBytes(&Callback, (IntPtr)(void*)&callbackContext, contiguousArray, elementType, resultType);
            if (_success)
                _payload.DangerousRelease();
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
    public static unsafe extern TypeBuffer PInvoke_Create(byte* str, long len, byte flag);

    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSS5countSivg")]
    public static extern long PInvoke_GetLength(TypeBuffer str);

    // https://developer.apple.com/documentation/swift/string/utf8cstring
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSS11utf8CStrings15ContiguousArrayVys4Int8VGvg")]
    public static unsafe extern IntPtr PInvoke_GetUtf8ContiguousArray(TypeBuffer str);

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
