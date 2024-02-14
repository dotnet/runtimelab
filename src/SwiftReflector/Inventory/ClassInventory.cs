// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SwiftReflector.ExceptionTools;
using System.IO;
using SwiftReflector.Demangling;

namespace SwiftReflector.Inventory {
	public class ClassInventory : Inventory<ClassContents> {
		int sizeofMachinePointer;
		public ClassInventory (int sizeofMachinePointer)
		{
			this.sizeofMachinePointer = sizeofMachinePointer;
		}

		public override void Add (TLDefinition tld, Stream srcStm)
		{
			// ignoring these - we don't/can't use them
			if (tld is TLDefaultArgumentInitializer)
				return;
			SwiftName className = ToClassName (tld);
			SwiftClassName formalName = ToFormalClassName (tld);
			lock (valuesLock) {
				ClassContents contents = null;
				if (!values.TryGetValue (className, out contents)) {
					contents = new ClassContents (formalName, sizeofMachinePointer);
					values.Add (className, contents);
				}
				contents.Add (tld, srcStm);
			}
		}

		public static SwiftClassName ToFormalClassName (TLDefinition tld)
		{
			TLClassElem elem = tld as TLClassElem;
			if (elem != null) {
				if (elem.Class == null)
					throw ErrorHelper.CreateError (ReflectorError.kCantHappenBase + 2, $"Expected a top level definition to have a class name.");
				return elem.Class.ClassName;
			}
			if (tld is TLProtocolConformanceDescriptor protocolDesc) {
				if (protocolDesc.ImplementingType is SwiftClassType swiftClass)
					return swiftClass.ClassName;
				if (protocolDesc.ImplementingType is SwiftBoundGenericType boundGen) {
					var baseType = boundGen.BaseType as SwiftClassType;
					if (baseType != null)
						return baseType.ClassName;
				} else if (protocolDesc.ImplementingType is SwiftBuiltInType builtInType) {
					return SwiftClassName.FromFullyQualifiedName ($"Swift.{builtInType.BuiltInType}", OperatorType.None, "V");
				}
			}

			throw ErrorHelper.CreateError (ReflectorError.kInventoryBase + 0, $"unknown top level definition {tld.GetType ().Name}");
		}

		public static SwiftName ToClassName (TLDefinition tld)
		{
			SwiftClassName cln = ToFormalClassName (tld);
			return new SwiftName (cln.ToFullyQualifiedName (), false);
		}
	}

}

