// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// USE_GC_INFO_DECODER does not defined for Win-x86 in CoreCLR
// I force that definition here, so code sharing can still happens.
#define USE_GC_INFO_DECODER
#include "../../inc/gcinfodecoder.h"
