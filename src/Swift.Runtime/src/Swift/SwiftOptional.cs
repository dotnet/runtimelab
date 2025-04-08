// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Swift.Runtime;
using Swift.Runtime.InteropServices;

namespace Swift;

/// <summary>
/// Defines the possible cases for an optional type
/// </summary>
public enum SwiftOptionalCases : uint
{
    Some,
    None,
}

/// <summary>
/// Represents a Swift Optional type
/// </summary>
public class SwiftOptional<T> : ISwiftObject
{
    byte[] _payload;

    /// <summary>
    /// Constructs a new empty SwiftOptional
    /// </summary>
    SwiftOptional()
    {
        _payload = new byte[SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata().Size];
    }

    /// <summary>
    /// Returns the TypeMetadata for this object
    /// </summary>
    /// <returns>The TypeMetadata for this object</returns>
    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.Cache.GetOrAdd(typeof(SwiftOptional<T>), _ =>
                PInvokesForSwiftOptional._MetadataAccessor(TypeMetadataRequest.Complete, TypeMetadata.GetTypeMetadataOrThrow<T>()));
    }

    /// <summary>
    /// Creates a new SwiftOptional from a Swift payload
    /// </summary>
    static ISwiftObject ISwiftObject.NewFromPayload(IntPtr payload)
    {
        var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
        var instance = new SwiftOptional<T>();
        unsafe
        {
            fixed (byte* payloadPtr = instance._payload)
            {
                metadata.ValueWitnessTable->InitializeWithCopy(payloadPtr, (byte*)payload, metadata);
                return instance;
            }
        }
    }

    /// <summary>
    /// Marshals this object to a Swift destination
    /// </summary>
    /// <param name="swiftDestSpan"></param>
    /// <returns></returns>
    int ISwiftObject.MarshalToSwift(ref Span<byte> swiftDestSpan)
    {
        var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
        if ((int)metadata.Size > swiftDestSpan.Length)
        {
            throw new ArgumentException($"Span size does not match type size, Expected: {(int)metadata.Size}, Actual: {swiftDestSpan.Length}");
        }
        unsafe
        {
            fixed (byte* payload = _payload)
            fixed (void* swiftDest = swiftDestSpan)
            {
                metadata.ValueWitnessTable->InitializeWithCopy(swiftDest, payload, metadata);
                return (int)metadata.Size;
            }
        }
    }

    /// <summary>
    /// Gets the protocol conformance descriptor for the given type
    /// </summary>
    /// <typeparam name="TProtocol"></typeparam>
    /// <returns></returns>
    static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>()
        where TProtocol : class
    {
        // TODO: https://github.com/dotnet/runtimelab/issues/2963
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a new SwiftOptional with a Some case payload
    /// </summary>
    public static SwiftOptional<T> NewSome(T value)
    {
        unsafe
        {
            var instance = new SwiftOptional<T>();
            fixed (byte* payload = instance._payload)
            {
                var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
                // The additional byte is a discriminator for the enum case
                // https://github.com/swiftlang/swift/blob/8c8ed346edac36f07ece5518f40e35c05e4aa13a/stdlib/public/core/Optional.swift#L121
                Span<byte> payloadSpan = new Span<byte>(payload, (int)metadata.Size - 1);
                SwiftMarshal.MarshalToSwift(value, ref payloadSpan);
                metadata.ValueWitnessTable->DestructiveInjectEnumTag(payload, (uint)SwiftOptionalCases.Some, metadata);
                return instance;
            }
        }
    }

    /// <summary>
    /// Creates a new SwiftOptional with no payload
    /// </summary>
    public static SwiftOptional<T> NewNone()
    {
        unsafe
        {
            var instance = new SwiftOptional<T>();
            fixed (byte* payload = instance._payload)
            {
                var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
                metadata.ValueWitnessTable->DestructiveInjectEnumTag(payload, (uint)SwiftOptionalCases.None, metadata);
                return instance;
            }
        }
    }

    /// <summary>
    /// Gets the case of the optional type
    /// </summary>
    public SwiftOptionalCases Case
    {
        get
        {
            unsafe
            {
                fixed (byte* payload = _payload)
                {
                    var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
                    return (SwiftOptionalCases)metadata.ValueWitnessTable->GetEnumTag(payload, metadata);
                }
            }
        }
    }

    /// <summary>
    /// Gets the value of the optional type if the case is Some
    /// </summary>
    public T Some
    {
        get
        {
            if (Case != SwiftOptionalCases.Some)
            {
                throw new InvalidOperationException("Cannot get Some when case is None");
            }
            var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
            unsafe
            {
                Span<byte> payload = stackalloc byte[_payload.Length];
                _payload.CopyTo(payload);
                fixed (byte* payloadPtr = payload)
                {
                    return SwiftMarshal.MarshalFromSwift<T>(new IntPtr(payloadPtr));
                }
            }
        }
    }

    /// <summary>
    /// Gets the value of the optional type if the case is Some or the default value if the case is None
    /// </summary>
    public T? Value => Case switch
    {
        SwiftOptionalCases.Some => Some,
        SwiftOptionalCases.None => default(T),
        _ => throw new SwiftRuntimeException(string.Format("Unknown case {0}", Case))
    };

    /// <summary>
    /// Returns true if the case is Some
    /// </summary>
    public bool HasValue => Case == SwiftOptionalCases.Some;
}

internal static class PInvokesForSwiftOptional
{
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSqMa")]
    public static extern TypeMetadata _MetadataAccessor(TypeMetadataRequest request, TypeMetadata typeMetadata);
}
