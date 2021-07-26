// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <unixasmmacros.inc>
#include "AsmOffsets.inc"

    .global RhpGcPoll2

    LEAF_ENTRY RhpGcPoll
        ; @todo: I'm assuming it's not OK to trash any register here. If that's not true we can optimize the
        ; push/pops out of this fast path.
        push        {r0}
        ldr         r0, =RhpTrapThreads
        ldr         r0, [r0]
        tst         r0, #TrapThreadsFlags_TrapThreads
        bne         %0
        pop         {r0}
        bx          lr
0
        pop         {r0}
        b           RhpGcPollRare
    LEAF_END RhpGcPoll

    NESTED_ENTRY RhpGcPollRare, _TEXT, NoHandler
        PUSH_COOP_PINVOKE_FRAME r0
        bl RhpGcPoll2
        POP_COOP_PINVOKE_FRAME
        bx           lr
    NESTED_END RhpGcPollRare
