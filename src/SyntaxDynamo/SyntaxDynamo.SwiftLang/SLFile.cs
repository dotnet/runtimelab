// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.SwiftLang {
	public class SLFile : ICodeElementSet {
		public SLFile (SLImportModules import)
		{
			Imports = import ?? new SLImportModules ();
			Declarations = new List<ICodeElement> ();
			Classes = new SLClasses ();
			Functions = new List<SLFunc> ();
			Trailer = new List<ICodeElement> ();
		}

		public SLImportModules Imports { get; private set; }
		public List<ICodeElement> Declarations { get; private set; }
		public SLClasses Classes { get; private set; }
		public List<SLFunc> Functions { get; private set; }
		public List<ICodeElement> Trailer { get; private set; }

		#region ICodeElem implementation

		public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

		public event EventHandler<WriteEventArgs> End = (s, e) => { };

		public object BeginWrite (ICodeWriter writer)
		{
			OnBegin (new WriteEventArgs (writer));
			return null;
		}

		protected virtual void OnBegin (WriteEventArgs args)
		{
			Begin (this, args);
		}

		public void Write (ICodeWriter writer, object o)
		{
		}

		public void EndWrite (ICodeWriter writer, object o)
		{
			OnEnd (new WriteEventArgs (writer));
		}

		protected virtual void OnEnd (WriteEventArgs args)
		{
			End (this, args);
		}

		#endregion

		#region ICodeElemSet implementation

		public IEnumerable<ICodeElement> Elements {
			get {
				yield return Imports;
				foreach (var decl in Declarations)
					yield return decl;
				yield return Classes;
				foreach (var func in Functions)
					yield return func;
				foreach (var trailerElem in Trailer)
					yield return trailerElem;
			}
		}

		#endregion
	}
}

