// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.CSLang {
	public interface ICSTopLevelDeclaration : ICodeElement {
	}

	public class CSTopLevelDeclations : CodeElementCollection<ICSTopLevelDeclaration> {
		public CSTopLevelDeclations (params ICSTopLevelDeclaration [] decls)
			: base ()
		{
			AddRange (Exceptions.ThrowOnNull (decls, "decls"));
		}


	}
}

