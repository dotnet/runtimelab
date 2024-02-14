// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.SwiftLang {
	public class SLCase : DelegatedSimpleElement, ISLStatement {
		public SLCase (ICodeElement caseExpr, ICodeElement actions)
		{
			CaseExpr = caseExpr;
			Actions = actions;
		}

		public ICodeElement CaseExpr { get; private set; }
		public ICodeElement Actions { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.BeginNewLine (true);
			if (CaseExpr != null) {
				writer.Write ("case ", true);
				CaseExpr.WriteAll (writer);
			} else {
				writer.Write ("default", true);
			}
			writer.Write (" : ", true);
			if (Actions != null) {
				if (Actions is ICodeElementSet) {
					writer.EndLine ();
					writer.Indent ();
					writer.BeginNewLine (true);
				}
				Actions.WriteAll (writer);
				writer.EndLine ();
				if (Actions is ICodeElementSet) {
					writer.Exdent ();
				}
			}
		}
	}

	public class SLSwitch : ICodeElementSet, ISLStatement, ISLLineable {
		public SLSwitch (ISLExpr switchOn, IEnumerable<SLCase> cases)
		{
			Cases = new List<SLCase> ();
			if (cases != null) {
				Cases.AddRange (cases);
			}
			SwitchOn = switchOn;
		}

		public ISLExpr SwitchOn { get; set; }
		public List<SLCase> Cases { get; private set; }

		#region ICodeElem implementation

		public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

		public event EventHandler<WriteEventArgs> End = (s, e) => { };

		public virtual object BeginWrite (ICodeWriter writer)
		{
			writer.BeginNewLine (true);
			OnBeginWrite (new WriteEventArgs (writer));
			return null;
		}

		protected virtual void OnBeginWrite (WriteEventArgs args)
		{
			Begin (this, args);
		}

		public virtual void Write (ICodeWriter writer, object o)
		{
		}

		public virtual void EndWrite (ICodeWriter writer, object o)
		{
			OnEndWrite (new WriteEventArgs (writer));
		}

		protected virtual void OnEndWrite (WriteEventArgs args)
		{
			End.FireInReverse (this, args);
		}


		#endregion

		#region ICodeElemSet implementation

		public IEnumerable<ICodeElement> Elements {
			get {
				yield return new SimpleElement ("switch");
				yield return SimpleElement.Spacer;
				yield return SwitchOn;
				yield return SimpleElement.Spacer;
				SLCodeBlock caseBlock = new SLCodeBlock (Cases);
				yield return caseBlock;
			}
		}

		#endregion
	}
}

