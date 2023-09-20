// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;
using Internal.Mint.Abstraction;

namespace Internal.Mint.EE;

public class MintRuntime
{
    internal sealed unsafe class ExecutionContext : IDisposable
    {
        private static EEVtableProvider s_eeVTableProvder = new();

        [ThreadStatic]
        private static ExecutionContext? _current;

        public static ExecutionContext Current
        {
            get
            {
                _current ??= new ExecutionContext();
                return _current;
            }
        }
        private readonly MemoryManager _memoryManager;
        private readonly Abstraction.ThreadContextInstanceAbstractionNativeAot* _nativeThreadContext;

        ~ExecutionContext()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _memoryManager.Dispose();
            }
        }
        private unsafe ExecutionContext()
        {
            _memoryManager = new MemoryManager();
            _nativeThreadContext = InitializeNativeThreadContext(_memoryManager);
        }

        private static unsafe Abstraction.ThreadContextInstanceAbstractionNativeAot* InitializeNativeThreadContext(MemoryManager memoryManager)
        {
            Abstraction.ThreadContextInstanceAbstractionNativeAot* context = memoryManager.Allocate<Abstraction.ThreadContextInstanceAbstractionNativeAot>();
            byte* stack = memoryManager.AllocateStack(out var stackSize, out var redZoneSize, out var initAlignment);
            context->vtable = s_eeVTableProvder.ThreadContextInstanceAbstractionVTable;
            context->stack_start = stack;
            context->stack_end = context->stack_start + stackSize - redZoneSize;
            context->stack_real_end = context->stack_start + stackSize;
            /* We reserve a stack slot at the top of the interp stack to make temp objects visible to GC */
            context->stack_pointer = context->stack_start + initAlignment; ;
            // context->gcHandle = memoryManager.Own(this);

            FrameDataAllocatorInit(memoryManager, &context->data_stack);

            return context;
        }

        private sealed unsafe class EEVtableProvider : VTableProvider
        {
            public EEVtableProvider() : base() { }
            ~EEVtableProvider()
            {
                Dispose(false);
                GC.SuppressFinalize(this);
            }

            private ThreadContextInstanceAbstractionVTable* _threadContextInstanceAbstractionVTable;
            internal ThreadContextInstanceAbstractionVTable* ThreadContextInstanceAbstractionVTable
            {
                get => GetOrAddVTable(ref _threadContextInstanceAbstractionVTable, static (vtable) =>
                {
                    vtable->set_stack_pointer = &SetStackPointer;
                    vtable->check_sufficient_stack = &CheckSufficientStack;
                });
            }
        }

        [UnmanagedCallersOnly]
        private static unsafe void SetStackPointer(Abstraction.ThreadContextInstanceAbstractionNativeAot* context, byte* stackPointer)
        {
            byte* oldSP = context->stack_pointer;
            //TODO:
            //  zero out reference slots between [stackPointer ... oldSP]
            context->stack_pointer = stackPointer;
        }

        [UnmanagedCallersOnly]
        private static unsafe int CheckSufficientStack(Abstraction.ThreadContextInstanceAbstractionNativeAot* context, UIntPtr size)
        {
            byte* stackPointer = context->stack_pointer;
            byte* stackEnd = context->stack_real_end;
            if (stackPointer + (int)size < stackEnd)
            {
                return 1;
            }
            return 0;
        }
        private static unsafe void FrameDataAllocatorInit(MemoryManager memoryManager, Abstraction.FrameDataAllocatorNativeAot* stack)
        {
            Abstraction.FrameDataFragmentNativeAot* frag;

            frag = FrameDataFragNew(memoryManager, out var size);
            stack->first = stack->current = frag;
            stack->infos_capacity = 4;
            stack->infos = (Abstraction.FrameDataInfoNativeAot*)memoryManager.Allocate((uint)(stack->infos_capacity * sizeof(Abstraction.FrameDataInfoNativeAot)));
        }

        private static Abstraction.FrameDataFragmentNativeAot* FrameDataFragNew(MemoryManager memoryManager, out uint size)
        {
            Abstraction.FrameDataFragmentNativeAot* frag = memoryManager.Allocate<Abstraction.FrameDataFragmentNativeAot>();
            size = Abstraction.FrameDataFragmentNativeAot.FRAME_DATA_FRAGMENT_SIZE;
            frag->pos = (byte*)&frag->data;
            frag->end = (byte*)frag + size;
            frag->next = null;
            return frag;
        }

        internal unsafe Abstraction.ThreadContextInstanceAbstractionNativeAot* NativeThreadContext => _nativeThreadContext;
    }

    [UnmanagedCallersOnly]
    private static void TlsInitialize()
    {
        var _ = ExecutionContext.Current;
    }

    [UnmanagedCallersOnly]
    private static unsafe Abstraction.ThreadContextInstanceAbstractionNativeAot* GetCurrentThreadContext()
    {
        return ExecutionContext.Current.NativeThreadContext;
    }

    internal static unsafe Abstraction.EEItf* CreateItf()
    {
        Abstraction.EEItf* itf = Mint.GlobalMemoryManager.Allocate<Abstraction.EEItf>();
        itf->tls_initialize = &TlsInitialize;
        itf->get_context = &GetCurrentThreadContext;
        itf->get_ThreadContext_inst = &Abstraction.Itf.unwrapTransparentAbstraction;
        return itf;
    }

}
