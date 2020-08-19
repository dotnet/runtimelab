using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

#nullable enable

namespace Microsoft.Interop
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ManualTypeMarshallingAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticIdPrefix = "INTEROPGEN";
        private const string Category = "Interoperability";

        public readonly static DiagnosticDescriptor BlittableTypeMustBeBlittableRule =
            new DiagnosticDescriptor(
                "INTEROPGEN001",
                "BlittableTypeMustBeBlittable",
                "Type '{0}' is marked with BlittableTypeAttribute but is not blittable.",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "A type marked with BlittableTypeAttribute must be blittable.");

        public readonly static DiagnosticDescriptor CannotHaveMultipleMarshallingAttributesRule =
            new DiagnosticDescriptor(
                "INTEROPGEN002",
                "CannotHaveMultipleMarshallingAttributes",
                "Type '{0}' is marked with BlittableTypeAttribute and NativeMarshallingAttribute. A type can only have one of these two attributes.",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "The BlittableTypeAttribute and NativeMarshallingAttributes are mutually exclusive.");

                
        public readonly static DiagnosticDescriptor NativeTypeMustBeNonNullRule =
            new DiagnosticDescriptor(
                "INTEROPGEN003",
                "NativeTypeMustBeBlittable",
                "The native type for the type '{0}' is null.",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "A native type for a given type must be non-null.");

        public readonly static DiagnosticDescriptor NativeTypeMustBeBlittableRule =
            new DiagnosticDescriptor(
                "INTEROPGEN004",
                "NativeTypeMustBeBlittable",
                "The native type '{0}' for the type '{1}' is not blittable.",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "A native type for a given type must be blittable.");

        public readonly static DiagnosticDescriptor GetPinnableReferenceReturnTypeBlittableRule =
            new DiagnosticDescriptor(
                "INTEROPGEN005",
                "GetPinnableReferenceReturnTypeBlittable",
                "The dereferenced type of the return type of the GetPinnableReference method must be blittable.",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "The return type of GetPinnableReference (after accounting for ref) must be blittable.");
    
        public readonly static DiagnosticDescriptor NativeTypeMustBePointerSizedRule =
            new DiagnosticDescriptor(
                "INTEROPGEN006",
                "NativeTypeMustBePointerSized",
                "The native type '{0}' must be pointer sized because the managed type '{1}' has a GetPinnableReference method.",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "The native type must be pointer sized so we can cast the pinned result of GetPinnableReference to the native type.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(
                BlittableTypeMustBeBlittableRule,
                CannotHaveMultipleMarshallingAttributesRule,
                NativeTypeMustBeNonNullRule,
                NativeTypeMustBeBlittableRule,
                GetPinnableReferenceReturnTypeBlittableRule,
                NativeTypeMustBePointerSizedRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
            var generatedMarshallingAttribute = context.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.GeneratedMarshallingAttribute");
            var blittableTypeAttribute = context.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.BlittableTypeAttribute");
            var nativeMarshallingAttribute = context.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.NativeMarshallingAttribute");

            AttributeData? blittableTypeAttributeData = null;
            AttributeData? nativeMarshallingAttributeData = null;
            foreach (var attr in type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, generatedMarshallingAttribute))
                {
                    // If the type has the GeneratedMarshallingAttribute,
                    // we let the source generator handle error checking.
                    return;
                }
                else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, blittableTypeAttribute))
                {
                    blittableTypeAttributeData = attr;
                }
                else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, nativeMarshallingAttribute))
                {
                    nativeMarshallingAttributeData = attr;
                }
            }

            if (blittableTypeAttributeData is not null && nativeMarshallingAttributeData is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(CannotHaveMultipleMarshallingAttributesRule, blittableTypeAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.Name));
            }
            else if (blittableTypeAttributeData is not null && !type.HasOnlyBlittableFields())
            {
                context.ReportDiagnostic(Diagnostic.Create(BlittableTypeMustBeBlittableRule, blittableTypeAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.Name));
            }
            else if (nativeMarshallingAttributeData is not null)
            {
                if (nativeMarshallingAttributeData.ConstructorArguments[0].IsNull)
                {
                    context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustBeNonNullRule, nativeMarshallingAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.Name));
                }
                else
                {
                    ITypeSymbol nativeType = (ITypeSymbol)nativeMarshallingAttributeData.ConstructorArguments[0].Value!;
                    IPropertySymbol? valueProperty = nativeType.GetMembers("Value").OfType<IPropertySymbol>().FirstOrDefault();
                    if (valueProperty is not null)
                    {
                        nativeType = valueProperty.Type;
                    }
                    if (!nativeType.IsConsideredBlittable())
                    {
                        context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustBeBlittableRule,
                            valueProperty is not null
                            ? valueProperty.DeclaringSyntaxReferences[0].GetSyntax().GetLocation()
                            : nativeType.DeclaringSyntaxReferences[0].GetSyntax().GetLocation(),
                            nativeType.Name,
                            type.Name));
                    }

                    IMethodSymbol? getPinnableReferenceMethod = nativeType.GetMembers("GetPinnableReference")
                                                                         .OfType<IMethodSymbol>()
                                                                         .FirstOrDefault(m => m is {Parameters: { Length: 0 } } and ({ReturnsByRef : true} or {ReturnsByRefReadonly : true}));
                    if (getPinnableReferenceMethod is not null)
                    {
                        if (!getPinnableReferenceMethod.ReturnType.IsConsideredBlittable())
                        {
                            context.ReportDiagnostic(Diagnostic.Create(GetPinnableReferenceReturnTypeBlittableRule, getPinnableReferenceMethod.DeclaringSyntaxReferences[0].GetSyntax().GetLocation()));
                        }
                        if (valueProperty is null or
                            not { Type : IPointerTypeSymbol _ 
                                or { SpecialType : SpecialType.System_IntPtr }
                                or { SpecialType : SpecialType.System_UIntPtr }})
                        {
                            context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustBePointerSizedRule,
                                valueProperty is not null
                                ? valueProperty.DeclaringSyntaxReferences[0].GetSyntax().GetLocation()
                                : nativeType.DeclaringSyntaxReferences[0].GetSyntax().GetLocation(),
                                nativeType.Name,
                                type.Name));
                        }
                    }
                }
            }
        }
    }
}