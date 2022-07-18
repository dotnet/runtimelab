// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection.Metadata;

namespace System.Reflection.Emit.Experimental
{
 /* The purpose of  this class is to provide wrappers for entities that are referenced in metadata.
 *  The wrappers allows for convenient access to the parent token of an entity.
 *  They override default equality for Assemblies, Types, Methods etc. to make sure identical writes to metadata aren't made even if the objects are different.
 * */
    internal class EntityWrappers
    {
        internal class AssemblyReferenceWrapper
        {
            internal readonly Assembly Assembly;

            public AssemblyReferenceWrapper(Assembly assembly)
            {
                Assembly = assembly;
            }

            public override bool Equals(object? obj)
            {
                return obj is AssemblyReferenceWrapper wrapper &&
                       EqualityComparer<string>.Default.Equals(Assembly.GetName().FullName, wrapper.Assembly.GetName().FullName);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Assembly.GetName().FullName);
            }
        }

        internal class TypeReferenceWrapper
        {
            internal readonly Type Type;
            internal EntityHandle ParentToken;

            public TypeReferenceWrapper(Type type)
            {
                Type = type;
            }

            public override bool Equals(object? obj)
            {
                return obj is TypeReferenceWrapper wrapper
                    && EqualityComparer<string>.Default.Equals(Type.Name, wrapper.Type.Name)
                    && EqualityComparer<string>.Default.Equals(Type.Namespace, wrapper.Type.Namespace)
                    && EqualityComparer<EntityHandle>.Default.Equals(ParentToken, wrapper.ParentToken);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Type.Name, Type.Namespace, ParentToken);
            }
        }

        internal class MethodReferenceWrapper
        {
            internal readonly MethodBase Method;
            internal EntityHandle ParentToken;

            public MethodReferenceWrapper(MethodBase method)
            {
                Method = method;
            }

            public override bool Equals(object? obj)
            {
                return obj is MethodReferenceWrapper wrapper
                    && EqualityComparer<string>.Default.Equals(Method.Name, wrapper.Method.Name)
                    && EqualityComparer<string>.Default.Equals(Method.ToString(), wrapper.Method.ToString())
                    && EqualityComparer<EntityHandle>.Default.Equals(ParentToken, wrapper.ParentToken);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Method.Name, Method.ToString(), ParentToken);
            }
        }

        internal class CustomAttributeWrapper
        {
            internal ConstructorInfo ConstructorInfo;
            internal byte[] BinaryAttribute;
            internal EntityHandle ConToken;

            public CustomAttributeWrapper(ConstructorInfo constructorInfo, byte[] binaryAttribute)
            {
                ConstructorInfo = constructorInfo;
                BinaryAttribute = binaryAttribute;
            }
        }
    }
}
