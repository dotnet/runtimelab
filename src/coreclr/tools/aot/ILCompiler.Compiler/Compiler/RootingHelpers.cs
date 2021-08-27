// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;

using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;
using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public class RootingHelpers
    {
        public static bool TryRootType(IRootingServiceProvider rootProvider, TypeDesc type, string reason)
        {
            try
            {
                RootType(rootProvider, type, reason);
                return true;
            }
            catch (TypeSystemException)
            {
                return false;
            }
        }

        public static void RootType(IRootingServiceProvider rootProvider, TypeDesc type, string reason)
        {
            rootProvider.AddCompilationRoot(type, reason);

            // Instantiate generic types over something that will be useful at runtime
            if (type.IsGenericDefinition)
            {
                Instantiation inst = TypeExtensions.GetInstantiationThatMeetsConstraints(type.Instantiation, allowCanon: true);
                if (inst.IsNull)
                    return;

                type = ((MetadataType)type).MakeInstantiatedType(inst);

                rootProvider.AddCompilationRoot(type, reason);
            }

            // Also root base types. This is so that we make methods on the base types callable.
            // This helps in cases like "class Foo : Bar<int> { }" where we discover new
            // generic instantiations.
            TypeDesc baseType = type.BaseType;
            if (baseType != null)
            {
                RootType(rootProvider, baseType.NormalizeInstantiation(), reason);
            }

            if (type.IsDefType)
            {
                foreach (var method in type.GetMethods())
                {
                    if (method.HasInstantiation)
                    {
                        // Generic methods on generic types could end up as Foo<object>.Bar<__Canon>(),
                        // so for simplicity, we just don't handle them right now to make this more
                        // predictable.
                        if (!method.OwningType.HasInstantiation)
                        {
                            Instantiation inst = TypeExtensions.GetInstantiationThatMeetsConstraints(method.Instantiation, allowCanon: false);
                            if (!inst.IsNull)
                            {
                                TryRootMethod(rootProvider, method.MakeInstantiatedMethod(inst), reason);
                            }
                        }
                    }
                    else
                    {
                        TryRootMethod(rootProvider, method, reason);
                    }
                }
            }
        }

        public static bool TryRootMethod(IRootingServiceProvider rootProvider, MethodDesc method, string reason)
        {
            try
            {
                RootMethod(rootProvider, method, reason);
                return true;
            }
            catch (TypeSystemException)
            {
                return false;
            }
        }

        public static void RootMethod(IRootingServiceProvider rootProvider, MethodDesc method, string reason)
        {
            // Make sure we're not putting something into the graph that will crash later.
            LibraryRootProvider.CheckCanGenerateMethod(method);

            rootProvider.AddReflectionRoot(method, reason);
        }

        public static bool TryGetDependenciesForReflectedMethod(ref DependencyList dependencies, NodeFactory factory, MethodDesc method, string reason)
        {
            // If there's any genericness involved, try to create a fitting instantiation that would be usable at runtime.
            // This is not a complete solution to the problem.
            // If we ever decide that MakeGenericType/MakeGenericMethod should simply be considered unsafe, this code can be deleted
            // and instantiations that are not fully closed can be ignored.
            if (method.OwningType.IsGenericDefinition || method.OwningType.ContainsSignatureVariables(treatGenericParameterLikeSignatureVariable: true))
            {
                TypeDesc owningType = method.OwningType.GetTypeDefinition();
                Instantiation inst = TypeExtensions.GetInstantiationThatMeetsConstraints(owningType.Instantiation, allowCanon: false);
                if (inst.IsNull)
                {
                    return false;
                }

                method = method.Context.GetMethodForInstantiatedType(
                    method.GetTypicalMethodDefinition(),
                    ((MetadataType)owningType).MakeInstantiatedType(inst));
            }

            if (method.IsGenericMethodDefinition || method.Instantiation.ContainsSignatureVariables())
            {
                method = method.GetMethodDefinition();

                Instantiation inst = TypeExtensions.GetInstantiationThatMeetsConstraints(method.Instantiation, allowCanon: false);
                if (inst.IsNull)
                {
                    return false;
                }

                method = method.MakeInstantiatedMethod(inst);
            }

            dependencies ??= new DependencyList();

            try
            {
                // Make sure we're not putting something into the graph that will crash later.
                LibraryRootProvider.CheckCanGenerateMethod(method);
            }
            catch (TypeSystemException)
            {
                return false;
            }

            dependencies.Add(factory.ReflectableMethod(method), reason);

            return true;
        }

        public static bool TryGetDependenciesForReflectedField(ref DependencyList dependencies, NodeFactory factory, FieldDesc field, string reason)
        {
            // If there's any genericness involved, try to create a fitting instantiation that would be usable at runtime.
            // This is not a complete solution to the problem.
            // If we ever decide that MakeGenericType/MakeGenericMethod should simply be considered unsafe, this code can be deleted
            // and instantiations that are not fully closed can be ignored.
            if (field.OwningType.IsGenericDefinition || field.OwningType.ContainsSignatureVariables(treatGenericParameterLikeSignatureVariable: true))
            {
                TypeDesc owningType = field.OwningType.GetTypeDefinition();
                Instantiation inst = TypeExtensions.GetInstantiationThatMeetsConstraints(owningType.Instantiation, allowCanon: true);
                if (inst.IsNull)
                {
                    return false;
                }

                field = field.Context.GetFieldForInstantiatedType(
                    field.GetTypicalFieldDefinition(),
                    ((MetadataType)owningType).MakeInstantiatedType(inst));
            }

            if (!TryGetDependenciesForReflectedType(ref dependencies, factory, field.OwningType, reason))
            {
                return false;
            }

            // Currently generating the base of the type is enough to make the field reflectable.

            if (field.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any))
            {
                return true;
            }

            if (field.IsStatic && !field.IsLiteral && !field.HasRva)
            {
                bool cctorContextAdded = false;
                if (field.IsThreadStatic)
                {
                    dependencies.Add(factory.TypeThreadStaticIndex((MetadataType)field.OwningType), reason);
                }
                else if (field.HasGCStaticBase)
                {
                    dependencies.Add(factory.TypeGCStaticsSymbol((MetadataType)field.OwningType), reason);
                }
                else
                {
                    dependencies.Add(factory.TypeNonGCStaticsSymbol((MetadataType)field.OwningType), reason);
                    cctorContextAdded = true;
                }

                if (!cctorContextAdded && factory.PreinitializationManager.HasLazyStaticConstructor(field.OwningType))
                {
                    dependencies.Add(factory.TypeNonGCStaticsSymbol((MetadataType)field.OwningType), reason);
                }
            }

            return true;
        }

        public static bool TryGetDependenciesForReflectedProperty(ref DependencyList dependencies, NodeFactory factory, PropertyPseudoDesc property, string reason)
        {
            bool succeeded = true;
            if (property.GetMethod != null)
                succeeded &= TryGetDependenciesForReflectedMethod(ref dependencies, factory, property.GetMethod, reason);
            if (property.SetMethod != null)
                succeeded &= TryGetDependenciesForReflectedMethod(ref dependencies, factory, property.SetMethod, reason);
            return succeeded;
        }

        public static bool TryGetDependenciesForReflectedEvent(ref DependencyList dependencies, NodeFactory factory, EventPseudoDesc @event, string reason)
        {
            bool succeeded = true;
            if (@event.AddMethod != null)
                succeeded &= TryGetDependenciesForReflectedMethod(ref dependencies, factory, @event.AddMethod, reason);
            if (@event.RemoveMethod != null)
                succeeded &= TryGetDependenciesForReflectedMethod(ref dependencies, factory, @event.RemoveMethod, reason);
            return succeeded;
        }

        public static bool TryGetDependenciesForReflectedType(ref DependencyList dependencies, NodeFactory factory, TypeDesc type, string reason)
        {
            try
            {
                // Instantiations with signature variables are not helpful - just use the definition.
                if (type.ContainsSignatureVariables(treatGenericParameterLikeSignatureVariable: true))
                {
                    type = type.GetTypeDefinition();
                }

                dependencies.Add(factory.MaximallyConstructableType(type), reason);

                // If there's any unknown genericness involved, try to create a fitting instantiation that would be usable at runtime.
                // This is not a complete solution to the problem.
                // If we ever decide that MakeGenericType/MakeGenericMethod should simply be considered unsafe, this code can be deleted
                // and instantiations that are not fully closed can be ignored.
                if (type.IsGenericDefinition)
                {
                    Instantiation inst = TypeExtensions.GetInstantiationThatMeetsConstraints(type.Instantiation, allowCanon: true);
                    if (!inst.IsNull)
                    {
                        dependencies.Add(factory.MaximallyConstructableType(((MetadataType)type).MakeInstantiatedType(inst)), reason);
                    }
                }
            }
            catch (TypeSystemException)
            {
                return false;
            }

            return true;
        }

        public static void GetDependenciesForEntireReflectedType(ref DependencyList dependencies, NodeFactory factory, TypeDesc type, string reason, Stack<TypeDesc> typesVisited = null)
        {
            // We can end up with a cycle for things like
            // class Base
            // {
            //     class Nested : Base { }
            // }
            if (typesVisited != null && typesVisited.Contains(type))
                return;

            dependencies ??= new DependencyList();

            // If the type has no rootable members, ensure at least the type itself is reflectable
            if (!TryGetDependenciesForReflectedType(ref dependencies, factory, type, reason))
            {
                return;
            }

            typesVisited ??= new Stack<TypeDesc>();
            typesVisited.Push(type);

            if (type.IsDefType)
            {
                foreach (var nestedType in ((MetadataType)type).GetNestedTypes())
                {
                    GetDependenciesForEntireReflectedType(ref dependencies, factory, nestedType, reason, typesVisited);
                }

                foreach (var intf in ((MetadataType)type).ExplicitlyImplementedInterfaces)
                {
                    GetDependenciesForEntireReflectedType(ref dependencies, factory, intf, reason, typesVisited);
                }
            }

            if (type.HasBaseType)
            {
                GetDependenciesForEntireReflectedType(ref dependencies, factory, type.BaseType, reason, typesVisited);
            }

            // We assume that reflection enabling the accessors enabled the properties/events
            // If that ever changes, we need extra code here.

            foreach (var method in type.GetMethods())
            {
                TryGetDependenciesForReflectedMethod(ref dependencies, factory, method, reason);
            }

            foreach (var field in type.GetFields())
            {
                TryGetDependenciesForReflectedField(ref dependencies, factory, field, reason);
            }

            var popped = typesVisited.Pop();
            Debug.Assert(popped == type);
        }
    }
}
