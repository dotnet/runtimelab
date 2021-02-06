// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//////////////////////////////////////////////////////////////////////////////

#if defined(TARGET_XARCH)
#include "emitfmtsxarch.h"
#elif defined(TARGET_ARM)
#include "emitfmtsarm.h"
#elif defined(TARGET_ARM64)
#include "emitfmtsarm64.h"
#elif defined(TARGET_WASM32) || defined(TARGET_WASM64) // TODO: need anything here?  Try removing
#include "emitfmtswasm.h"
#else
#error Unsupported or unset target architecture
#endif // target type
