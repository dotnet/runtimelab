// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SwiftReflector.SwiftXmlReflection;

namespace SwiftReflector.Parser
{
    public interface ISwiftParser
    {
        public ModuleDeclaration GetModuleDeclaration(string filePath, ErrorHandling errors);
    }
}