// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Internal.DeveloperExperience
{
    internal sealed class DeveloperExperienceConsole : DeveloperExperience
    {
        internal static void SetAsDefault()
        {
            DeveloperExperience.Default = new DeveloperExperienceConsole();
        }

        public sealed override void WriteLine(string s)
        {
            Console.Error.WriteLine(s);
        }
    }
}
