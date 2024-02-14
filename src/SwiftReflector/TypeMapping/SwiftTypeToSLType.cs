// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo.SwiftLang;
using SwiftReflector.ExceptionTools;
using System.Collections.Generic;
using System.Linq;
using SwiftRuntimeLibrary;

namespace SwiftReflector.TypeMapping {
	public class SwiftTypeToSLType {
		TypeMapper typeMapper;
		bool IncludeModule { get; }
		public SwiftTypeToSLType (TypeMapper typeMapper, bool includeModule = false)
		{
			this.typeMapper = Exceptions.ThrowOnNull (typeMapper, "typeMapper");
			IncludeModule = includeModule;
		}


		public SLType MapType (SLImportModules modules, SwiftType st)
		{
			switch (st.Type) {
			case CoreCompoundType.Scalar:
				return ToScalar ((SwiftBuiltInType)st);
			case CoreCompoundType.Tuple:
				return ToTuple (modules, (SwiftTupleType)st);
			case CoreCompoundType.MetaClass:
				// Steve sez:
				// Today, type objects are never explicit arguments to functions
				throw ErrorHelper.CreateError (ReflectorError.kCantHappenBase + 3, "Asked to map an internal type object to an SLType - this should never happen");
			case CoreCompoundType.Class:
				return ToClass (modules, (SwiftClassType)st);
			case CoreCompoundType.BoundGeneric:
				return ToClass (modules, (SwiftBoundGenericType)st);
			case CoreCompoundType.ProtocolList:
				return ToProtocol (modules, (SwiftProtocolListType)st);
			case CoreCompoundType.GenericReference:
				return ToGenericArgReference (modules, (SwiftGenericArgReferenceType)st);
			case CoreCompoundType.Function:
				return ToClosure (modules, (SwiftFunctionType)st);
			default:
				throw new NotImplementedException ();
			}
		}

		public SLType MapType (SLImportModules modules, SwiftClassName cl)
		{
			return ToClass (modules, cl);
		}


		static SLType ToScalar (SwiftBuiltInType st)
		{
			switch (st.BuiltInType) {
			case CoreBuiltInType.Bool:
				return SLSimpleType.Bool;
			case CoreBuiltInType.Double:
				return SLSimpleType.Double;
			case CoreBuiltInType.Float:
				return SLSimpleType.Float;
			case CoreBuiltInType.Int:
				return SLSimpleType.Int;
			case CoreBuiltInType.UInt:
				return SLSimpleType.UInt;
			default:
				throw new ArgumentOutOfRangeException (nameof (st));
			}
		}

		public void MapParams (SLImportModules modules, List<SLNameTypePair> output, SwiftType parameters)
		{
			if (parameters is SwiftTupleType) {
				SLTupleType sltuple = typeMapper.SwiftTypeMapper.ToParameters (modules, (SwiftTupleType)parameters);
				output.AddRange (sltuple.Elements);
			} else {
				output.Add (new SLNameTypePair (parameters.IsReference || MustBeInOut (parameters) ? SLParameterKind.InOut : SLParameterKind.None, new SLIdentifier ("noName"), MapType (modules, parameters)));
			}
		}

		bool MustBeInOut (SwiftType st)
		{
			SwiftProtocolListType protList = st as SwiftProtocolListType;
			if (protList != null) {
				if (protList.Protocols.Count > 1)
					throw ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 14, "Protocol lists of size > 1 aren't supported. Yet.");
				return MustBeInOut (protList.Protocols [0]);
			}
			if (st.IsClass)
				return false;
			return typeMapper.MustForcePassByReference (st);
		}

		public SLTupleType ToParameters (SLImportModules modules, SwiftTupleType st)
		{
			List<SLNameTypePair> contents = st.Contents.Select (
				(swiftType, i) => new SLNameTypePair (
					swiftType.IsReference || MustBeInOut (swiftType) ? SLParameterKind.InOut : SLParameterKind.None,
					ConjureIdentifier (swiftType.Name, i),
					MapType (modules, swiftType))).ToList ();
			return new SLTupleType (contents);
		}


		SLTupleType ToTuple (SLImportModules modules, SwiftTupleType st)
		{
			List<SLNameTypePair> contents = st.Contents.Select (
				(swiftType, i) => new SLNameTypePair (
					(swiftType.Name == null ? SLIdentifier.Anonymous : new SLIdentifier (swiftType.Name.Name)),
					MapType (modules, swiftType))).ToList ();
			return new SLTupleType (contents);
		}

		SLSimpleType ToClass (SLImportModules modules, SwiftClassType st)
		{
			return ToClass (modules, st.ClassName, IncludeModule);
		}

		SLType ToClass (SLImportModules modules, SwiftBoundGenericType gt)
		{
			SLType slType = MapType (modules, gt.BaseType);
			SLSimpleType baseType = slType as SLSimpleType;
			if (baseType == null)
				throw ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 15, $"Mapping SwiftType to SLType, expected a simple type, but got {baseType.GetType ().Name}.");
			IEnumerable<SLType> boundTypes = gt.BoundTypes.Select (st => MapType (modules, st));
			return new SLBoundGenericType (baseType.Name, boundTypes);
		}

		static SLSimpleType ToClass (SLImportModules modules, SwiftClassName className, bool includeModule = false)
		{
			modules.AddIfNotPresent (className.Module.Name);
			return new SLSimpleType (className.ToFullyQualifiedName (includeModule));
		}

		SLSimpleType ToProtocol (SLImportModules modules, SwiftProtocolListType protocol)
		{
			if (protocol.Protocols.Count > 1)
				throw new NotSupportedException ("Protocol lists > 1 not supported (yet).");
			SwiftClassType cl = protocol.Protocols [0];
			return ToClass (modules, cl);
		}

		SLType ToGenericArgReference (SLImportModules modules, SwiftGenericArgReferenceType arg)
		{
			return new SLGenericReferenceType (arg.Depth, arg.Index, associatedTypePath: arg.AssociatedTypePath);
		}

		SLType ToClosure (SLImportModules modules, SwiftFunctionType func)
		{
			var args = func.EachParameter.Select (p => new SLUnnamedParameter (MapType (modules, p), ToParameterKind (p)));
			var returnType = MapType (modules, func.ReturnType);
			var closureResult = new SLFuncType (returnType, args);
			return closureResult;
		}

		static SLIdentifier ConjureIdentifier (SwiftName name, int index)
		{
			return new SLIdentifier (name != null ? name.Name : String.Format ("noName{0}", index));
		}

		static SLParameterKind ToParameterKind (SwiftType p)
		{
			if (p.IsReference)
				return SLParameterKind.InOut;
			return SLParameterKind.None;
		}
	}
}
