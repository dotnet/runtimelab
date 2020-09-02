using System;
using System.Collections.Immutable;
using System.Linq;
using DllImportGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

#nullable enable

namespace Microsoft.Interop
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ManualTypeMarshallingAnalyzer : DiagnosticAnalyzer
    {
        private const string Category = "Usage";
        public readonly static DiagnosticDescriptor BlittableTypeMustBeBlittableRule =
            new DiagnosticDescriptor(
                "INTEROPGEN001",
                "BlittableTypeMustBeBlittable",
                new LocalizableResourceString(nameof(Resources.BlittableTypeMustBeBlittableMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.BlittableTypeMustBeBlittableDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor CannotHaveMultipleMarshallingAttributesRule =
            new DiagnosticDescriptor(
                "INTEROPGEN002",
                "CannotHaveMultipleMarshallingAttributes",
                new LocalizableResourceString(nameof(Resources.CannotHaveMultipleMarshallingAttributesMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.CannotHaveMultipleMarshallingAttributesDescription), Resources.ResourceManager, typeof(Resources)));

                
        public readonly static DiagnosticDescriptor NativeTypeMustBeNonNullRule =
            new DiagnosticDescriptor(
                "INTEROPGEN003",
                "NativeTypeMustBeNonNull",
                new LocalizableResourceString(nameof(Resources.NativeTypeMustBeNonNullMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.NativeTypeMustBeNonNullDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor NativeTypeMustBeBlittableRule =
            new DiagnosticDescriptor(
                "INTEROPGEN004",
                "NativeTypeMustBeBlittable",
                new LocalizableResourceString(nameof(Resources.NativeTypeMustBeBlittableMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.BlittableTypeMustBeBlittableDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor GetPinnableReferenceReturnTypeBlittableRule =
            new DiagnosticDescriptor(
                "INTEROPGEN005",
                "GetPinnableReferenceReturnTypeBlittable",
                new LocalizableResourceString(nameof(Resources.GetPinnableReferenceReturnTypeBlittableMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.GetPinnableReferenceReturnTypeBlittableDescription), Resources.ResourceManager, typeof(Resources)));
    
        public readonly static DiagnosticDescriptor NativeTypeMustBePointerSizedRule =
            new DiagnosticDescriptor(
                "INTEROPGEN006",
                "NativeTypeMustBePointerSized",
                new LocalizableResourceString(nameof(Resources.NativeTypeMustBePointerSizedMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.NativeTypeMustBePointerSizedDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor NativeTypeMustHaveRequiredShapeRule =
            new DiagnosticDescriptor(
                "INTEROPGEN007",
                "NativeTypeMustHaveRequiredShape",
                new LocalizableResourceString(nameof(Resources.NativeTypeMustHaveRequiredShapeMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.NativeTypeMustHaveRequiredShapeDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor ValuePropertyMustHaveSetterRule =
            new DiagnosticDescriptor(
                "INTEROPGEN008",
                "ValuePropertyMustHaveSetter",
                new LocalizableResourceString(nameof(Resources.ValuePropertyMustHaveSetterMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.ValuePropertyMustHaveSetterDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor ValuePropertyMustHaveGetterRule =
            new DiagnosticDescriptor(
                "INTEROPGEN009",
                "ValuePropertyMustHaveGetter",
                new LocalizableResourceString(nameof(Resources.ValuePropertyMustHaveGetterMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.ValuePropertyMustHaveGetterDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor GetPinnableReferenceShouldSupportAllocatingMarshallingFallbackRule =
            new DiagnosticDescriptor(
                "INTEROPGEN010",
                "GetPinnableReferenceShouldSupportAllocatingMarshallingFallback",
                new LocalizableResourceString(nameof(Resources.GetPinnableReferenceShouldSupportAllocatingMarshallingFallbackMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.GetPinnableReferenceShouldSupportAllocatingMarshallingFallbackDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor StackallocMarshallingShouldSupportAllocatingMarshallingFallbackRule =
            new DiagnosticDescriptor(
                "INTEROPGEN011",
                "StackallocMarshallingShouldSupportAllocatingMarshallingFallback",
                new LocalizableResourceString(nameof(Resources.StackallocMarshallingShouldSupportAllocatingMarshallingFallbackMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.StackallocMarshallingShouldSupportAllocatingMarshallingFallbackDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor ConditionallyBlittableTypeMustBeConditionallyBlittableRule =
            new DiagnosticDescriptor(
                "INTEROPGEN012",
                "ConditionallyBlittableTypeMustBeConditionallyBlittable",
                new LocalizableResourceString(nameof(Resources.ConditionallyBlittableGenericTypeMustBeConditionallyBlittableMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.ConditionallyBlittableGenericTypeMustBeConditionallyBlittableDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor ReferenceTypeParameterCanNeverBeBlittableRule =
            new DiagnosticDescriptor(
                "INTEROPGEN013",
                "ReferenceTypeParameterCanNeverBeBlittable",
                new LocalizableResourceString(nameof(Resources.ReferenceTypeParameterCanNeverBeBlittableMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.ReferenceTypeParameterCanNeverBeBlittableDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor BlittableTypeIfGenericParametersBlittableInvalidTargetRule =
            new DiagnosticDescriptor(
                "INTEROPGEN014",
                "BlittableTypeIfGenericParametersBlittableInvalidTarget",
                new LocalizableResourceString(nameof(Resources.BlittableTypeIfGenericParametersBlittableInvalidTargetMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.BlittableTypeIfGenericParametersBlittableInvalidTargetDescription), Resources.ResourceManager, typeof(Resources)));

        public readonly static DiagnosticDescriptor GenericTypeIndexMustBeInRangeRule =
            new DiagnosticDescriptor(
                "INTEROPGEN015",
                "GenericTypeIndexMustBeInRange",
                new LocalizableResourceString(nameof(Resources.GenericTypeIndexMustBeInRangeMessage), Resources.ResourceManager, typeof(Resources)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: new LocalizableResourceString(nameof(Resources.GenericTypeIndexMustBeInRangeDescription), Resources.ResourceManager, typeof(Resources)));

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
                StackallocMarshallingShouldSupportAllocatingMarshallingFallbackRule,
                ConditionallyBlittableTypeMustBeConditionallyBlittableRule,
                ReferenceTypeParameterCanNeverBeBlittableRule,
                BlittableTypeIfGenericParametersBlittableInvalidTargetRule,
                GenericTypeIndexMustBeInRangeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(PrepareForAnalysis);
        }

        private void PrepareForAnalysis(CompilationStartAnalysisContext context)
        {
            var generatedMarshallingAttribute = context.Compilation.GetTypeByMetadataName(TypeNames.GeneratedMarshallingAttribute);
            var blittableTypeAttribute = context.Compilation.GetTypeByMetadataName(TypeNames.BlittableTypeAttribute);
            var nativeMarshallingAttribute = context.Compilation.GetTypeByMetadataName(TypeNames.NativeMarshallingAttribute);
            var marshalUsingAttribute = context.Compilation.GetTypeByMetadataName(TypeNames.MarshalUsingAttribute);
            var blittableTypeIfGenericParametersBlittableAttribute = context.Compilation.GetTypeByMetadataName(TypeNames.BlittableTypeIfGenericParametersBlittableAttribute);
            var spanOfByte = context.Compilation.GetTypeByMetadataName(TypeNames.System_Span)!.Construct(context.Compilation.GetSpecialType(SpecialType.System_Byte));

            if (generatedMarshallingAttribute is not null &&
                blittableTypeAttribute is not null &&
                nativeMarshallingAttribute is not null &&
                marshalUsingAttribute is not null &&
                blittableTypeIfGenericParametersBlittableAttribute is not null &&
                spanOfByte is not null)
            {
                var perCompilationAnalyzer = new PerCompilationAnalyzer(
                    generatedMarshallingAttribute,
                    blittableTypeAttribute,
                    nativeMarshallingAttribute,
                    marshalUsingAttribute,
                    blittableTypeIfGenericParametersBlittableAttribute,
                    spanOfByte,
                    context.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.StructLayoutAttribute")!);
                context.RegisterSymbolAction(context => perCompilationAnalyzer.AnalyzeTypeDefinition(context), SymbolKind.NamedType);
                context.RegisterSymbolAction(context => perCompilationAnalyzer.AnalyzeElement(context), SymbolKind.Parameter, SymbolKind.Field);
                context.RegisterSymbolAction(context => perCompilationAnalyzer.AnalyzeReturnType(context), SymbolKind.Method);
            }
        }

        class PerCompilationAnalyzer
        {
            private readonly INamedTypeSymbol GeneratedMarshallingAttribute;
            private readonly INamedTypeSymbol BlittableTypeAttribute;
            private readonly INamedTypeSymbol NativeMarshallingAttribute;
            private readonly INamedTypeSymbol MarshalUsingAttribute;
            private readonly INamedTypeSymbol BlittableTypeIfGenericParametersBlittableAttribute;
            private readonly INamedTypeSymbol SpanOfByte;
            private readonly INamedTypeSymbol StructLayoutAttribute;

            public PerCompilationAnalyzer(INamedTypeSymbol generatedMarshallingAttribute,
                                          INamedTypeSymbol blittableTypeAttribute,
                                          INamedTypeSymbol nativeMarshallingAttribute,
                                          INamedTypeSymbol marshalUsingAttribute,
                                          INamedTypeSymbol blittableTypeIfGenericParametersBlittableAttribute,
                                          INamedTypeSymbol spanOfByte,
                                          INamedTypeSymbol structLayoutAttribute)
            {
                GeneratedMarshallingAttribute = generatedMarshallingAttribute;
                BlittableTypeAttribute = blittableTypeAttribute;
                NativeMarshallingAttribute = nativeMarshallingAttribute;
                MarshalUsingAttribute = marshalUsingAttribute;
                BlittableTypeIfGenericParametersBlittableAttribute = blittableTypeIfGenericParametersBlittableAttribute;
                SpanOfByte = spanOfByte;
                StructLayoutAttribute = structLayoutAttribute;
            }

            public void AnalyzeTypeDefinition(SymbolAnalysisContext context)
            {
                INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;

                AttributeData? blittableTypeAttributeData = null;
                AttributeData? nativeMarshallingAttributeData = null;
                AttributeData? blittableGenericTypeAttributeData = null;
                foreach (var attr in type.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, GeneratedMarshallingAttribute))
                    {
                        // If the type has the GeneratedMarshallingAttribute,
                        // we let the source generator handle error checking.
                        return;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, BlittableTypeAttribute))
                    {
                        blittableTypeAttributeData = attr;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, NativeMarshallingAttribute))
                    {
                        nativeMarshallingAttributeData = attr;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, BlittableTypeIfGenericParametersBlittableAttribute))
                    {
                        blittableGenericTypeAttributeData = attr;
                    }
                }

                if (HasMultipleMarshallingAttributes(blittableTypeAttributeData, nativeMarshallingAttributeData, blittableGenericTypeAttributeData))
                {
                    context.ReportDiagnostic(Diagnostic.Create(CannotHaveMultipleMarshallingAttributesRule, (blittableGenericTypeAttributeData ?? nativeMarshallingAttributeData)!.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.ToDisplayString()));
                }
                else if (blittableGenericTypeAttributeData is not null)
                {
                    AnalyzeConditionallyBlittableGeneric(context, type, blittableGenericTypeAttributeData);
                }
                else if (blittableTypeAttributeData is not null && (!type.HasOnlyBlittableFields() || type.IsAutoLayout(StructLayoutAttribute)))
                {
                    context.ReportDiagnostic(Diagnostic.Create(BlittableTypeMustBeBlittableRule, blittableTypeAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.ToDisplayString()));
                }
                else if (nativeMarshallingAttributeData is not null)
                {
                    AnalyzeNativeMarshalerType(context, type, nativeMarshallingAttributeData, validateGetPinnableReference: true, validateAllScenarioSupport: true);
                }
            }

            private bool HasMultipleMarshallingAttributes(AttributeData? blittableTypeAttributeData, AttributeData? nativeMarshallingAttributeData, AttributeData? blittableGenericTypeAttributeData)
            {
                return (blittableTypeAttributeData, nativeMarshallingAttributeData, blittableGenericTypeAttributeData)  switch
                {
                    (null, null, null) => false,
                    (not null, null, null) => false,
                    (null, not null, null) => false,
                    (null, null, not null) => false,
                    _ => true
                };
            }

            public void AnalyzeElement(SymbolAnalysisContext context)
            {
                AttributeData? attrData = context.Symbol.GetAttributes().FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(MarshalUsingAttribute, attr.AttributeClass));
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

            public void AnalyzeReturnType(SymbolAnalysisContext context)
            {
                var method = (IMethodSymbol)context.Symbol;
                AttributeData? attrData = method.GetReturnTypeAttributes().FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(MarshalUsingAttribute, attr.AttributeClass));
                if (attrData is not null)
                {
                    AnalyzeNativeMarshalerType(context, method.ReturnType, attrData, false, false);
                }
            }
            
            private void AnalyzeConditionallyBlittableGeneric(SymbolAnalysisContext context, INamedTypeSymbol type, AttributeData blittableGenericTypeAttributeData)
            {
                if (!type.IsGenericType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(BlittableTypeIfGenericParametersBlittableInvalidTargetRule, blittableGenericTypeAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation()));
                    return;
                }

                ITypeParameterSymbol[] conditionallyBlittableGenericParameters = new ITypeParameterSymbol[blittableGenericTypeAttributeData.ConstructorArguments[0].Values.Length];

                // Validate that the indices passed to the attribute are in the range [0, number of generic type parameters)
                for (int i = 0; i < blittableGenericTypeAttributeData.ConstructorArguments[0].Values.Length; ++i)
                {
                    int index = (int)blittableGenericTypeAttributeData.ConstructorArguments[0].Values[i].Value!;
                    if (index >= type.Arity)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(GenericTypeIndexMustBeInRangeRule, blittableGenericTypeAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation()));
                        continue;
                    }

                    conditionallyBlittableGenericParameters[i] = (ITypeParameterSymbol)type.TypeParameters[index];

                    // Validate that a generic type parameter marked as conditionally blittable actually can be blittable.
                    if (type.TypeParameters[index].IsReferenceType)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(ReferenceTypeParameterCanNeverBeBlittableRule, type.TypeParameters[index].DeclaringSyntaxReferences[0].GetSyntax().GetLocation(), type.TypeParameters[index].ToDisplayString()));
                    }
                }

                // Report error that generic type is still not blittable if the given type arguments are blittable.
                if (!type.HasOnlyBlittableFields(conditionallyBlittableGenericParameters) || type.IsAutoLayout(StructLayoutAttribute))
                {
                    context.ReportDiagnostic(Diagnostic.Create(ConditionallyBlittableTypeMustBeConditionallyBlittableRule, blittableGenericTypeAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.ToDisplayString()));
                }
            }

            private void AnalyzeNativeMarshalerType(SymbolAnalysisContext context, ITypeSymbol type, AttributeData nativeMarshalerAttributeData, bool validateGetPinnableReference, bool validateAllScenarioSupport)
            {
                if (nativeMarshalerAttributeData.ConstructorArguments[0].IsNull)
                {
                    context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustBeNonNullRule, nativeMarshalerAttributeData.ApplicationSyntaxReference!.GetSyntax().GetLocation(), type.ToDisplayString()));
                    return;
                }

                ITypeSymbol nativeType = (ITypeSymbol)nativeMarshalerAttributeData.ConstructorArguments[0].Value!;

                if (!nativeType.IsValueType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustHaveRequiredShapeRule, GetSyntaxReferenceForDiagnostic(nativeType).GetSyntax().GetLocation(), nativeType.ToDisplayString(), type.ToDisplayString()));
                    return;
                }

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
                                                    && SymbolEqualityComparer.Default.Equals(SpanOfByte, ctor.Parameters[1].Type))
                    {
                        hasStackallocConstructor = true;
                    }
                }

                bool hasToManaged = marshalerType.GetMembers("ToManaged")
                                                    .OfType<IMethodSymbol>()
                                                    .Any(m => m.Parameters.IsEmpty && !m.ReturnsByRef && !m.ReturnsByRefReadonly && SymbolEqualityComparer.Default.Equals(m.ReturnType, type) && !m.IsStatic);

                // Validate that the native type has at least one marshalling method (either managed to native or native to managed)
                if (!hasConstructor && !hasStackallocConstructor && !hasToManaged)
                {
                    context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustHaveRequiredShapeRule, GetSyntaxReferenceForDiagnostic(marshalerType).GetSyntax().GetLocation(), marshalerType.ToDisplayString(), type.ToDisplayString()));
                }

                // Validate that this type can support marshalling when stackalloc is not usable.
                if (validateAllScenarioSupport && hasStackallocConstructor && !hasConstructor)
                {
                    context.ReportDiagnostic(Diagnostic.Create(StackallocMarshallingShouldSupportAllocatingMarshallingFallbackRule, GetSyntaxReferenceForDiagnostic(marshalerType).GetSyntax().GetLocation(), marshalerType.ToDisplayString()));
                }

                IPropertySymbol? valueProperty = nativeType.GetMembers("Value").OfType<IPropertySymbol>().FirstOrDefault();
                if (valueProperty is not null)
                {
                    nativeType = valueProperty.Type;

                    // Validate that we don't have partial implementations.
                    // We error if either of the conditions below are partially met but not fully met:
                    //  - a constructor and a Value property getter
                    //  - a ToManaged method and a Value property setter
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
                                                                .FirstOrDefault(m => m is { Parameters: { Length: 0 } } and ({ ReturnsByRef: true } or { ReturnsByRefReadonly: true }));
                if (validateGetPinnableReference && getPinnableReferenceMethod is not null)
                {
                    if (!getPinnableReferenceMethod.ReturnType.IsConsideredBlittable())
                    {
                        context.ReportDiagnostic(Diagnostic.Create(GetPinnableReferenceReturnTypeBlittableRule, getPinnableReferenceMethod.DeclaringSyntaxReferences[0].GetSyntax().GetLocation()));
                    }
                    // Validate that the Value property is a pointer-sized primitive type.
                    if (valueProperty is null ||
                        valueProperty.Type is not (
                            IPointerTypeSymbol _ or
                            { SpecialType: SpecialType.System_IntPtr } or
                            { SpecialType: SpecialType.System_UIntPtr }))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(NativeTypeMustBePointerSizedRule,
                            valueProperty is not null
                            ? GetSyntaxReferenceForDiagnostic(valueProperty).GetSyntax().GetLocation()
                            : GetSyntaxReferenceForDiagnostic(nativeType).GetSyntax().GetLocation(),
                            nativeType.ToDisplayString(),
                            type.ToDisplayString()));
                    }

                    // Validate that our marshaler supports scenarios where GetPinnableReference cannot be used.
                    if (validateAllScenarioSupport && (!hasConstructor || valueProperty is { GetMethod: null }))
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
}
