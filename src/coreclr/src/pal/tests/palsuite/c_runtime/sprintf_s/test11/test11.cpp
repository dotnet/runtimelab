// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*============================================================================
**
** Source:  test11.c
**
** Purpose: Test #11 for the sprintf_s function. Test the unsigned int
**          specifier (%u).
**
**
**==========================================================================*/



#include <palsuite.h>
#include "../sprintf_s.h"

/* 
 * Depends on memcmp and strlen
 */

PALTEST(c_runtime_sprintf_s_test11_paltest_sprintf_test11, "c_runtime/sprintf_s/test11/paltest_sprintf_test11")
{
    int neg = -42;
    int pos = 42;
    INT64 l = 42;
    
    if (PAL_Initialize(argc, argv) != 0)
    {
        return FAIL;
    }


    DoNumTest("foo %u", pos, "foo 42");
    DoNumTest("foo %lu", 0xFFFF, "foo 65535");
    DoNumTest("foo %hu", 0xFFFF, "foo 65535");
    DoNumTest("foo %Lu", pos, "foo 42");
    DoI64Test("foo %I64u", l, "42", "foo 42");
    DoNumTest("foo %3u", pos, "foo  42");
    DoNumTest("foo %-3u", pos, "foo 42 ");
    DoNumTest("foo %.1u", pos, "foo 42");
    DoNumTest("foo %.3u", pos, "foo 042");
    DoNumTest("foo %03u", pos, "foo 042");
    DoNumTest("foo %#u", pos, "foo 42");
    DoNumTest("foo %+u", pos, "foo 42");
    DoNumTest("foo % u", pos, "foo 42");
    DoNumTest("foo %+u", neg, "foo 4294967254");
    DoNumTest("foo % u", neg, "foo 4294967254");

    PAL_Terminate();
    return PASS;
}

