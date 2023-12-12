// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO
{
    /// <summary>
    /// Instructs the <c>System.IO.StreamSourceGeneration</c> source generator to generate boilerplate implementations 
    /// for the <see cref="Stream"/> members of the associated type based on the currently implemented members that
    /// hint the capabilities of the associated type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class GenerateStreamBoilerplateAttribute : Attribute { }
}