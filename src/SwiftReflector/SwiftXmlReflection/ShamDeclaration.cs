// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SwiftReflector.ExceptionTools;

namespace SwiftReflector.SwiftXmlReflection {
	public class ShamDeclaration : TypeDeclaration {
		public ShamDeclaration (string fullName, EntityType type)
		{
			fullUnrootedName = fullName;
			Tuple<string, string> modName = fullName.SplitModuleFromName ();
			unrootedName = modName.Item2;
			IsUnrooted = true;
			TypeKind kind = TypeKind.Unknown;
			switch (type) {
			case EntityType.Scalar:
				kind = TypeKind.Struct;
				break;
			case EntityType.Class:
				kind = TypeKind.Class;
				break;
			case EntityType.Struct:
				kind = TypeKind.Struct;
				break;
			case EntityType.Enum:
			case EntityType.TrivialEnum:
				kind = TypeKind.Enum;
				break;
			default:
				break;
			}
			Kind = kind;
			Module = new ModuleDeclaration ();
			Module.Name = modName.Item1;
		}

		protected override void GatherXObjects (System.Collections.Generic.List<System.Xml.Linq.XObject> xobjects)
		{
			throw ErrorHelper.CreateError (ReflectorError.kCantHappenBase + 1, $"Attempt to serialize a sham type for {this.ToFullyQualifiedName (true)}. Likely this type was never fully realized.");
		}
	}
}

