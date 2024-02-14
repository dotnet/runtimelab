// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.SwiftLang {
	public class SLParameterList : DelegatedSimpleElement {
		public SLParameterList ()
		{
			Parameters = new List<SLParameter> ();
		}

		public SLParameterList (IEnumerable <SLParameter> parameters)
			: this ()
		{
			Parameters.AddRange (parameters);
		}

		public SLParameterList (params SLParameter[] parameters)
			: this ()
		{
			Parameters.AddRange (parameters);
		}

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ('(', true);
			for (int i = 0; i < Parameters.Count; i++) {
				if (i > 0) {
					writer.Write (", ", true);
				}
				Parameters [i].WriteAll (writer);
			}
			writer.Write (')', true);
		}

		public List<SLParameter> Parameters { get; private set; }
	}
}
