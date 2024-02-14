// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace SyntaxDynamo.SwiftLang {
	public abstract class SLType : DelegatedSimpleElement {
		public SLType (bool isAny = false)
		{
			IsAny = isAny;
		}
		public override string ToString () => CodeWriter.WriteToString (this);
		public bool IsAny { get; set; }
		protected string AnyString => IsAny ? "any " : "";
	}

	public class SLCompoundType : SLType {
		public SLCompoundType(SLType parent, SLType child, bool isAny = false)
			: base (isAny)
		{
			Parent = Exceptions.ThrowOnNull (parent, nameof (parent));
			Child = Exceptions.ThrowOnNull (child, nameof (child));
		}
		public SLType Parent { get; private set; }
		public SLType Child { get; private set; }

		protected override void LLWrite(ICodeWriter writer, object o)
		{
			writer.Write (AnyString, true);
			Parent.Write (writer, o);
			writer.Write ('.', false);
			Child.Write (writer, o);
		}
	}

	public class SLSimpleType : SLType {
		public SLSimpleType (string name, bool isAny = false)
			: base (isAny)
		{
			Name = Exceptions.ThrowOnNull (name, nameof(name));
		}

		public string Name { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write (AnyString, true);
			writer.Write (Name, false);
		}

		static SLType
			tBool = new SLSimpleType ("Bool"),
			tChar = new SLSimpleType ("Character"),
			tInt8 = new SLSimpleType ("Int8"),
			tInt16 = new SLSimpleType ("Int16"),
			tInt32 = new SLSimpleType ("Int32"),
			tInt64 = new SLSimpleType ("Int64"),
			tInt = new SLSimpleType ("Int"),
			tUint8 = new SLSimpleType ("UInt8"),
			tUint16 = new SLSimpleType ("UInt16"),
			tUint32 = new SLSimpleType ("UInt32"),
			tUint64 = new SLSimpleType ("UInt64"),
			tUint = new SLSimpleType ("UInt"),
			tFloat = new SLSimpleType ("Float"),
			tDouble = new SLSimpleType ("Double"),
			tString = new SLSimpleType ("String"),
			tVoid = new SLSimpleType ("Void"),
			tOpaquePointer = new SLSimpleType("OpaquePointer")
			;

		public static SLType Bool { get { return tBool; } }
		public static SLType Char { get { return tChar; } }
		public static SLType Int { get { return tInt; } }
		public static SLType Int8 { get { return tInt8; } }
		public static SLType Int16 { get { return tInt16; } }
		public static SLType Int32 { get { return tInt32; } }
		public static SLType Int64 { get { return tInt64; } }
		public static SLType UInt { get { return tUint; } }
		public static SLType UInt8 { get { return tUint8; } }
		public static SLType UInt16 { get { return tUint16; } }
		public static SLType UInt32 { get { return tUint32; } }
		public static SLType UInt64 { get { return tUint64; } }
		public static SLType Float { get { return tFloat; } }
		public static SLType Double { get { return tDouble; } }
		public static SLType String { get { return tString; } }
		public static SLType Void { get { return tVoid; } }
		public static SLType OpaquePointer { get { return tOpaquePointer;  } }

	}

	public class SLProtocolListType : SLType {
		public SLProtocolListType ()
		{
			Protocols = new List<SLType> ();
		}

		public SLProtocolListType (IEnumerable<SLType> types)
			: this ()
		{
			Exceptions.ThrowOnNull (types, nameof (types));
			Protocols.AddRange (types);
		}

		public List<SLType> Protocols { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			if (Protocols.Count < 2)
				throw new Exception ("Protocol list types must have at least two elements");
			Protocols [0].WriteAll (writer);
			for (int i = 1; i < Protocols.Count; i++) {
				writer.Write (" & ", true);
				Protocols [i].WriteAll (writer);
			}
		}
	}

	public class SLOptionalType : SLType {
		public SLOptionalType (SLType opt)
			: base (false)
		{
			Optional = Exceptions.ThrowOnNull (opt, nameof (opt));
		}

		public SLType Optional { get; private set; }
		protected override void LLWrite (ICodeWriter writer, object o)
		{
			bool containsFuncType = Optional is SLFuncType;
			if (containsFuncType)
				writer.Write ('(', true);
			Optional.WriteAll (writer);
			if (containsFuncType)
				writer.Write (')', true);
			writer.Write ('?', true);
		}
	}

	public class SLBoundGenericType : SLType {
		public SLBoundGenericType (string name, IEnumerable<SLType> boundTypes)
			: base (false)
		{
			Name = Exceptions.ThrowOnNull (name, nameof (name));
			BoundTypes = new List<SLType> ();
			if (boundTypes != null)
				BoundTypes.AddRange (boundTypes);
		}

		public SLBoundGenericType (string name, string singleBoundType)
		{
			Name = Exceptions.ThrowOnNull (name, nameof (name));
			BoundTypes = new List<SLType> ();
			BoundTypes.Add (new SLSimpleType (singleBoundType));
		}

		public SLBoundGenericType (string name, SLType singleBoundType)
			: this (name, new SLType [] { singleBoundType })
		{
		}

		public string Name { get; private set; }
		public List<SLType> BoundTypes { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write (Name, false);
			writer.Write ('<', true);
			for (int i = 0; i < BoundTypes.Count; i++) {
				if (i > 0)
					writer.Write (", ", true);
				BoundTypes [i].WriteAll (writer);
			}
			writer.Write ('>', true);
		}

	}

	public class SLArrayType : SLType {
		public SLArrayType (SLType elementType)
			: base (false)
		{
			ElementType = Exceptions.ThrowOnNull (elementType, nameof (elementType));
		}

		public SLType ElementType { get; private set; }
		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ('[', true);
			ElementType.WriteAll (writer);
			writer.Write (']', true);
		}

	}

	public class SLTupleType : SLType { 
		public SLTupleType (List<SLNameTypePair> pairs)
			: base (false)
		{
			Elements = new List<SLNameTypePair> ();
			if (pairs != null)
				Elements.AddRange (pairs);
		}

		public SLTupleType (params SLNameTypePair [] pairs)
			: base (false)
		{
			Elements = new List<SLNameTypePair> ();
			Elements.AddRange (pairs);
		}

		public List<SLNameTypePair> Elements { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ('(', true);
			for (int i = 0; i < Elements.Count; i++) {
				if (i > 0) {
					writer.Write (", ", true);
				}
				Elements [i].WriteAll (writer);
			}
			writer.Write (')', true);
		}

		public static SLTupleType Of (SLIdentifier id, SLType type)
		{
			return new SLTupleType (new SLNameTypePair [] { new SLNameTypePair (id, type) });
		}

		public static SLTupleType Of (string id, SLType type)
		{
			return Of (new SLIdentifier (id), type);
		}
	}


	public class SLFuncType : SLType {
		public SLFuncType (SLType argType, SLType retType, bool hasThrows = false, bool isAsync = false)
			: this (retType, ConvertSLTypeToParameters (argType), hasThrows, isAsync)
		{
		}

		public SLFuncType (SLType retType, IEnumerable<SLUnnamedParameter> parameters,
			bool hasThrows = false, bool isAsync = false)
			: base (false)
		{
			Exceptions.ThrowOnNull (parameters, nameof (parameters));
			Attributes = new List<SLAttribute> ();
			Parameters = new List<SLUnnamedParameter> ();
			if (parameters != null)
				Parameters.AddRange (parameters);
			ReturnType = Exceptions.ThrowOnNull (retType, nameof (retType));
			Throws = hasThrows;
			IsAsync = isAsync;
		}

		public List<SLUnnamedParameter> Parameters { get; private set; }
		public SLType ReturnType { get; private set; }
		public List<SLAttribute> Attributes { get; private set; }
		public bool Throws { get; private set; }
		public bool IsAsync { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			foreach (var attr in Attributes) {
				attr.WriteAll (writer);
				writer.Write (' ', true);
			}
			writer.Write ('(', true);
			for (int i = 0; i < Parameters.Count; i++) {
				if (i > 0)
					writer.Write (", ", true);
				Parameters [i].WriteAll (writer);
			}
			writer.Write (") ", true);
			if (IsAsync) {
				writer.Write ("async ", true);
			}
			if (Throws) {
				writer.Write ("throws", true);
			}
			writer.Write ("-> ", true);
			ReturnType.WriteAll (writer);
		}

		static IEnumerable<SLUnnamedParameter> ConvertSLTypeToParameters (SLType type)
		{
			if (type is SLTupleType tuple) {
				foreach (var elem in tuple.Elements) {
					yield return new SLUnnamedParameter (elem.TypeAnnotation, elem.ParameterKind);
				}
			} else {
				yield return new SLUnnamedParameter (type);
			}
		}
	}

	public class SLDictionaryType : SLType {
		public SLDictionaryType (SLType keyType, SLType valueType)
			: base (false)
		{
			KeyType = Exceptions.ThrowOnNull (keyType, nameof (keyType));
			ValueType = Exceptions.ThrowOnNull (valueType, nameof (valueType));
		}

		public SLType KeyType { get; private set; }
		public SLType ValueType { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ("[", true);
			KeyType.WriteAll (writer);
			writer.Write (" : ", true);
			ValueType.WriteAll (writer);
			writer.Write ("]", true);
		}
	}


	public class SLGenericReferenceType : SLType {
		public SLGenericReferenceType (int depth, int index, bool isMetatype = false, List<string> associatedTypePath = null)
			: base (false)
		{
			if (depth < 0)
				throw new ArgumentOutOfRangeException (nameof (depth));
			if (index < 0)
				throw new ArgumentOutOfRangeException (nameof (index));
			Depth = depth;
			Index = index;
			IsMetatype = isMetatype;
			AssociatedTypePath = associatedTypePath;
		}

		public int Depth { get; private set; }
		public int Index { get; private set; }
		public bool IsMetatype { get; private set; }
		public Func<int, int, string> ReferenceNamer { get; set; }
		public List<string> AssociatedTypePath { get; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			var suffix = IsMetatype ? ".Type" : "";
			string name;
			if (AssociatedTypePath != null && AssociatedTypePath.Any ()) {
				name = $"{Name}.{AssociatedTypePath.First ()}{suffix}";
			} else {
				name = $"{Name}{suffix}";
			}
			writer.Write (name, true);
		}

		public string Name {
			get {
				Func<int, int, string> namer = ReferenceNamer ?? DefaultNamer;
				return namer (Depth, Index);
			}
		}

		const string kNames = "TUVWABCDEFGHIJKLMN";

		public static string DefaultNamer (int depth, int index)
		{
			if (depth < 0 || depth >= kNames.Length)
				throw new ArgumentOutOfRangeException (nameof (depth));
			if (index < 0)
				throw new ArgumentOutOfRangeException (nameof (index));
			return String.Format ("{0}{1}", kNames [depth], index);
		}
	}

	public class SLVariadicType : SLType {
		public SLVariadicType (SLType repeatingType)
			: base (repeatingType.IsAny)
		{
			RepeatingType = Exceptions.ThrowOnNull (repeatingType, nameof (repeatingType));
		}

		public SLType RepeatingType { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			RepeatingType.WriteAll (writer);
			writer.Write (" ...", true);
		}
	}
}

