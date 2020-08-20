using System;
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

        public readonly static DiagnosticDescriptor NativeTypeMustHaveRequiredShapeRule =
            new DiagnosticDescriptor(
                "INTEROPGEN007",
                "NativeTypeMustHaveRequiredShape",
                "The native type '{0}' have a constructor that takes one parameter of type '{1}' or a parameterless instance method named 'ToManaged' that returns '{1}'.",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "The native type must have at least one of the two marshalling methods to enable marshalling the managed type.");

        public readonly static DiagnosticDescriptor ValuePropertyMustHaveSetterRule =
            new DiagnosticDescriptor(
                "INTEROPGEN008",
                "ValuePropertyMustHaveSetter",
                "The 'Value' property on the native type '{0}' must have a setter.",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "The native type's Value property must have a setter to support marshalling from native to managed.");

        public readonly static DiagnosticDescriptor ValuePropertyMustHaveGetterRule =
            new DiagnosticDescriptor(
                "INTEROPGEN009",
                "ValuePropertyMustHaveGetter",
                "The 'Value' property on the native type '{0}' must have a getter.",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "The native type's Value property must have a getter to support marshalling from managed to native.");

        public readonly static DiagnosticDescriptor GetPinnableReferenceShouldSupportAllocatingMarshallingFallbackRule =
            new DiagnosticDescriptor(
                "INTEROPGEN010",
                "GetPinnableReferenceShouldSupportAllocatingMarshallingFallback",
                "Type '{0}' has a GetPinnableReference method but its native type does not support marshalling in scenarios where pinning is impossible.",
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: "A type that supports marshalling from managed to native by pinning should also support marshalling from managed to native where pinning is impossible.");

        public readonly static DiagnosticDescriptor StackallocMarshallingShouldSupportAllocatingMarshallingFallbackRule =
            new DiagnosticDescriptor(
                "INTEROPGEN011",
                "StackallocMarshallingShouldSupportAllocatingMarshallingFallback",
                "Native type '{0}' has a stack-allocating constructor does not support marshalling in scenarios where stack allocation is impossible.",
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: "A type that supports marshalling from managed to native by stack allocation should also support marshalling from managed to native where stack allocation is impossible.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(
                BlittableTypeMustBeBlittableRule,
                CannotHaveMultipleMarshallingAttributesRule,
                NativeTypeMustBeNonNullRule,
                NativeTypeMustBeBlittableRule,
                GetPinnableReferenceReturnTypeBlittableRule,
                NativeTypeMustBePointerSizedRule,
                NativeTypeMustHaveRequiredShapeRule,
                ValuePropertyMustHaveSetterRule,
                ValuePropertyMustHaveGetterRule,
                GetPinnableReferenceShouldSupportAllocatingMarshallingFallbackRule,
                StackallocMarshallingShouldSupportAllocatingMarshallingFallbackRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeTypeDefinition, SymbolKind.NamedType);
            context.RegisterSymbolAction(AnalyzeElement, SymbolKind.Parameter, SymbolKind.Field);
            context.RegisterSymbolAction(AnalyzeReturnType, SymbolKind.Method);
        }

        private void AnalyzeTypeDefinition(SymbolAnalysisContext context)
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
                context.ReportDiagnostic(Diagnostic.Create(CannotHaveMultipleMarshallingAttributesRule, blittableTypeAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.ToDisplayString()));
            }
            else if (blittableTypeAttributeData is not null && !type.HasOnlyBlittableFields())
            {
                context.ReportDiagnostic(Diagnostic.Create(BlittableTypeMustBeBlittableRule, blittableTypeAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.ToDisplayString()));
            }
            else if (nativeMarshallingAttributeData is not null)
            {
                if (nativeMarshallingAttributeData.ConstructorArguments[0].IsNull)
                {
                    context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustBeNonNullRule, nativeMarshallingAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.ToDisplayString()));
                }
                else
                {
                    AnalyzeNativeMarshalerType(context, type, nativeMarshallingAttributeData, validateGetPinnableReference: true, validateAllScenarioSupport: true);
                }
            }
        }
        
        private void AnalyzeElement(SymbolAnalysisContext context)
        {
            var marshalUsingAttribute = context.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.MarshalUsingAttribute");
            AttributeData? attrData = context.Symbol.GetAttributes().FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(marshalUsingAttribute, attr.AttributeClass));
            if (attrData is not null)
            {
                if (context.Symbol is IParameterSymbol param)
                {
                    AnalyzeNativeMarshalerType(context, param.Type, attrData, false, false);
                }
                else if (context.Symbol is IFieldSymbol field)
                {
                    AnalyzeNativeMarshalerType(context, field.Type, attrData, false, false);
                }
            }
        }

        private void AnalyzeReturnType(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;
            var marshalUsingAttribute = context.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.MarshalUsingAttribute");
            AttributeData? attrData = method.ReturnType.GetAttributes().FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(marshalUsingAttribute, attr.AttributeClass));
            if (attrData is not null)
            {
                AnalyzeNativeMarshalerType(context, method.ReturnType, attrData, false, false);
            }
        }

        private void AnalyzeNativeMarshalerType(SymbolAnalysisContext context, ITypeSymbol type, AttributeData nativeMarshalerAttributeData, bool validateGetPinnableReference, bool validateAllScenarioSupport)
        {
            INamedTypeSymbol spanOfByte = context.Compilation.GetTypeByMetadataName("System.Span`1")!.Construct(context.Compilation.GetSpecialType(SpecialType.System_Byte));

            ITypeSymbol nativeType = (ITypeSymbol)nativeMarshalerAttributeData.ConstructorArguments[0].Value!;
            
            if (nativeType is not INamedTypeSymbol marshalerType)
            {
                context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustHaveRequiredShapeRule, nativeMarshalerAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), nativeType.ToDisplayString(), type.ToDisplayString()));
                return;
            }

            bool hasConstructor = false;
            bool hasStackallocConstructor = false;
            foreach (var ctor in marshalerType.Constructors)
            {
                if (ctor.IsStatic)
                {
                    continue;
                }
                if (ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(type, ctor.Parameters[0].Type))
                {
                    hasConstructor = true;
                }
                if (ctor.Parameters.Length == 2 && SymbolEqualityComparer.Default.Equals(type, ctor.Parameters[0].Type)
                                                && SymbolEqualityComparer.Default.Equals(spanOfByte, ctor.Parameters[1].Type))
                {
                    hasStackallocConstructor = true;
                }
            }

            bool hasToManaged = marshalerType.GetMembers("ToManaged")
                                                .OfType<IMethodSymbol>()
                                                .Any(m => m.Parameters.IsEmpty && !m.ReturnsByRef && !m.ReturnsByRefReadonly && SymbolEqualityComparer.Default.Equals(m.ReturnType, type) && !m.IsStatic);

            if (!hasConstructor && !hasStackallocConstructor && !hasToManaged)
            {
                context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustHaveRequiredShapeRule, GetSyntaxReferenceForDiagnostic(marshalerType).GetSyntax().GetLocation(), marshalerType.ToDisplayString(), type.ToDisplayString()));
            }

            if (validateAllScenarioSupport && hasStackallocConstructor && !hasConstructor)
            {
                context.ReportDiagnostic(Diagnostic.Create(StackallocMarshallingShouldSupportAllocatingMarshallingFallbackRule, GetSyntaxReferenceForDiagnostic(marshalerType).GetSyntax().GetLocation(), marshalerType.ToDisplayString()));
            }

            IPropertySymbol? valueProperty = nativeType.GetMembers("Value").OfType<IPropertySymbol>().FirstOrDefault();                    
            if (valueProperty is not null)
            {
                nativeType = valueProperty.Type;

                if ((hasConstructor || hasStackallocConstructor) && valueProperty.GetMethod is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ValuePropertyMustHaveGetterRule, GetSyntaxReferenceForDiagnostic(valueProperty).GetSyntax().GetLocation(), marshalerType.ToDisplayString()));
                }
                if (hasToManaged && valueProperty.SetMethod is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ValuePropertyMustHaveSetterRule, GetSyntaxReferenceForDiagnostic(valueProperty).GetSyntax().GetLocation(), marshalerType.ToDisplayString()));
                }
            }
            if (!nativeType.IsConsideredBlittable())
            {
                context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustBeBlittableRule,
                    valueProperty is not null
                    ? GetSyntaxReferenceForDiagnostic(valueProperty).GetSyntax().GetLocation()
                    : GetSyntaxReferenceForDiagnostic(nativeType).GetSyntax().GetLocation(),
                    nativeType.ToDisplayString(),
                    type.ToDisplayString()));
            }

            IMethodSymbol? getPinnableReferenceMethod = type.GetMembers("GetPinnableReference")
                                                            .OfType<IMethodSymbol>()
                                                            .FirstOrDefault(m => m is {Parameters: { Length: 0 } } and ({ReturnsByRef : true} or {ReturnsByRefReadonly : true}));
            if (validateGetPinnableReference && getPinnableReferenceMethod is not null)
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
                        ? GetSyntaxReferenceForDiagnostic(valueProperty).GetSyntax().GetLocation()
                        : GetSyntaxReferenceForDiagnostic(nativeType).GetSyntax().GetLocation(),
                        nativeType.ToDisplayString(),
                        type.ToDisplayString()));
                }

                if (validateAllScenarioSupport && (!hasConstructor || valueProperty is { GetMethod : null }))
                {
                    context.ReportDiagnostic(Diagnostic.Create(GetPinnableReferenceShouldSupportAllocatingMarshallingFallbackRule, nativeMarshalerAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.ToDisplayString()));
                }
            }

            SyntaxReference GetSyntaxReferenceForDiagnostic(ISymbol targetSymbol)
            {
                if (targetSymbol.DeclaringSyntaxReferences.IsEmpty)
                {
                    return nativeMarshalerAttributeData.ApplicationSyntaxReference!;
                }
                else
                {
                    return targetSymbol.DeclaringSyntaxReferences[0];
                }
            }
        }
    }
}