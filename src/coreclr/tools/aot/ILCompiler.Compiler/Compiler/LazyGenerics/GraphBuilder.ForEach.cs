namespace Microsoft.Build.ILTasks.Transforms
{
    using System;
    using System.IO;
    using System.Text;
    using System.Linq;
    using System.Collections;
    using System.Diagnostics;
    using System.Collections.Generic;

    using Microsoft.Cci;
    using Microsoft.Cci.Extensions;

    internal static partial class LazyGenericsSupport
    {
        private sealed partial class GraphBuilder
        {
            /// <summary>
            /// Walk through the type expression and find any embedded generic parameter references. For each one found,
            /// invoke the collector delegate with that generic parameter and a boolean indicate whether this is
            /// a proper embedding (i.e. there is something actually nesting this.)
            /// 
            /// Typically, the type expression is something that a generic type formal is being bound to, and we're
            /// looking to see if another other generic type formals are referenced within that type expression.
            /// 
            /// This method also records bindings for any generic instances it finds inside the tree expression.
            /// Sometimes, this side-effect is all that's wanted - in such cases, invoke this method with a null collector.
            /// </summary>
            private void ForEachEmbeddedGenericFormal(ITypeReference typeExpression, System.Action<GenericFormal, bool> collector = null)
            {
                System.Action<GenericFormal, int> wrappedCollector =
                    delegate(GenericFormal embedded, int depth)
                    {
                        bool isProperEmbedding = (depth > 0);
                        if (collector != null)
                            collector(embedded, isProperEmbedding);
                        return;
                    };
                ForEachEmbeddedGenericFormalWorker(typeExpression, wrappedCollector, depth: 0);
            }

            private void ForEachEmbeddedGenericFormalWorker(ITypeReference type, System.Action<GenericFormal, int> collector, int depth)
            {
                if (type is IArrayTypeReference)
                {
                    ForEachEmbeddedGenericFormalWorker(((IArrayTypeReference)type).ElementType, collector, depth + 1);
                    return;
                }

                if (type is IManagedPointerTypeReference)
                {
                    ForEachEmbeddedGenericFormalWorker(((IManagedPointerTypeReference)type).TargetType, collector, depth + 1);
                    return;
                }

                if (type is IPointerTypeReference)
                {
                    ForEachEmbeddedGenericFormalWorker(((IPointerTypeReference)type).TargetType, collector, depth + 1);
                    return;
                }

                if (type.IsConstructedGenericType())
                {
                    INamedTypeDefinition genericTypeDefinition = type.GetGenericTypeDefinition().ConfirmedResolvedType<INamedTypeDefinition>();
                    IList<GenericFormal> genericTypeParameters = genericTypeDefinition.GenericTypeParameters().Select(igtp => igtp.AsGenericTypeFormal(genericTypeDefinition)).ToArray();
                    IList<ITypeReference> genericTypeArguments = type.GenericTypeArguments().ToArray();
                    for (int i = 0; i < genericTypeArguments.Count; i++)
                    {
                        GenericFormal genericTypeParameter = genericTypeParameters[i];
                        ITypeReference genericTypeArgument = genericTypeArguments[i];

                        int newDepth = depth + 1;
                        ForEachEmbeddedGenericFormalWorker(
                            genericTypeArgument,
                            delegate(GenericFormal embedded, int depth2)
                            {
                                collector(embedded, depth2);
                                bool isProperEmbedding = (depth2 > newDepth);
                                RecordBinding(genericTypeParameter, embedded, isProperEmbedding);
                            },
                            newDepth
                        );
                    }
                    return;
                }

                if (type is IGenericParameterReference)
                {
                    GenericFormal embedded = ((IGenericParameterReference)type).AsGenericFormal(_declaringType);
                    collector(embedded, depth);
                    return;
                }

                if (type is INamespaceTypeReference || type is INestedTypeReference)
                {
                    // Non-constructed type. End of recursion.
                    return;
                }

                if (type is IFunctionPointerTypeReference)
                {
                    // Function pointer type.
                    return;
                }

                // Custom modifiers wrap normal types
                if (type is IModifiedTypeReference)
                {
                    IModifiedTypeReference modifiedType = (IModifiedTypeReference)type;
                    foreach (ICustomModifier customMod in modifiedType.CustomModifiers)
                    {
                        ForEachEmbeddedGenericFormalWorker(customMod.Modifier, collector, depth + 1);
                    }

                    // Look for generic formals on the unmodified type
                    ForEachEmbeddedGenericFormalWorker(modifiedType.UnmodifiedType, collector, depth + 1);
                    return;
                }

                // Did CCI invent a new kind of Type that we're unaware of?
                Debug.Fail("Unexpected failure to bucket CCI type: " + type);
                throw new InvalidOperationException();
            }
        }
    }
}
