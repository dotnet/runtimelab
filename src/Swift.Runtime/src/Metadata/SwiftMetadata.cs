// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;

namespace Swift.Runtime
{
	// Implementations are partially taken from https://github.com/chkn/Xamarin.SwiftUI and https://github.com/xamarin/binding-tools-for-swift/
    using static MetadataFlags;

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/ABI/MetadataValues.h
	/// </summary>
    [Flags]
    public enum MetadataFlags
    {
        MetadataKindIsNonType = 0x400,
        MetadataKindIsNonHeap = 0x200,
        MetadataKindIsRuntimePrivate = 0x100,
    }

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/ABI/MetadataKind.def
	/// </summary>
    public enum MetadataKinds : long
    {
        Class = 0,
        Struct = 0 | MetadataKindIsNonHeap,
        Enum = 1 | MetadataKindIsNonHeap,
        Optional = 2 | MetadataKindIsNonHeap,
        ForeignClass = 3 | MetadataKindIsNonHeap,
        Opaque = 0 | MetadataKindIsRuntimePrivate | MetadataKindIsNonHeap,
        Tuple = 1 | MetadataKindIsRuntimePrivate | MetadataKindIsNonHeap,
        Function = 2 | MetadataKindIsRuntimePrivate | MetadataKindIsNonHeap,
        Existential = 3 | MetadataKindIsRuntimePrivate | MetadataKindIsNonHeap,
        Metatype = 4 | MetadataKindIsRuntimePrivate | MetadataKindIsNonHeap,
        ObjCClassWrapper = 5 | MetadataKindIsRuntimePrivate | MetadataKindIsNonHeap,
        ExistentialMetatype = 6 | MetadataKindIsRuntimePrivate | MetadataKindIsNonHeap,
        HeapLocalVariable = 0 | MetadataKindIsNonType,
        HeapGenericLocalVariable = 0 | MetadataKindIsNonType | MetadataKindIsRuntimePrivate,
        ErrorObject = 1 | MetadataKindIsNonType | MetadataKindIsRuntimePrivate
    }

	/// <summary>
	/// Represents the Swift type information with the metadata and value witness table.
	/// </summary>
	public unsafe struct SwiftTypeInfo
	{
		public IntPtr MetadataPtr;
		public SwiftMetadata *Metadata => (SwiftMetadata*)MetadataPtr.ToPointer();
		public ValueWitnessTable *ValueWitnessTable =>  (ValueWitnessTable*)(*((IntPtr*)MetadataPtr - 1));

		public int[] FieldOffsetVector
		{
			get
			{
				switch (Metadata->Kind)
				{
					case MetadataKinds.Struct:
						int[] fieldOffsets = new int[((StructDescriptor*)Metadata->TypeDescriptor)->NumberOfFields];
						int* fieldOffsetPtr = (int*)IntPtr.Add(MetadataPtr, (int)((StructDescriptor*)Metadata->TypeDescriptor) * IntPtr.Size).ToPointer();
						for (int i = 0; i < ((StructDescriptor*)Metadata->TypeDescriptor)->NumberOfFields; i++)
						{
							fieldOffsets[i] = *fieldOffsetPtr;
							fieldOffsetPtr++;
						}

						return fieldOffsets;
					default:
						throw new NotImplementedException();
				}
			}
		}
	}

