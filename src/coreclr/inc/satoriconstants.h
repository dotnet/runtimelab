// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __SATORI_CONSTANTS_H__
#define __SATORI_CONSTANTS_H__

// Shared by the GC, runtime helpers, and assembly write barriers.
// User VA must fit in SATORI_ADDRESS_SPACE_BITS; the top byte may hold a tag.
#define SATORI_PAGE_BITS            30
#define SATORI_ADDRESS_SPACE_BITS   48
#define SATORI_PAGE_MAP_BITS        (SATORI_ADDRESS_SPACE_BITS - SATORI_PAGE_BITS)

#if SATORI_ADDRESS_SPACE_BITS > 56 || SATORI_PAGE_BITS >= SATORI_ADDRESS_SPACE_BITS
#error Invalid Satori page map dimensions
#endif

#endif // __SATORI_CONSTANTS_H__
