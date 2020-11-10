﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace System.Text.Json.SourceGeneration
{
        public class JsonSerializableSyntaxReceiver : ISyntaxReceiver
        {
            public List<CompilationUnitSyntax> CompilationUnits { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is CompilationUnitSyntax compilationUnit)
                {
                    CompilationUnits.Add(compilationUnit);
                }
            }
        }
}
