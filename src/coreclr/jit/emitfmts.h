// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//////////////////////////////////////////////////////////////////////////////

#if defined(TARGET_XARCH)
#include "emitfmtsxarch.h"
#elif defined(TARGET_ARM)
#include "emitfmtsarm.h"
#elif defined(TARGET_ARM64)
#include "emitfmtsarm64.h"
<<<<<<< HEAD
#elif defined(TARGET_WASM) // this file included in CMakeList.txt unconditionally
=======
#elif defined(TARGET_LOONGARCH64)
#include "emitfmtsloongarch64.h"
>>>>>>> 6543a048d7242ddf204f2e1ba0723d27c02bdfc7
#else
#error Unsupported or unset target architecture
#endif // target type
