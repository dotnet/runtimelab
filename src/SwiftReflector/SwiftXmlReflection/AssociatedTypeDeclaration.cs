// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

namespace SwiftReflector.SwiftXmlReflection {
	public class AssociatedTypeDeclaration {
		public AssociatedTypeDeclaration ()
		{
			ConformingProtocols = new List<NamedTypeSpec> ();
		}

		public string Name { get; set; }

		public TypeSpec SuperClass { get; set; }
		public TypeSpec DefaultType { get; set; }
		public List<NamedTypeSpec> ConformingProtocols { get; private set; }


		public static AssociatedTypeDeclaration FromXElement (TypeAliasFolder folder, XElement elem)
		{
			var assocType = new AssociatedTypeDeclaration ();
			assocType.Name = NameAttribute (elem);

			var superClassElem = elem.Element ("superclass");
			if (superClassElem != null) {
				var superClassName = NameAttribute (superClassElem);
				if (superClassName != null) {
					assocType.SuperClass = folder.FoldAlias (null, TypeSpecParser.Parse (superClassName));
				}
			}
			var defaultDefn = elem.Attribute ("defaulttype");
			if (defaultDefn != null) {
				assocType.DefaultType = folder.FoldAlias (null, TypeSpecParser.Parse ((string)defaultDefn));
			}
			
			if (elem.Element ("conformingprotocols") != null) {
				var conforming = from conform in elem.Element ("conformingprotocols").Elements ()
						 select folder.FoldAlias (null, TypeSpecParser.Parse (NameAttribute (conform))) as NamedTypeSpec;
				assocType.ConformingProtocols.AddRange (conforming);
			}

			return assocType;
		}

		public void GatherXObjects (List<XObject> xobjects)
		{
			xobjects.Add (new XAttribute ("name", Name));
			if (SuperClass != null)
				xobjects.Add (new XElement ("superclass", new XElement ("superclass", new XAttribute ("name", SuperClass.ToString ()))));
			if (DefaultType != null)
				xobjects.Add (new XAttribute ("defaulttype", DefaultType.ToString ()));
			var conforming = new List<XObject> ();
			foreach (var spec in ConformingProtocols) {
				conforming.Add (new XElement ("conformingprotocol", new XAttribute ("name", spec.ToString ())));
			}
			xobjects.Add (new XElement ("conformingprotocols", conforming.ToArray ()));
		}

		static string NameAttribute(XElement elem)
		{
			return (string)elem.Attribute ("name");
		}
	}
}
