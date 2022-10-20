// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef REGLIST_H
#define REGLIST_H

#include "target.h"
#include "tinyarray.h"

// The "regList" type is a small set of registerse
<<<<<<< HEAD
#if defined(TARGET_X86) || defined(TARGET_WASM)
typedef TinyArray<unsigned short, regNumber, REGNUM_BITS> regList;
=======
#ifdef TARGET_X86
typedef TinyArray<unsigned int, regNumber, REGNUM_BITS> regList;
>>>>>>> 80f6b8afa7abf639a441f01561e8c9af1de0d497
#else
// The regList is unused for all other targets.
#endif // TARGET*

#endif // REGLIST_H
