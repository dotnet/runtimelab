// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using SwiftReflector.TypeMapping;

namespace SwiftReflector.SwiftXmlReflection {
	public class GenericDeclaration {
		public GenericDeclaration ()
		{
			Constraints = new List<BaseConstraint> ();
		}

		public GenericDeclaration (string name)
			: this ()
		{
			Name = name;
		}

		public GenericDeclaration (GenericDeclaration other)
			: this ()
		{
			Name = other.Name;
			foreach (var constraint in other.Constraints) {
				Constraints.Add (BaseConstraint.CopyOf (constraint));
			}
		}


		public string Name { get; set; }
		public List<BaseConstraint> Constraints { get; private set; }

		public bool IsProtocolConstrained (TypeMapper mapper)
		{
			if (Constraints.Count == 0)
				return false;
			foreach (BaseConstraint bc in Constraints) {
				Entity ent = null;
				InheritanceConstraint inh = bc as InheritanceConstraint;
				if (inh != null) {
					ent = mapper.GetEntityForTypeSpec (inh.InheritsTypeSpec);
				} else {
					EqualityConstraint eq = (EqualityConstraint)bc;
					ent = mapper.GetEntityForTypeSpec (eq.Type2Spec);
				}
				if (ent == null)
					continue; // shouldn't happen
				if (ent.EntityType != EntityType.Protocol)
					return false;
			}
			return true;
		}

		public bool IsAssociatedTypeProtocolConstrained (TypeMapper mapper)
		{
			if (Constraints.Count == 0)
				return false;
			foreach (BaseConstraint bc in Constraints) {
				Entity ent = null;
				InheritanceConstraint inh = bc as InheritanceConstraint;
				if (inh != null) {
					ent = mapper.GetEntityForTypeSpec (inh.InheritsTypeSpec);
				} else {
					EqualityConstraint eq = (EqualityConstraint)bc;
					ent = mapper.GetEntityForTypeSpec (eq.Type2Spec);
				}
				if (ent == null)
					continue; // shouldn't happen
				if (ent.EntityType != EntityType.Protocol)
					return false;
				if (ent.Type is ProtocolDeclaration proto && !proto.IsExistential)
					return true;
			}
			return false;
		}

		public bool IsClassConstrained (TypeMapper mapper)
		{
			if (Constraints.Count == 0)
				return false;
			foreach (BaseConstraint bc in Constraints) {
				Entity ent = null;
				InheritanceConstraint inh = bc as InheritanceConstraint;
				if (inh != null) {
					ent = mapper.GetEntityForTypeSpec (inh.InheritsTypeSpec);
				} else {
					EqualityConstraint eq = (EqualityConstraint)bc;
					ent = mapper.GetEntityForTypeSpec (eq.Type2Spec);
				}
				if (ent == null)
					continue; // shouldn't happen
				if (ent.EntityType == EntityType.Class)
					return true;
			}
			return false;

		}

		public static List<GenericDeclaration> FromXElement (TypeAliasFolder folder, XElement generic)
		{
			List<GenericDeclaration> decls = new List<GenericDeclaration> ();
			if (generic == null)
				return decls;
			decls.AddRange (from decl in generic.Descendants ("param") select new GenericDeclaration ((string)decl.Attribute ("name")));

			var constraints = from constr in generic.Descendants ("where") select BaseConstraint.FromXElement (folder, constr);
			foreach (BaseConstraint constr in constraints) {
				GenericDeclaration decl = FindGenericDeclFor (constr, decls);
				if (decl != null)
					decl.Constraints.Add (constr);
			}

			return decls;
		}

		static GenericDeclaration FindGenericDeclFor (BaseConstraint constraint, List<GenericDeclaration> decls)
		{
			string nameToMatch = constraint.EffectiveTypeName ();
			if (nameToMatch == null)
				return null;
			return decls.FirstOrDefault (d => d.Name == nameToMatch);
		}
	}
}
