// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Dynamo.CSLang;

/// <summary>
/// Represents a top-level declaration in C#. This can include classes, interfaces, enums, etc.
/// </summary>
public interface ICSTopLevelDeclaration : ICodeElement
{
}

/// <summary>
/// Represents a collection of top-level declarations in C#.
/// </summary>
public class CSTopLevelDeclarations : CodeElementCollection<ICSTopLevelDeclaration>
{
    public CSTopLevelDeclarations(params ICSTopLevelDeclaration[] decls)
        : base()
    {
        AddRange(decls);
    }
}

