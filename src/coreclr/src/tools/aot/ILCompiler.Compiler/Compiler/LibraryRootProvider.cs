// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem.Ecma;
using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// Provides compilation group for a library that compiles everything in the input IL module.
    /// </summary>
    public class LibraryRootProvider : ICompilationRootProvider
    {
        private EcmaModule _module;

        public LibraryRootProvider(EcmaModule module)
        {
            _module = module;
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            TypeSystemContext context = _module.Context;

            foreach (MetadataType type in _module.GetAllTypes())
            {
                if (!RootType(type, rootProvider))
                    continue;

                if (type.IsGenericDefinition)
                {
                    // Generic type - try to create a canonical instantiation and root that.
                    // The multifile compilation group relies on the fact that a canonical
                    // instantiation is going to be homed in the definition module.
                    bool canonMakesSense = true;
                    foreach (var p in type.Instantiation)
                    {
                        if (((GenericParameterDesc)p).HasNotNullableValueTypeConstraint)
                            canonMakesSense = false;
                    }

                    if (canonMakesSense)
                    {
                        TypeDesc[] canonInstantiation = new TypeDesc[type.Instantiation.Length];
                        for (int i = 0; i < canonInstantiation.Length; i++)
                            canonInstantiation[i] = context.CanonType;
                        TypeDesc typeWithMethods = type.MakeInstantiatedType(canonInstantiation);

                        // Do not root EEType for System.Array`1 because it's magic and the EEType
                        // should never be generated.
                        if (type.Module != context.SystemModule || type.Name != "Array`1" || type.Namespace != "System")
                        {
                            if (!RootType(typeWithMethods, rootProvider))
                                continue;
                        }

                        RootMethods(typeWithMethods, "Library module method", rootProvider);
                    }
                }
                else
                {
                    RootMethods(type, "Library module method", rootProvider);
                    rootProvider.RootThreadStaticBaseForType(type, "Library module type statics");
                    rootProvider.RootGCStaticBaseForType(type, "Library module type statics");
                    rootProvider.RootNonGCStaticBaseForType(type, "Library module type statics");
                }
            }
        }

        private bool RootType(TypeDesc type, IRootingServiceProvider rootProvider)
        {
            try
            {
                rootProvider.AddCompilationRoot(type, "Library module type");
            }
            catch (TypeSystemException)
            {
                // TODO: fail compilation if a switch was passed

                // Swallow type load exceptions while rooting
                return false;

                // TODO: Log as a warning
            }

            return true;
        }

        private void RootMethods(TypeDesc type, string reason, IRootingServiceProvider rootProvider)
        {
            foreach (MethodDesc method in type.GetAllMethods())
            {
                // Skip methods with no IL and uninstantiated generic methods
                if (method.IsAbstract || method.HasInstantiation)
                    continue;

                if (method.IsInternalCall)
                    continue;

                try
                {
                    CheckCanGenerateMethod(method);
                    rootProvider.AddCompilationRoot(method, reason);
                }
                catch (TypeSystemException)
                {
                    // TODO: fail compilation if a switch was passed

                    // Individual methods can fail to load types referenced in their signatures.
                    // Skip them in library mode since they're not going to be callable.
                    continue;

                    // TODO: Log as a warning
                }
            }
        }

        /// <summary>
        /// Validates that it will be possible to generate '<paramref name="method"/>' based on the types 
        /// in its signature. Unresolvable types in a method's signature prevent RyuJIT from generating
        /// even a stubbed out throwing implementation.
        /// </summary>
        public static void CheckCanGenerateMethod(MethodDesc method)
        {
            MethodSignature signature = method.Signature;

            // Vararg methods are not supported in .NET Core
            if ((signature.Flags & MethodSignatureFlags.UnmanagedCallingConventionMask) == MethodSignatureFlags.CallingConventionVarargs)
                ThrowHelper.ThrowBadImageFormatException();

            CheckTypeCanBeUsedInSignature(signature.ReturnType);

            for (int i = 0; i < signature.Length; i++)
            {
                CheckTypeCanBeUsedInSignature(signature[i]);
            }
        }

        private static void CheckTypeCanBeUsedInSignature(TypeDesc type)
        {
            MetadataType defType = type as MetadataType;

            if (defType != null)
            {
                defType.ComputeTypeContainsGCPointers();
            }
        }
    }
}