	/// <summary>
	/// https://github.com/apple/swift/blob/main/docs/ABI/TypeMetadata.rst#common-metadata-layout
	/// </summary>
    [StructLayout (LayoutKind.Sequential)]
    public unsafe ref struct SwiftMetadata
    {
        public IntPtr _Kind;
		public NominalTypeDescriptor *TypeDescriptor;

		public MetadataKinds Kind => (MetadataKinds)_Kind.ToInt64();

    }

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/ABI/Metadata.h#L4098
	/// </summary>
    [StructLayout (LayoutKind.Sequential)]
	public unsafe ref struct StructDescriptor
	{
		public NominalTypeDescriptor NominalType;
		public uint NumberOfFields;
		public uint FieldOffsetVectorOffset;
	}

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/ABI/Metadata.h#L3844
	/// </summary>
    [StructLayout (LayoutKind.Sequential)]
	public unsafe ref struct NominalTypeDescriptor
    {
        public ContextDescriptor Context;
		public RelativePointer NamePtr;
		public RelativePointer AccessFunctionPtr;
		public RelativePointer FieldsPtr;
		public string? Name => Marshal.PtrToStringAnsi((IntPtr)NamePtr.Target);
    }

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/ABI/Metadata.h#L2920
	/// </summary>
    [StructLayout (LayoutKind.Sequential)]
	public unsafe ref struct ContextDescriptor
	{
		public ContextDescriptorFlags Flags;
		internal RelativePointer ParentPtr;
        public ContextDescriptor* Parent => (ContextDescriptor*)ParentPtr.Target;
	}

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/ABI/MetadataValues.h
	/// </summary>
    public enum ContextDescriptorKind : byte
	{
		Module = 0,
		Extension = 1,
		Anonymous = 2,
		Protocol = 3,
		OpaqueType = 4,
		Type_First = 16,
		Class = Type_First,
		Struct = Type_First + 1,
		Enum = Type_First + 2,
		Type_Last = 31,
	};

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/ABI/MetadataValues.h
	/// </summary>
	[StructLayout (LayoutKind.Sequential)]
	public ref struct ContextDescriptorFlags
	{
		public uint Value;

		/// <summary>
		/// The kind of context this descriptor describes.
		/// </summary>
		public ContextDescriptorKind Kind {
			get => (ContextDescriptorKind)(Value & 0x1Fu);
			set => Value = (Value & 0xFFFFFFE0u) | (byte)value;
		}

		/// <summary>
		/// Whether this is a unique record describing the referenced context.
		/// </summary>
		public bool IsUnique {
			get => (Value & 0x40u) != 0;
			set => Value = (Value & 0xFFFFFFBFu) | (value ? 0x40u : 0);
		}

		/// <summary>
		/// Whether the context being described is generic.
		/// </summary>
		public bool IsGeneric {
			get => (Value & 0x80u) != 0;
			set => Value = (Value & 0xFFFFFF7Fu) | (value? 0x80u : 0);
		}
	}

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/Basic/RelativePointer.h
	/// </summary>
    [StructLayout (LayoutKind.Sequential)]
	public unsafe ref struct RelativePointer
	{
		public int offset; // make internal
		public static RelativePointer Zero => default;
		public void* Target {
			get {
				if (offset == 0)
					return null;

				fixed (void* ptr = &this)
					return (byte*)ptr + offset;
			}
			set {
				if (value == null)
					offset = 0;
				else fixed (void* ptr = &this)
					offset = checked ((int)((byte*)value - (byte*)ptr));
			}
		}
	}

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/RemoteInspection/Records.h
	/// </summary>
	[StructLayout (LayoutKind.Sequential)]
	public unsafe ref struct FieldDescriptor
	{
		public FieldDescriptorKind Kind;

		public ushort FieldRecordSize;

		public uint NumFields;

		public FieldRecord* GetFieldRecord(int field)
		{
			fixed (FieldDescriptor* descriptorPtr = &this)
			{
				byte* current = (byte*)descriptorPtr + sizeof(FieldDescriptor);
				current += field * FieldRecordSize;
				FieldRecord* fieldRecordPtr = (FieldRecord*)current;
				return fieldRecordPtr;
			}
		}
	}

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/RemoteInspection/Records.h
	/// </summary>
	public enum FieldDescriptorKind : ushort
	{
		// Swift nominal types.
		Struct,
		Class,
		Enum,

		// Fixed-size multi-payload enums have a special descriptor format that
		// encodes spare bits.
		MultiPayloadEnum,

		// A Swift opaque protocol. There are no fields, just a record for the
		// type itself.
		Protocol,

		// A Swift class-bound protocol.
		ClassProtocol,

		// An Objective-C protocol, which may be imported or defined in Swift.
		ObjCProtocol,

		// An Objective-C class, which may be imported or defined in Swift.
		// In the former case, field type metadata is not emitted, and
		// must be obtained from the Objective-C runtime.
		ObjCClass
	}

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/RemoteInspection/Records.h
	/// </summary>
	public enum FieldRecordFlags : uint 
	{
		IsIndirectCase = 0x1,
		IsVar = 0x2,
		IsArtificial = 0x4,
  	}

