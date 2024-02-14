// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using SwiftReflector.ExceptionTools;
using SwiftReflector.Demangling;

namespace SwiftReflector {
	public class ValueWitnessTable {
		//		#define FOR_ALL_FUNCTION_VALUE_WITNESSES(MACRO) \
		//		MACRO(destroyBuffer) \
		//		MACRO(initializeBufferWithCopyOfBuffer) \
		//		MACRO(projectBuffer) \
		//		MACRO(deallocateBuffer) \
		//		MACRO(destroy) \
		//		MACRO(initializeBufferWithCopy) \
		//		MACRO(initializeWithCopy) \
		//		MACRO(assignWithCopy) \
		//		MACRO(initializeBufferWithTake) \
		//		MACRO(initializeWithTake) \
		//		MACRO(assignWithTake) \
		//		MACRO(allocateBuffer) \
		//		MACRO(initializeBufferWithTakeOfBuffer) \
		//		MACRO(destroyArray) \
		//		MACRO(initializeArrayWithCopy) \
		//		MACRO(initializeArrayWithTakeFrontToBack) \
		//		MACRO(initializeArrayWithTakeBackToFront)
		public long DestroyBufferOffset { get; private set; }
		public long InitializeBufferWithCopyOfBufferOffset { get; private set; }
		public long ProjectBufferOffset { get; private set; }
		public long DeallocateBufferOffset { get; private set; }
		public long DestroyOffset { get; private set; }
		public long InitializeBufferWithCopyOffset { get; private set; }
		public long InitializeWithCopyOffset { get; private set; }
		public long AssignWithCopyOffset { get; private set; }
		public long InitializeBufferWithTakeOffset { get; private set; }
		public long InitializeWithTakeOffset { get; private set; }
		public long AssignWithTakeOffset { get; private set; }
		public long AllocateBufferOffset { get; private set; }
		public long InitializeBufferWithTakeOfBufferOffset { get; private set; }
		public long DestroyArrayOffset { get; private set; }
		public long InitializeArrayWithCopyOffset { get; private set; }
		public long InitializeArrayWithTakeFrontToBackOffset { get; private set; }
		public long InitializeArrayWithTakeBackToFrontOffset { get; private set; }
		public long Size { get; private set; }
		public ushort Flags { get; private set; }
		public ushort Log2Stride { get; private set; }
		public long Stride { get; private set; }
		public string MangledName { get; private set; }

		ValueWitnessTable ()
		{
		}

		public static ValueWitnessTable FromStream (Stream stm, TLFunction tlf, int sizeofMachinePointer)
		{
			if (sizeofMachinePointer != 4 && sizeofMachinePointer != 8) {
				throw ErrorHelper.CreateError (ReflectorError.kCantHappenBase + 14, $"Expected a maching pointer size of either 4 or 8, but got {sizeofMachinePointer}");
			}
			var wit = tlf.Signature as SwiftWitnessTableType;
			if (wit == null || wit.WitnessType != WitnessType.Value)
				throw ErrorHelper.CreateError (ReflectorError.kCantHappenBase + 15, $"Expected a SwiftWitnessTable, but got {tlf.Signature.GetType ().Name}.");
			var reader = new BinaryReader (stm);
			reader.BaseStream.Seek ((long)tlf.Offset, SeekOrigin.Begin);

			var table = new ValueWitnessTable ();
			table.MangledName = tlf.MangledName;
			if (sizeofMachinePointer == 4)
				table.Read32 (reader);
			else
				table.Read64 (reader);
			return table;
		}

		void Read32 (BinaryReader reader)
		{
			DestroyBufferOffset = reader.ReadInt32 ();
			InitializeBufferWithCopyOfBufferOffset = reader.ReadInt32 ();
			ProjectBufferOffset = reader.ReadInt32 ();
			DeallocateBufferOffset = reader.ReadInt32 ();
			DestroyOffset = reader.ReadInt32 ();
			InitializeBufferWithCopyOffset = reader.ReadInt32 ();
			InitializeWithCopyOffset = reader.ReadInt32 ();
			AssignWithCopyOffset = reader.ReadInt32 ();
			InitializeBufferWithTakeOffset = reader.ReadInt32 ();
			InitializeWithTakeOffset = reader.ReadInt32 ();
			AssignWithTakeOffset = reader.ReadInt32 ();
			AllocateBufferOffset = reader.ReadInt32 ();
			InitializeBufferWithTakeOfBufferOffset = reader.ReadInt32 ();
			DestroyArrayOffset = reader.ReadInt32 ();
			InitializeArrayWithCopyOffset = reader.ReadInt32 ();
			InitializeArrayWithTakeFrontToBackOffset = reader.ReadInt32 ();
			InitializeArrayWithTakeBackToFrontOffset = reader.ReadInt32 ();
			Size = reader.ReadInt32 ();
			Flags = reader.ReadUInt16 ();
			Log2Stride = reader.ReadUInt16 ();
			Stride = reader.ReadInt32 ();
		}

		void Read64 (BinaryReader reader)
		{
			DestroyBufferOffset = reader.ReadInt64 ();
			InitializeBufferWithCopyOfBufferOffset = reader.ReadInt64 ();
			ProjectBufferOffset = reader.ReadInt64 ();
			DeallocateBufferOffset = reader.ReadInt64 ();
			DestroyOffset = reader.ReadInt64 ();
			InitializeBufferWithCopyOffset = reader.ReadInt64 ();
			InitializeWithCopyOffset = reader.ReadInt64 ();
			AssignWithCopyOffset = reader.ReadInt64 ();
			InitializeBufferWithTakeOffset = reader.ReadInt64 ();
			InitializeWithTakeOffset = reader.ReadInt64 ();
			AssignWithTakeOffset = reader.ReadInt64 ();
			AllocateBufferOffset = reader.ReadInt64 ();
			InitializeBufferWithTakeOfBufferOffset = reader.ReadInt64 ();
			DestroyArrayOffset = reader.ReadInt64 ();
			InitializeArrayWithCopyOffset = reader.ReadInt64 ();
			InitializeArrayWithTakeFrontToBackOffset = reader.ReadInt64 ();
			InitializeArrayWithTakeBackToFrontOffset = reader.ReadInt64 ();
			Size = reader.ReadInt64 ();
			reader.ReadInt32 ();
			Flags = reader.ReadUInt16 ();
			Log2Stride = reader.ReadUInt16 ();
			Stride = reader.ReadInt64 ();
		}
	}
}

