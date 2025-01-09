namespace BindingsGeneration
{
    public sealed record ModuleParsingResult(
        ModuleDecl ModuleDecl,
        Dictionary<NamedTypeSpec, TypeDecl> TypeDecls
    );
}
