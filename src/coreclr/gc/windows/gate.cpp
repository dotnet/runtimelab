// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Copyright (c) 2025 Vladimir Sadov
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

#include "common.h"
#include "gcenv.structs.h"
#include "gcenv.base.h"
#include "gcenv.os.h"

GCGate::GCGate()
    : m_impl(nullptr), m_state(Blocking)
{
}

void GCGate::Wait()
{
    TimedWait(INFINITE);
}

bool GCGate::TimedWait(uint32_t timeout)
{
    uint32_t blocking = Blocking;
    BOOL result = WaitOnAddress(&m_state, &blocking, sizeof(blocking), timeout);
    if (result == FALSE)
    {
        if (GetLastError() == ERROR_TIMEOUT)
        {
            return false;
        }

        assert(!"Failed to wait on GC gate");
        abort();
    }

    m_state = Blocking;
    return true;
}

void GCGate::WakeOne()
{
    m_state = Open;
    WakeByAddressSingle(const_cast<uint32_t*>(&m_state));
}

void GCGate::WakeAll()
{
    m_state = Open;
    WakeByAddressAll(const_cast<uint32_t*>(&m_state));
}
