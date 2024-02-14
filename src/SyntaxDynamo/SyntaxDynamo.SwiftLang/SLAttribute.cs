// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Text;

namespace SyntaxDynamo.SwiftLang {
	public class SLAttribute : LineCodeElementCollection<ICodeElement> {
		public SLAttribute (SLIdentifier name, CommaListElementCollection<SLBaseExpr> args, bool isSingleLine = false)
			: base (isSingleLine, false, isSingleLine)
		{
			Name = Exceptions.ThrowOnNull (name, nameof (name));
			Add (new SimpleElement ("@"));
			var stringRep = new StringBuilder (Name.Name);
			Add (Name);
			if (args != null) {
				Add (new SimpleElement ("("));
				Add (args);
				Add (new SimpleElement (")"));
				stringRep.Append ("(");
				foreach (var arg in args) {
					if (arg is SLIdentifier argId) {
						stringRep.Append (argId.Name);
					}
				}
				stringRep.Append (")");
			}
			StringRep = stringRep.ToString ();
		}

		public SLAttribute (string name, CommaListElementCollection<SLBaseExpr> args = null)
			: this (new SLIdentifier (name), args)
		{
		}

		public SLAttribute (string name, bool isSingleLine, params SLBaseExpr [] args)
			: this (new SLIdentifier (name), new CommaListElementCollection<SLBaseExpr> (args), isSingleLine)
		{
		}

		public SLIdentifier Name { get; private set; }

		public string StringRep { get; private set; }

		static SLAttribute objc;

		public static SLAttribute ObjC ()
		{
			if (objc == null) {
				objc = new SLAttribute ("objc");
			}
			return objc;
		}

		static SLAttribute convc;

		public static SLAttribute ConventionC ()
		{
			if (convc == null) {
				convc = new SLAttribute ("convention", new CommaListElementCollection<SLBaseExpr> () { new SLIdentifier ("c") });
			}
			return convc;
		}

		static SLAttribute escaping;

		public static SLAttribute Escaping ()
		{
			if (escaping == null) {
				escaping = new SLAttribute ("escaping");
			}
			return escaping;
		}

		void SLAttributeWriteAll (object sender, WriteEventArgs eventArgs)
		{
			this.WriteAll (eventArgs.Writer);	
		}
	}
}
