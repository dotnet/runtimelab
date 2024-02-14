// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SyntaxDynamo.CSLang;
using SwiftReflector.SwiftXmlReflection;
using SwiftRuntimeLibrary;

namespace SwiftReflector.TypeMapping {
	public class NetTypeBundle {
		const string kNoType = "!!NO_TYPE!!";
		List<NetTypeBundle> genericTypes = new List<NetTypeBundle> ();

		public NetTypeBundle (string nameSpace, string entityName, bool isScalar, bool isReference, EntityType entity,
			bool swiftThrows = false)
		{
			GenericIndex = -1;
			IsVoid = entityName == kNoType;
			Type = entityName;
			NameSpace = nameSpace;

			FullName = String.IsNullOrEmpty (nameSpace) ? entityName : nameSpace + "." + entityName;
			IsScalar = isScalar;
			IsReference = isReference;
			Entity = entity;
			Throws = swiftThrows;
		}

		public NetTypeBundle (List<NetTypeBundle> tupleElements, bool isReference)
		{
			GenericIndex = -1;
			string netTypeName = ToTupleName (tupleElements);
			FullName = "System." + netTypeName;
			Type = netTypeName;
			NameSpace = "System";

			IsScalar = false;
			IsReference = isReference;
			Entity = EntityType.Tuple;
			TupleTypes.AddRange (Exceptions.ThrowOnNull (tupleElements, "tupleElements"));
		}

		public NetTypeBundle (string nameSpace, string entityName, EntityType entity, bool isReference, IEnumerable<NetTypeBundle> genericTypes,
			bool swiftThrows = false)
			: this (nameSpace, entityName, false, isReference, entity)
		{
			GenericIndex = -1;
			this.genericTypes.AddRange (genericTypes);
			if (this.genericTypes.Count == 0)
				throw new ArgumentOutOfRangeException (nameof (genericTypes), "Generic NetBundle constructor needs actual generic types.");
			Throws = swiftThrows;
		}

		public NetTypeBundle (int depth, int index)
		{
			if (depth < 0)
				throw new ArgumentOutOfRangeException (nameof (depth));
			if (index < 0)
				throw new ArgumentOutOfRangeException (nameof (index));
			GenericDepth = depth;
			GenericIndex = index;
		}

		public NetTypeBundle (ProtocolDeclaration proto, AssociatedTypeDeclaration assoc, bool isReference)
		{
			GenericIndex = -1;
			AssociatedTypeProtocol = proto;
			AssociatedType = assoc;
			// Type = OverrideBuilder.GenericAssociatedTypeName (assoc);
			FullName = Type;
			NameSpace = String.Empty;
			IsReference = isReference;
		}

		public NetTypeBundle (string selfRepresentation, bool isReference)
		{
			GenericIndex = -1;
			Type = FullName = Exceptions.ThrowOnNull (selfRepresentation, nameof (selfRepresentation));
			NameSpace = String.Empty;
			IsReference = isReference;
			IsSelf = true;
		}

		static NetTypeBundle ntbVoid = new NetTypeBundle ("", kNoType, false, false, EntityType.None);

		public static NetTypeBundle Void { get { return ntbVoid; } }

		static NetTypeBundle ntbIntPtr = new NetTypeBundle ("System", "IntPtr", false, false, EntityType.None);
		public static NetTypeBundle IntPtr {
			get {
				return ntbIntPtr;
			}
		}

