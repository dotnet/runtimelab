// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;

namespace Internal.Runtime.Augments
{
    public partial class RuntimeAugments
    {
        [FeatureSwitchDefinition("Internal.Runtime.PreciseVirtualUnwind")]
        public static bool PreciseVirtualUnwind => false;

        [CLSCompliant(false)]
        public static unsafe int ParsePreciseVirtualUnwindInfo(byte* pUnwindInfo, void** pFunctionPointer = null)
        {
            return PreciseVirtualUnwindInfo.Parse(pUnwindInfo, pFunctionPointer: pFunctionPointer);
        }
    }
}
