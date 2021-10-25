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
            public GraphBuilder(IAssembly assembly, ILTransformLogger logger)
            {
                _graph = new Graph<GenericFormal>();
                foreach (INamedTypeDefinition declaringType in assembly.GetAllTypes())
                {
                    _logger = logger;
                    _declaringType = declaringType;
                    WalkAncestorTypes();
                    WalkFields();
                    WalkMethods();
                }
                return;
            }

            public Graph<GenericFormal> Graph { get { return _graph; } }

            // Base types and interfaces.
            private void WalkAncestorTypes()
            {
                if (!_declaringType.IsGenericTypeDefinition())
                    return;

                ITypeReference baseType = _declaringType.BaseClasses.Any() ? _declaringType.BaseClasses.First() : null;
                if (baseType != null)
                {
                    ProcessAncestorType(baseType);
                }
                foreach (ITypeReference ifcType in _declaringType.Interfaces)
                {
                    ProcessAncestorType(ifcType);
                }
                return;

            }

            private void ProcessAncestorType(ITypeReference ancestorType)
            {
                ForEachEmbeddedGenericFormal(ancestorType);
            }

            private void WalkFields()
            {
                if (!_declaringType.IsGenericTypeDefinition())
                    return;

                foreach (IFieldDefinition field in _declaringType.Fields)
                {
                    ITypeReference fieldType = field.Type;
                    ProcessTypeReference(fieldType);
                }
            }

            private void WalkMethods()
            {
                // Do not bail out early just because _declaringType is not generic. There are still generic methods to consider.

                foreach (IMethodDefinition method in _declaringType.Methods)
                {
                    ProcessTypeReference(method.Type);
                    foreach (IParameterDefinition parameter in method.Parameters)
                    {
                        ProcessTypeReference(parameter.Type);
                    }

                    if (method.IsAbstract || method.Body == null || method.Body.Operations == null)
                        continue;

                    foreach (IOperation op in method.Body.Operations)
                    {
                        IMethodReference target = op.Value as IMethodReference;
                        if (target != null)
                        {
                            ProcessTypeReference(target.ContainingType);
                            ProcessMethodCall(target);
                        }

                        IFieldReference field = op.Value as IFieldReference;
                        if (field != null)
                        {
                            ProcessTypeReference(field.ContainingType);
                        }

                        ITypeReference typeReference = op.Value as ITypeReference;
                        if (typeReference != null)
                        {
                            ProcessTypeReference(typeReference);
                        }
                    }
                }
            }

            /// <summary>
            /// Inside a method body, we found a reference to another type (e.g. ldtoken, or a member access.)
            /// If the type is a generic instance, record any bindings between its formals and the referencer's
            /// formals.
            /// </summary>
            private void ProcessTypeReference(ITypeReference typeReference)
            {
                ForEachEmbeddedGenericFormal(typeReference);
            }

            /// <summary>
            /// Records the fact that the type formal "receiver" is being bound to a type expression that references
            /// "embedded."
            /// </summary>
            private void RecordBinding(GenericFormal receiver, GenericFormal embedded, bool isProperEmbedding)
            {
                bool flagged;
                if (isProperEmbedding)
                {
                    // If we got here, we have a potential codepath that binds "receiver" to a type expression involving "embedded"
                    // (and is not simply "embedded" itself.)
                    flagged = true;
                }
                else
                {
                    // If we got here, we have a potential codepath that binds "receiver" to a type expression that is simply "embedded"
                    flagged = false;
                }

                _graph.AddEdge(embedded, receiver, flagged);

                return;
            }

            private Graph<GenericFormal> _graph;

            //
            // Stores the type being analyzed. Kinda sucks for it be an instance field but we have to pass it around in so many
            // places (including the helper code to transform an IGenericTypeParameter to a GenericFormal), it's just less confusing this way.
            //
            private INamedTypeDefinition _declaringType;

            private ILTransformLogger _logger;
        }
    }
}