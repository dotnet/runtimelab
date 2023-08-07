// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.ManagedZLib;

public class TreeDesc
{
    public CtData[] _dynamicTree;
    public int maxCode;
    public StaticTreesDesc _StaticTreeDesc;

    public TreeDesc(CtData[] dynamicTree, StaticTreesDesc StaticTreeDesc)
    {
        _dynamicTree = dynamicTree;
        _StaticTreeDesc = StaticTreeDesc;
    }
}