	/// <summary>
	/// https://github.com/apple/swift/blob/main/include/swift/RemoteInspection/Records.h
	/// </summary>
    [StructLayout (LayoutKind.Sequential)]
	public unsafe ref struct FieldRecord
	{
		public FieldRecordFlags Flags;
		public RelativePointer MangledName;
		public RelativePointer Name;

		/// <summary>
		/// Gets the context descriptor address.
		/// https://github.com/apple/swift/blob/main/docs/ABI/Mangling.rst
		/// </summary>
		/// <returns></returns>
		public IntPtr GetContextDescriptorAddress()
		{
			IntPtr ptr = (IntPtr)MangledName.Target;
			byte[] data = new byte[GetSymbolLength(ptr)];
			Marshal.Copy(ptr, data, 0, data.Length);

			int index = 0;
			while (index < data.Length)
			{
				byte next = data[index];

				switch (GetComponent(next))
				{
					case 0:
						return IntPtr.Zero;
					case 1:
						{
							IntPtr address = IntPtr.Zero;
							if (next >= 0x01 && next <= 0x17) // Relative symbolic reference
							{
								int offset = BitConverter.ToInt32(data, index + 1);
								address = IntPtr.Add((IntPtr)MangledName.Target, offset + 1);
							}
							else if (next >= 0x18 && next <= 0x1F) // Absolute symbolic reference
							{
								address = (IntPtr)BitConverter.ToInt64(data, index + 1);
							}
							if (next == 0x01)
							{
								// Direct relative symbolic reference
								return address;
							}
							else if (next == 0x02)
							{
								// Indirect relative symbolic reference
								return *(IntPtr*)address;
							}
							break;
						}
				}

				index += GetOffset(next);
			}

			return IntPtr.Zero;
		}

		/// <summary>
		/// Gets the mangling symbol.
		/// https://github.com/apple/swift/blob/main/docs/ABI/Mangling.rst
		/// </summary>
		public string GetMangledNameSymbol()
		{
			IntPtr ptr = (IntPtr)MangledName.Target;
			byte[] data = new byte[GetSymbolLength(ptr)];
			Marshal.Copy(ptr, data, 0, data.Length);

			StringBuilder symbolBuilder = new();

			int index = 0;
			while (index < data.Length)
			{
				byte next = data[index];

				switch (GetComponent(next))
				{
					case 1:
						{
							if (next >= 0x01 && next <= 0x17) // Relative symbolic reference
							{
								index += sizeof(int);
							}
							else if (next >= 0x18 && next <= 0x1F) // Absolute symbolic reference
							{
								index += IntPtr.Size;
							}
							break;
						}
					case 2:
						{
							symbolBuilder.Append((char)next);
							index++;
							break;
						}
					default:
						index++;
						break;
				}
			}

			return symbolBuilder.ToString();
		}

		/// <summary>
		/// https://github.com/apple/swift/blob/main/docs/ABI/Mangling.rst
		/// </summary>
		private static int GetSymbolLength(IntPtr ptr)
		{
			int length = 0;
			while (true)
			{
				byte currentByte = Marshal.ReadByte(ptr, length);
				if (currentByte == 0)
					break;

				length += GetOffset(currentByte);
			}
			return length;
		}

		/// <summary>
		/// https://github.com/apple/swift/blob/main/docs/ABI/Mangling.rst
		/// </summary>
		private static int GetOffset(byte b)
		{
			switch (b)
			{
				case 0:
					return 0;
				case byte n when (n >= 0x01 && n <= 0x17):
					return 1 + sizeof(int);
				case byte m when (m >= 0x18 && m <= 0x1F):
					return 1 + IntPtr.Size;
				default:
					return 1;
			}
		}

		/// <summary>
		/// https://github.com/apple/swift/blob/main/docs/ABI/Mangling.rst
		/// </summary>
		private static int GetComponent(byte b)
		{
			switch (b)
			{
				case 0:
					return 0; // Null
				case byte n when (n >= 0x01 && n <= 0x17):
				case byte m when (m >= 0x18 && m <= 0x1F):
					return 1; // Symbolic reference
				default:
					return 2; // Normal
			}
		}
	}
}
