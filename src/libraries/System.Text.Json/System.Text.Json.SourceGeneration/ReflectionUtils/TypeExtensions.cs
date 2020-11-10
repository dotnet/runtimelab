// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace System.Reflection
{
    public static class TypeExtensions 
    {
        public static string GetUniqueCompilableTypeName(this Type type) => GetCompilableTypeName(type, type.FullName);

        public static string GetCompilableTypeName(this Type type) => GetCompilableTypeName(type, type.Name);

        private static string GetCompilableTypeName(Type type, string name)
        {
            if (!type.IsGenericType)
            {
                return name;
            }

            // TODO: Guard upstream against open generics.
            Debug.Assert(!type.ContainsGenericParameters);

            int backTickIndex = name.IndexOf('`');
            string baseName = name.Substring(0, backTickIndex);

            return $"{baseName}<{string.Join(",", type.GetGenericArguments().Select(arg => GetUniqueCompilableTypeName(arg)))}>";
        }

        public static string GetUniqueFriendlyTypeName(this Type type)
        {
            return GetFriendlyTypeName(type.GetUniqueCompilableTypeName());
        }

        public static string GetFriendlyTypeName(this Type type)
        {
            return GetFriendlyTypeName(type.GetCompilableTypeName());
        }

        private static string GetFriendlyTypeName(string compilableName)
        {
            return compilableName.Replace(".", "").Replace("<", "").Replace(">", "").Replace(",", "").Replace("[]", "Array");
        }
    }
}
