// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang {
	public class CSProperty : CodeElementCollection<ICodeElement> {
		public CSProperty (CSType type, CSMethodKind kind, CSIdentifier name,
			CSVisibility getVis, IEnumerable<ICodeElement> getter,
			CSVisibility setVis, IEnumerable<ICodeElement> setter)
			: this (type, kind, name,
				getVis, getter != null ? new CSCodeBlock (getter) : null,
				setVis, setter != null ? new CSCodeBlock (setter) : null)
		{
		}


		public CSProperty (CSType type, CSMethodKind kind, CSIdentifier name,
			CSVisibility getVis, CSCodeBlock getter,
			CSVisibility setVis, CSCodeBlock setter)
			: this (type, kind, name, getVis, getter, setVis, setter, null)
		{
		}

		public CSProperty (CSType type, CSMethodKind kind,
			CSVisibility getVis, CSCodeBlock getter,
			CSVisibility setVis, CSCodeBlock setter, CSParameterList parms)
			: this (type, kind, new CSIdentifier ("this"), getVis, getter, setVis, setter,
				Exceptions.ThrowOnNull (parms, nameof (parms)))
		{
		}

		CSProperty (CSType type, CSMethodKind kind, CSIdentifier name,
			CSVisibility getVis, CSCodeBlock getter,
			CSVisibility setVis, CSCodeBlock setter, CSParameterList parms)
		{
			bool unifiedVis = getVis == setVis;
			IndexerParameters = parms;

			LineCodeElementCollection<ICodeElement> decl = new LineCodeElementCollection<ICodeElement> (null, false, true);

			GetterVisibility = getVis;
			SetterVisibility = setVis;
			CSVisibility bestVis = (CSVisibility)Math.Min ((int)getVis, (int)setVis);
			decl.And (new SimpleElement (CSMethod.VisibilityToString (bestVis))).And (SimpleElement.Spacer);
			if (kind != CSMethodKind.None)
				decl.And (new SimpleElement (CSMethod.MethodKindToString (kind))).And (SimpleElement.Spacer);

			PropType = type;
			Name = name;

			decl.And (Exceptions.ThrowOnNull (type, "type")).And (SimpleElement.Spacer)
				.And (Exceptions.ThrowOnNull (name, nameof (name)));
			if (parms != null) {
				decl.And (new SimpleElement ("[", true)).And (parms).And (new SimpleElement ("]"));
			}
			Add (decl);


			CSCodeBlock cb = new CSCodeBlock (null);

			if (getter != null) {
				Getter = getter;
				LineCodeElementCollection<ICodeElement> getLine = MakeEtter (getVis, "get", unifiedVis, getVis > setVis);
				cb.Add (getLine);
				if (getter.Count () == 0) {
					getLine.Add (new SimpleElement (";"));
				} else {
					cb.Add (getter);
				}
			}
			if (setter != null) {
				Setter = setter;
				LineCodeElementCollection<ICodeElement> setLine = MakeEtter (setVis, "set", unifiedVis, setVis > getVis);
				cb.Add (setLine);
				if (setter.Count () == 0) {
					setLine.Add (new SimpleElement (";"));
				} else {
					cb.Add (setter);
				}
			}

			Add (cb);
		}
		public CSType PropType { get; private set; }
		public CSIdentifier Name { get; private set; }
		public CSParameterList IndexerParameters { get; private set; }
		public CSVisibility GetterVisibility { get; private set; }
		public CSVisibility SetterVisibility { get; private set; }

		public CSCodeBlock Getter { get; private set; }

		public CSCodeBlock Setter { get; private set; }

		static LineCodeElementCollection<ICodeElement> MakeEtter (CSVisibility vis, string getset,
			bool unifiedVis, bool moreRestrictiveVis)
		{
			LineCodeElementCollection<ICodeElement> getLine = new LineCodeElementCollection<ICodeElement> (null, false, true);
			if (!unifiedVis && vis != CSVisibility.None && moreRestrictiveVis)
				getLine.And (new SimpleElement (CSMethod.VisibilityToString (vis))).And (SimpleElement.Spacer);
			return getLine.And (new SimpleElement (getset, false));
		}

		public static CSProperty PublicGetSet (CSType type, string name)
		{
			return new CSProperty (type, CSMethodKind.None, new CSIdentifier (name),
				CSVisibility.Public, new CSCodeBlock (), CSVisibility.Public, new CSCodeBlock ());
		}

		public static CSProperty PublicGetPrivateSet (CSType type, string name)
		{
			return new CSProperty (type, CSMethodKind.None, new CSIdentifier (name),
				CSVisibility.Public, new CSCodeBlock (), CSVisibility.Private, new CSCodeBlock ());
		}

		static CSProperty PublicGetPubPrivSetBacking (CSType type, string name, bool declareField, bool setIsPublic, string backingFieldName = null)
		{
			if (!declareField && backingFieldName == null)
				throw new ArgumentException ("declareField must be true if there is no supplied field name", nameof (declareField));
			backingFieldName = backingFieldName ?? MassageName (Exceptions.ThrowOnNull (name, nameof (name)));


			CSIdentifier backingIdent = new CSIdentifier (backingFieldName);
			LineCodeElementCollection<ICodeElement> getCode =
			    new LineCodeElementCollection<ICodeElement> (new ICodeElement [] { CSReturn.ReturnLine (backingIdent) }, false, true);
			LineCodeElementCollection<ICodeElement> setCode =
			    new LineCodeElementCollection<ICodeElement> (
				new ICodeElement [] {
			CSAssignment.Assign (backingFieldName, new CSIdentifier ("value"))
				}, false, true);
			CSProperty prop = new CSProperty (type, CSMethodKind.None, new CSIdentifier (name), CSVisibility.Public,
					    new CSCodeBlock (getCode),
						     (setIsPublic ? CSVisibility.Public : CSVisibility.Private), new CSCodeBlock (setCode));
			if (declareField)
				prop.Insert (0, CSFieldDeclaration.FieldLine (type, backingFieldName));
			return prop;
		}


		public static CSProperty PublicGetSetBacking (CSType type, string name, bool declareField, string backingFieldName = null)
		{
			return PublicGetPubPrivSetBacking (type, name, true, declareField, backingFieldName);
		}

		public static CSProperty PublicGetPrivateSetBacking (CSType type, string name, bool declareField, string backingFieldName = null)
		{
			return PublicGetPubPrivSetBacking (type, name, false, declareField, backingFieldName);
		}

		public static CSProperty PublicGetBacking (CSType type, CSIdentifier name, CSIdentifier backingFieldName,
			bool includeBackingFieldDeclaration = false, CSMethodKind methodKind = CSMethodKind.None)
		{
			LineCodeElementCollection<ICodeElement> getCode =
				new LineCodeElementCollection<ICodeElement> (
					new ICodeElement [] {
						CSReturn.ReturnLine (Exceptions.ThrowOnNull(backingFieldName, nameof(backingFieldName)))
					}, false, true);
			CSProperty prop = new CSProperty (type, methodKind, Exceptions.ThrowOnNull (name, nameof (name)),
				CSVisibility.Public, new CSCodeBlock (getCode),
				CSVisibility.Public, null);
			if (includeBackingFieldDeclaration)
				prop.Insert (0, CSFieldDeclaration.FieldLine (type, backingFieldName));
			return prop;
		}


		public static CSProperty PublicGetBacking (CSType type, string name, string backingFieldName, bool includeBackingFieldDeclaration = false)
		{
			return PublicGetBacking (type,
						 new CSIdentifier (Exceptions.ThrowOnNull (name, nameof (name))),
						 new CSIdentifier (Exceptions.ThrowOnNull (backingFieldName, nameof (backingFieldName))),
						 includeBackingFieldDeclaration);
		}

		static string MassageName (string name)
		{
			StringBuilder sb = new StringBuilder ();
			sb.Append ("_");
			sb.Append (Char.ToLowerInvariant (name [0]));
			if (name.Length > 0)
				sb.Append (name.Substring (1));
			return sb.ToString ();
		}
	}
}

