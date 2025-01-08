// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using Swift.Runtime;
using Swift.Runtime.InteropServices;

namespace Swift;

/// <summary>
/// Defines the possible cases for an optional type
/// </summary>
public enum SwiftOptionalCases : uint {
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
        return TypeMetadata.Cache.GetOrAdd (typeof(SwiftOptional<T>), _ =>
                PInvokesForSwiftOptional._MetadataAccessor(TypeMetadataRequest.Complete, TypeMetadata.GetTypeMetadataOrThrow<T>()));
    }

    /// <summary>
    /// Creates a new SwiftOptional from a Swift payload
    /// </summary>
    static ISwiftObject ISwiftObject.NewFromPayload(IntPtr payload)
    {
        var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
        var instance = new SwiftOptional<T>();
        unsafe {
            fixed (byte* payloadPtr = instance._payload) {
                metadata.ValueWitnessTable->InitializeWithCopy(payloadPtr, (byte*)payload, metadata);
                return instance;
            }
        }
    }

    /// <summary>
    /// Marshals this object to a Swift destination
    /// </summary>
    /// <param name="swiftDest"></param>
    /// <returns></returns>
    IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
    {
        var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
        unsafe {
            fixed (byte* payload = _payload) {
                metadata.ValueWitnessTable->InitializeWithCopy((void *)swiftDest, payload, metadata);
            }
        }
        return swiftDest;
    }

    /// <summary>
    /// Creates a new SwiftOptional with a Some case payload
    /// </summary>
    public static SwiftOptional<T> NewSome(T value)
    {
        unsafe {
            var instance = new SwiftOptional<T>();
            fixed (byte* payload = instance._payload) {
                var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
                SwiftMarshal.MarshalToSwift(value, new IntPtr(payload));
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
        unsafe {
            var instance = new SwiftOptional<T>();
            fixed (byte* payload = instance._payload) {
                var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
                metadata.ValueWitnessTable->DestructiveInjectEnumTag(payload, (uint)SwiftOptionalCases.None, metadata);
                return instance;
            }
        }
    }

    /// <summary>
    /// Gets the case of the optional type
    /// </summary>
    public SwiftOptionalCases Case {
        get {
            unsafe {
                fixed (byte* payload = _payload) {
                    var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
                    return (SwiftOptionalCases)metadata.ValueWitnessTable->GetEnumTag(payload, metadata);
                }
            }
        }
    }

    /// <summary>
    /// Gets the value of the optional type if the case is Some
    /// </summary>
    public T Some {
        get {
            if (Case != SwiftOptionalCases.Some) {
                throw new InvalidOperationException("Cannot get Some when case is None");
            }
            var metadata = SwiftObjectHelper<SwiftOptional<T>>.GetTypeMetadata();
            unsafe {
                Span<byte> payload = stackalloc byte[_payload.Length];
                _payload.CopyTo(payload);
                fixed (byte* payloadPtr = payload) {
                    metadata.ValueWitnessTable->DestructiveProjectEnumData(payloadPtr, metadata);                  
                    return SwiftMarshal.MarshalFromSwift<T>(new IntPtr (payloadPtr));
                }
            }
        }
    }

    /// <summary>
    /// Gets the value of the optional type if the case is Some or the default value if the case is None
    /// </summary>
    public T? Value => Case switch {
            SwiftOptionalCases.Some => Some,
            SwiftOptionalCases.None => default(T),
            _ => throw new InvalidOperationException("Unknown case")
        };

    /// <summary>
    /// Returns true if the case is Some
    /// </summary>
    public bool HasValue => Case == SwiftOptionalCases.Some;
}

internal static  class PInvokesForSwiftOptional {
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSqMa")]
    public static extern TypeMetadata _MetadataAccessor(TypeMetadataRequest request, TypeMetadata typeMetadata);
}