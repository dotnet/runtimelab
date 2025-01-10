// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    public sealed record ModuleParsingResult(
        ModuleDecl ModuleDecl,
        Dictionary<NamedTypeSpec, TypeDecl> TypeDecls,
        List<NamedTypeSpec> ClosedGenericTypes
    );
}
