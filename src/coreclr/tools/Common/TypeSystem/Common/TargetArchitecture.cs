// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Specifies the target CPU architecture.
    /// </summary>
    public enum TargetArchitecture
    {
        Unknown,
        ARM,
        ARM64,
        X64,
        X86,
        Wasm32,
<<<<<<< HEAD
        Wasm64
=======
        LoongArch64,
>>>>>>> 442c137891821a567e9a05411f821dbf2aec5aa5
    }
}
