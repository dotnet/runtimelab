// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System;
using System.Runtime.InteropServices;

namespace Swift.Runtime
{
    /// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/ABI/MetadataValues.h#L117
	/// </summary>
	[Flags]
	public enum ValueWitnessFlags
	{
		AlignmentMask = 0x0000FFFF,
		IsNonPOD = 0x00010000,
		IsNonInline = 0x00020000,
		HasSpareBits = 0x00080000,
		IsNonBitwiseTakable = 0x00100000,
		HasEnumWitnesses = 0x00200000,
		Incomplete = 0x00400000,
	}

	/// <summary>
	/// See https://github.com/apple/swift/blob/main/include/swift/ABI/ValueWitness.def
	/// </summary>
	[StructLayout (LayoutKind.Sequential)]
	public unsafe ref struct ValueWitnessTable
	{
		/// <summary>
		/// void *InitializeBufferWithCopyOfBuffer (dest, src, metadata)
		/// Initialize an invalid buffer dest with a copy of src. Returns dest.
		/// </summary>
		public delegate* unmanaged<void *, void *, TypeMetadata, void *> InitializeBufferWithCopyOfBuffer;

		/// <summary>
		/// void Destroy (object, witnessTable)
		/// Destroy the type pointed to by object leaving it invalid.
		/// </summary>
		public delegate* unmanaged<void *, ValueWitnessTable *, void> Destroy;

		/// <summary>
		/// void *InitializeWithCopy (dest, src, metadata)
		/// Initialize object dest with a copy of source. Returns dest.
		/// </summary>
		public delegate* unmanaged<void *, void *, TypeMetadata, void *> InitializeWithCopy;

		/// <summary>
		/// void *AssignWithCopy (dest, src, metadata)
		/// Overwrite an existing object, dest, with a copy of source, destroying the exisiting
		/// value in dest. Returns dest.
		/// </summary>
		public delegate* unmanaged<void *, void *, TypeMetadata, void *> AssignWithCopy;

		/// <summary>
		/// void *InitializeWithTake (dest, src, metadata)
		/// Initialize an invalid object dest with a copy of source, destroying src.
		/// Returns dest.
		/// </summary>
		public delegate* unmanaged<void *, void *, TypeMetadata, void *> InitializeWithTake;

		/// <summary>
		/// void *AssignWithTake (dest, src, metadata)
		/// Overwrite an existing object, dest, with a copy of source, destroying the existing
		/// value in dest and then destroying src after the copy. Returns dest.
		/// </summary>
		public delegate* unmanaged<void *, void *, TypeMetadata, void *> AssignWithTake;

		/// <summary>
		/// nuint GetEnumTagSinglePayload (enum, emptyCases, metadata)
		/// Given an instance of a valid single payload enum whose type is represented by
		/// metadata, get the tag of the enum.
		/// </summary>
		public delegate* unmanaged<void *, nuint, TypeMetadata, nuint> GetEnumTagSinglePayload;

		/// <summary>
		/// void StoreEnumTagSinglePayload (enum, whichCase, emptyCases, metadata)
		/// Given uninitialized memory for an instance of a single payload enum with a payload
		/// whose is represented by metadata, store the tag.
		/// </summary>
		public delegate* unmanaged<void *, nuint, nuint, TypeMetadata, void> StoreEnumTagSinglePayload;

		/// <summary>
		/// The size of the type in bytes.
		/// </summary>
		public nuint Size;

		/// <summary>
		/// The stride of the type in bytes.
		/// </summary>
		public nuint Stride;

		/// <summary>
		/// Flags describing the type represented by the witness table.
		/// </summary>
		public ValueWitnessFlags Flags;

		/// <summary>
		/// The number of extra inhabitants in the type.
		/// </summary>
		public uint ExtraInhabitantCount;

		/// <summary>
		/// Returns a tag for the given enumeration. The tags start at 0 with the first
		/// case with a payload and continue in declaration order. Then they are followed by
		/// cases without payloads in declaration order.
		/// </summary>
		public delegate* unmanaged<void *, TypeMetadata, nuint> GetEnumTag;

		/// <summary>
		/// Strips out the tag information from a given enumeration leaving the value behind.
		/// This is useful when pulling the payload out of the enum.
		/// </summary>
		public delegate* unmanaged<void *, TypeMetadata, void> DestructiveProjectEnumData;

		/// <summary>
		/// Injects a tag into an enumeration. This is useful when creating an enum.
		/// </summary>
		public delegate* unmanaged<void *, nuint, TypeMetadata, void> DestructiveInjectEnumTag;

		/// <summary>
		/// Returns the alignment of the type in bytes.
		/// </summary>
		public int Alignment => (int)((Flags & ValueWitnessFlags.AlignmentMask) + 1);

		/// <summary>
		/// Returns true if and only if the type is a POD type. A POD type is:
		/// * integer types
		/// * floating point numbers
		/// * C enums
		/// * fixed sized-arrays (presented as homogeneous tuples of POD types)
		/// * C structs whose contents are POD types
		/// * pointers to C types
		/// * C function pointers
		/// See https://github.com/swiftlang/swift/blob/6221b29c6835442706fbb44b67b755d370a87d96/docs/proposals/AttrC.rst#type-bridging
		/// </summary>
		public bool IsNonPOD => (Flags & ValueWitnessFlags.IsNonPOD) != 0;

		/// <summary>
		/// Returns true if the value can NOT be copied bitwise.
		/// </summary>
		public bool IsNonBitwiseTakable => (Flags & ValueWitnessFlags.IsNonBitwiseTakable) != 0;

		/// <summary>
		/// Returns true if and only if the type has extra inhabitants.
		/// </summary>
		public bool HasExtraInhabitants => ExtraInhabitantCount != 0;
	}
}
