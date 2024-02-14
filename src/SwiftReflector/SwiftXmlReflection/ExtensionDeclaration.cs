// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SwiftReflector.ExceptionTools;
using SwiftReflector.IOUtils;
using SwiftRuntimeLibrary;

namespace SwiftReflector.SwiftXmlReflection {
	public class ExtensionDeclaration : IXElementConvertible {

		public ExtensionDeclaration ()
		{
			Inheritance = new List<Inheritance> ();
			Members = new List<Member> ();
		}

		public ExtensionDeclaration (ExtensionDeclaration other)
			: this ()
		{
			Inheritance.AddRange (other.Inheritance);
			Members.AddRange (other.Members);
			ExtensionOnTypeName = other.ExtensionOnTypeName;
			Module = other.Module;
		}

		public ModuleDeclaration Module { get; set; }
		public List<Inheritance> Inheritance { get; set; }
		string extensionOnTypeName;
		public string ExtensionOnTypeName {
			get {
				return extensionOnTypeName;
			}
			set {
				extensionOnTypeName = Exceptions.ThrowOnNull (value, "value");
				try {
					ExtensionOnType = TypeSpecParser.Parse (extensionOnTypeName);
				} catch (RuntimeException ex) {
					throw ErrorHelper.CreateError (ReflectorError.kReflectionErrorBase, $"Unable to parse type name '{extensionOnTypeName}': {ex.Message}");
				}

			}
		}
		public TypeSpec ExtensionOnType { get; private set; }
		public List<Member> Members { get; set; }

		public XElement ToXElement ()
		{
			var xobjects = new List<XObject> ();
			GatherXObjects (xobjects);
			XElement typeDecl = new XElement ("extension", xobjects.ToArray ());
			return typeDecl;
		}

		void GatherXObjects (List<XObject> xobjects)
		{
			xobjects.Add (new XAttribute ("onType", ExtensionOnTypeName));
			List<XObject> memcontents = new List<XObject> (Members.Select (m => m.ToXElement ()));
			xobjects.Add (new XElement ("members", memcontents.ToArray ()));
			List<XObject> inherits = new List<XObject> (Inheritance.Select (i => i.ToXElement ()));
			xobjects.Add (new XElement ("inherits", inherits.ToArray ()));
		}

		public static ExtensionDeclaration FromXElement (TypeAliasFolder folder, XElement elem, ModuleDeclaration module)
		{
			var decl = new ExtensionDeclaration ();
			decl.Module = module;
			decl.ExtensionOnTypeName = (string)elem.Attribute ("onType");
			decl.ExtensionOnType = folder.FoldAlias (null, decl.ExtensionOnType);
			if (elem.Element ("members") != null) {
				var members = from mem in elem.Element ("members").Elements ()
					      select Member.FromXElement (folder, mem, module, null) as Member;
				decl.Members.AddRange (members);
				foreach (var member in decl.Members) {
					member.ParentExtension = decl;
				}
			}
			if (elem.Element ("inherits") != null) {
				var inherits = from inherit in elem.Element ("inherits").Elements ()
					       select SwiftReflector.SwiftXmlReflection.Inheritance.FromXElement (folder, inherit) as Inheritance;
				decl.Inheritance.AddRange (inherits);
			}
			return decl;
		}
	}
}