		public bool IsSelf { get; private set; }
		public bool IsVoid { get; private set; }
		public string Type { get; private set; }
		public string NameSpace { get; private set; }
		public string FullName { get; private set; }
		public bool IsScalar { get; private set; }
		public bool IsReference { get; private set; }
		public EntityType Entity { get; private set; }
		public List<NetTypeBundle> TupleTypes { get { return genericTypes; } }
		public NetTypeBundle OptionalType { get { return genericTypes [0]; } }
		public List<NetTypeBundle> GenericTypes { get { return genericTypes; } }
		public int GenericDepth { get; private set; }
		public int GenericIndex { get; private set; }
		public bool IsGenericReference { get { return GenericIndex >= 0; } }
		public ProtocolDeclaration AssociatedTypeProtocol { get; private set; }
		public AssociatedTypeDeclaration AssociatedType { get; private set; }
		public bool IsAssociatedType {  get { return AssociatedTypeProtocol != null; } }
		public bool Throws { get; private set; }
		public bool ContainsGenericParts {
			get {
				return genericTypes.Count > 0;
			}
		}
		public List<NetTypeBundle> GenericConstraints {
			get {
				return IsGenericReference ? GenericTypes : null;
			}
		}

		public override string ToString ()
		{
			return FullName;
		}

		public override bool Equals (object obj)
		{
			NetTypeBundle other = obj as NetTypeBundle;
			if (other == null)
				return false;
			return FullName == other.FullName;
		}

		public override int GetHashCode ()
		{
			return FullName.GetHashCode ();
		}

		static string ToTupleName (IEnumerable<NetTypeBundle> types)
		{
			StringBuilder sb = new StringBuilder ();
			bool first = true;
			sb.Append ("Tuple<");
			foreach (NetTypeBundle t in types) {
				if (!first) {
					sb.Append (", ");
				}
				first = false;
				sb.Append (t.FullName);
			}
			return sb.Append (">").ToString ();
		}

		static string ToOptionalName (NetTypeBundle optType)
		{
			StringBuilder sb = new StringBuilder ();
			sb.Append ("SwiftOptional<").Append (optType.FullName).Append (">");
			return sb.ToString ();
		}


		static IEnumerable<CSType> ToCSSimpleType (IEnumerable<NetTypeBundle> ntbs, CSUsingPackages use)
		{
			return ntbs.Select (ntb => ToCSSimpleType (ntb, use));
		}

		public static CSType ToCSSimpleType(NetTypeBundle ntb, CSUsingPackages use)
		{
			if (!String.IsNullOrEmpty (ntb.NameSpace))
				use.AddIfNotPresent (ntb.NameSpace);
			return ntb.Entity == EntityType.Tuple ? ToCSTuple (ntb.TupleTypes, use) : ntb.ToCSType (use);
		}

		public static CSSimpleType ToCSOptional (NetTypeBundle optType, CSUsingPackages use)
		{
			// use.AddIfNotPresent (typeof (SwiftOptional<>));
			// return new CSSimpleType ("SwiftOptional", false, optType.ToCSType (use));
			throw new Exception();
		}

		public static CSSimpleType ToCSTuple (IList<NetTypeBundle> innerTypes, CSUsingPackages use)
		{
			if (innerTypes.Count <= 7) {
				return new CSSimpleType ("Tuple", false, ToCSSimpleType (innerTypes, use).ToArray ());
			} else {
				IEnumerable<CSType> head = ToCSSimpleType (innerTypes.Take (7), use);
				CSType tail = ToCSTuple (innerTypes.Skip (7).ToList (), use);
				return new CSSimpleType ("Tuple", false, Enumerable.Concat (head, Enumerable.Repeat (tail, 1)).ToArray ());
			}
		}

		public CSType ToCSType (CSUsingPackages use)
		{
			if (!String.IsNullOrEmpty (NameSpace))
				use.AddIfNotPresent (NameSpace);
			if (IsGenericReference) {
				CSGenericReferenceType genref = new CSGenericReferenceType (GenericDepth, GenericIndex);
				if (this.GenericConstraints.Count > 0) {
					genref.InterfaceConstraints.AddRange (this.GenericConstraints.Select (ntb => ntb.ToCSType (use)));
				}
				return genref;
			} else if (IsAssociatedType || IsSelf) {
				return new CSSimpleType (Type, false);
			}

			return Entity == EntityType.Tuple ? ToCSTuple (TupleTypes, use) : 
				                   new CSSimpleType (Type, false, GenericTypes.Select (ntb => ntb.ToCSType (use)).ToArray ());
		}


	}
}

