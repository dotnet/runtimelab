// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using Swift.Runtime.InteropServices;

namespace Swift.Runtime
{
    /// <summary>
    /// Provides functionality to use Swift's Equatable protocol for equality comparison.
    /// </summary>
    public static class SwiftEquatable
    {
        // P/Invoke declaration for Swift's Equatable protocol equality operator
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSQ2eeoiySbx_xtFZTj")]
        private static extern bool PInvoke_SwiftEquals(
            IntPtr lhs,
            IntPtr rhs,
            SwiftSelf typeMetadataInSwiftSelf,
            TypeMetadata typeMetadata,
            ProtocolWitnessTable equatableProtocolWitnessTable);

        /// <summary>
        /// Compares two objects using Swift's Equatable protocol.
        /// </summary>
        /// <typeparam name="T">Type that implements ISwiftEquatable</typeparam>
        /// <param name="lhs">Left-hand side object</param>
        /// <param name="rhs">Right-hand side object</param>
        /// <returns>True if the objects are equal according to Swift's equality</returns>
        public static unsafe bool Equals<T>(T lhs, T rhs)
        {
            if (lhs == null)
                throw new ArgumentNullException(nameof(lhs));
            if (rhs == null)
                throw new ArgumentNullException(nameof(rhs));

            var metadata = TypeMetadata.GetTypeMetadataOrThrow<T>();
            var equatablePwt = ProtocolWitnessTable.GetOrThrow<T, IEquatable<T>>();

            Span<byte> lhsSpan = stackalloc byte[(int)metadata.Size];
            IntPtr lhsPayload = (IntPtr)Unsafe.AsPointer(ref MemoryMarshal.GetReference(lhsSpan));
            Span<byte> rhsSpan = stackalloc byte[(int)metadata.Size];
            IntPtr rhsPayload = (IntPtr)Unsafe.AsPointer(ref MemoryMarshal.GetReference(rhsSpan));

            SwiftMarshal.MarshalToSwift(lhs, ref lhsSpan);
            SwiftMarshal.MarshalToSwift(rhs, ref rhsSpan);

            return PInvoke_SwiftEquals(
                lhsPayload,
                rhsPayload,
                new SwiftSelf(metadata),
                metadata,
                equatablePwt);
        }
    }
}
