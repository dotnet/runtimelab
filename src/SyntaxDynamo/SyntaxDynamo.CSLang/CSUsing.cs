// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang {
	public class CSUsing : SimpleLineElement {
		public CSUsing (string package)
			: base (string.Format ("using {0};", Exceptions.ThrowOnNull (package, nameof(package))), false, false, false)
		{
			Package = package;
		}

		public string Package { get; private set; }
	}

	public class CSUsingPackages : CodeElementCollection<CSUsing> {
		public CSUsingPackages () : base () { }
		public CSUsingPackages (params CSUsing [] use)
			: this ()
		{
			AddRange (use);
		}
		public CSUsingPackages (params string [] use)
			: this ()
		{
			AddRange (use.Select (s => new CSUsing (s)));
		}

		public CSUsingPackages And (CSUsing use)
		{
			Add (use);
			return this;
		}

		public CSUsingPackages And (string package) { return And (new CSUsing (package)); }

		public void AddIfNotPresent (string package, CSIdentifier protectedBy = null)
		{
			if (String.IsNullOrEmpty (package))
				return;
			CSUsing target = new CSUsing (package);
			if (!this.Exists (use => use.Contents == target.Contents)) {
				if ((object)protectedBy != null) {
					CSConditionalCompilation.ProtectWithIfEndif (protectedBy, target);
				}
				Add (target);
			}
		}

		public void AddIfNotPresent (Type t, CSIdentifier protectedBy = null)
		{
			AddIfNotPresent (t.Namespace, protectedBy);
		}
	}
}

