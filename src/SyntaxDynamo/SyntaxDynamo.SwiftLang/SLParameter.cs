// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace SyntaxDynamo.SwiftLang {

	public class SLUnnamedParameter : DelegatedSimpleElement {
		public SLUnnamedParameter (SLType type, SLParameterKind kind = SLParameterKind.None)
		{
			ParameterKind = kind;
			TypeAnnotation = Exceptions.ThrowOnNull (type, nameof (type));
		}
		public SLParameterKind ParameterKind { get; private set; }
		public SLType TypeAnnotation { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			if (ParameterKind != SLParameterKind.None) {
				writer.Write (SLNameTypePair.ToParameterKindString (ParameterKind), false);
				writer.Write (' ', false);
			}
			TypeAnnotation.WriteAll (writer);
		}
	}


	public class SLParameter : SLUnnamedParameter {
		public SLParameter (SLIdentifier publicName, SLIdentifier privateName, SLType type, SLParameterKind kind = SLParameterKind.None)
			: base (type, kind)
		{
			PublicName = publicName;
			PrivateName = Exceptions.ThrowOnNull (privateName, nameof (privateName));
		}

		public SLParameter (string publicName, string privateName, SLType type, SLParameterKind kind = SLParameterKind.None)
			: this (publicName != null ? new SLIdentifier (publicName) : null, new SLIdentifier (privateName), type, kind)
		{
		}

		public SLParameter (SLIdentifier name, SLType type, SLParameterKind kind = SLParameterKind.None)
			: this (name, name, type, kind)
		{

		}

		public SLParameter (string name, SLType type, SLParameterKind kind = SLParameterKind.None)
			: this (name, name, type, kind)
		{

		}

		public SLIdentifier PublicName { get; private set; }
		public SLIdentifier PrivateName { get; private set; }
		public bool PublicNameIsOptional { get { return PublicName == null || String.IsNullOrEmpty (PublicName.Name); } }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			if (PublicNameIsOptional) {
				writer.Write ("_ ", true);
				PrivateName.WriteAll (writer);
			} else if (PublicName.Name == PrivateName.Name) {
				PrivateName.WriteAll (writer);
			} else {
				PublicName.WriteAll (writer);
				writer.Write (" ", true);
				PrivateName.WriteAll (writer);
			}
			writer.Write (": ", true);
			base.LLWrite (writer, o);
		}
	}
}

