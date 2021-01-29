// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace System.Net.Http.Json
{
    internal static class Helper
    {
        // Members accessed by the serializer when deserializing.
        internal const DynamicallyAccessedMemberTypes MembersAccessedOnRead =
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields;

        // Members accessed by the serializer when serializing.
        internal const DynamicallyAccessedMemberTypes MembersAccessedOnWrite =
            DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields;
    }
}
