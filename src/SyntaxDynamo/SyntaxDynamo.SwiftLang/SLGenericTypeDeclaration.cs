// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntaxDynamo.SwiftLang
{
	public class SLGenericTypeDeclaration
	{
		public SLGenericTypeDeclaration(SLIdentifier name)
		{
			Name = Exceptions.ThrowOnNull(name, nameof(name));
			Constraints = new List<SLGenericConstraint>();
		}

		public SLIdentifier Name { get; private set; }
		public List<SLGenericConstraint> Constraints { get; private set; }
	}

	public class SLGenericTypeDeclarationCollection : List<SLGenericTypeDeclaration>, ICodeElementSet
	{
		public SLGenericTypeDeclarationCollection()
			: base()
		{
		}

		public IEnumerable<ICodeElement> ConstraintElements
		{
			get
			{
				List<SLGenericTypeDeclaration> constrained = this.Where(decl => decl.Constraints.Count > 0).ToList();
				if (constrained.Count == 0)
					yield break;
				yield return new SimpleElement(" where ", true);
				bool first = true;
				foreach (SLGenericTypeDeclaration decl in constrained)
				{
					foreach (SLGenericConstraint constraint in decl.Constraints)
					{
						if (!first)
						{
							yield return new SimpleElement(",", true);
							yield return SimpleElement.Spacer;
						}
						else
						{
							first = false;
						}
						yield return constraint;
					}
				}
			}
		}

		public IEnumerable<ICodeElement> Elements
		{
			get
			{
				if (this.Count > 0)
				{
					yield return new SimpleElement("<");
					bool first = true;
					foreach (SLGenericTypeDeclaration decl in this)
					{
						if (!first)
						{
							yield return new SimpleElement(",", true);
							yield return SimpleElement.Spacer;
						}
						else {
							first = false;
						}
						yield return decl.Name;
					}
					yield return new SimpleElement(">");
				}
			}
		}

		public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

		public event EventHandler<WriteEventArgs> End = (s, e) => { };

		public virtual object BeginWrite(ICodeWriter writer)
		{
			OnBeginWrite(new WriteEventArgs(writer));
			return null;
		}

		protected virtual void OnBeginWrite(WriteEventArgs args)
		{
			Begin(this, args);
		}

		public virtual void Write(ICodeWriter writer, object o)
		{
		}

		public virtual void EndWrite(ICodeWriter writer, object o)
		{
			OnEndWrite(new WriteEventArgs(writer));
		}

		protected virtual void OnEndWrite(WriteEventArgs args)
		{
			End.FireInReverse(this, args);
		}
	}
}
