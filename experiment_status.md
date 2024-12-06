# Status 15-NOV-2024

## Aaron's CoreCLR learnings

- `PCODE` manifests itself from a myriad of places.
  - The `MethodDesc::DoPrestub()` entry point should be referenced for many of the APIs that can produce `PCODE`. (for example, `GetStubForInteropMethod()`).
- The ILStubResolver deletes the associated IL post JIT - `ILStubResolver::ClearCompileTimeState`.
  - This means that when an IL stub will be the interpreted, clean-up of generated IL needs to be avoided.
    - If a future CoreCLR interpreter uses a converted byte code approach, then getting rid of the IL may not be an issue.
  - See `CEEInfo::getMethodInfo()` for how IL stubs are impacted, see `MethodDesc::IsWrapperStub()`.
- Created a new `Precode` type to represent the interpreter entry point.
  - See `MethodDescCallSite` for the common ways CoreCLR calls from C++ into managed code. Specifically, `CallTargetWorker()` is of note.
  - The `Precode` approach seems to be a valid way to loop into the execution engine without too much disruption.
- Passing around stub context to P/Invokes needs to be considered in the interpreter case.
  - Disabling P/Invoke stub sharing would avoid this need. ReadyToRun is prior art for disabling stub sharing.

# Status 01-NOV-2024

Audit of W^X locations in CoreCLR has yielded locations that need investigation. The section below links to all locations where generated code is manipulated at run-time and indicates a scenario that needs consideration. Jan Vorlicek did a quick check on the list and added a high level thought on relevance to the CoreCLR interpreter track.

This experimental branch (feature/CoreclrInterpreter) in dotnet/runtimelab is now able to run the interpreter for a "Hello, World" scenario. The work yielded various missing features in the CoreCLR implementation (for example, `UnmanagedCallersOnly`). This work was a result of the multi-year effort to move more code into C#.

Aaron Robinson and Andrew Au are investigating the representation of `PCODE` that should be interpreted. This scenario can most easily be thought of as the result of the `ldftn` instruction and/or a functions pointer in a managed vtable slot.

Current questions being investigated:

* Is a new managed calling convention appropriate?
  * What are the trade offs? (performance, runtime development in native debugger, impact on native AOT/R2R call sites, FCall/QCall)
* How to hide extra data in a `PCODE` pointer and then unpack it.
* Can we narrow the support for the interpreter to make it simpler and force the more complex scenarios (interop, FCall) to require native AOT/R2R?

## Audit W^X locations

Uses of `ExecutableWriterHolder` and `ExecutableWriterHolderNoLog` under `src\coreclr`.

**Collected at hash `9ceb2bfd35ad8edd0342d2b10fef2860ad86ac9b`**

### JanV analysis:

* Let's ignore the debugger stuff for now, the debugger will need some other way to set breakpoints and step that will be platform specific.
* The LoaderHeap won't be used by the interpreter, it manages memory for various stubs and we need to decide on what we would do with each of the stub.
* I believe the Module::FixupVTables is for C++/CLI and we can ignore it
* We can ignore any COM related stuff
* CodeFragmentHeap is used by virtual stub lookup, dispatch, resolve and vtable helpers and dynamic helpers for R2R. We need to figure out how to replace these by a mechanism that doesn't need generated code
* I believe jump stub blocks are only for JIT, so anything related to them should not be needed. I need to double check though if R2R doesn't use them somehow too.
* EEJitManager::allocCodeFragmentBlock - code fragment heap stuff, see my comment above
* UMEntryThunk stuff - we will need to figure out how to handle unmanaged calls to managed code without the generated thunks.
* HostCodeHeap stuff is for JIT only, so we can ignore it
* GC coverage instruction writing - we can ignore it, as it works with jitted code only.
* CEEJitInfo::WriteCodeBytes - JIT only, we can ignore it
* EntryPointSlots::Backpatch_Locked - I don't know yet
* Precode::Allocate - we need to figure out what to do with the PRECODE_THISPTR_RETBUF
* Precode::Reset - I am not sure for which precodes it is used, maybe just the PRECODE_THISPTR_RETBUF. It doesn't make sense to be used for stub and fixup precodes.
* MethodDesc::DoPrestub - This seems to create unboxing stubs, instantiating stubs, array op stub and delegate stubs. These stubs look like some of them are IL stubs and some of them may be IL or native code depending on some defines. For the native ones, we'll need to come up with an alternative plan.
* StubCacheBase - I think this is used by native code stubs only, so once we replace them by something else, this will not be used anymore
* StubUnwindInfoSegmentBoundaryReservationList - this goes away, since except for the unwind info that Andrew temporarily added for the interpreter stub, the stub unwind info is not used anywhere.
* StubLinker::EmitStub - will go away after we replace all native code stubs by some other means
* Stub::NewStub - ditto
* Write barrier copy and state update - we will need to figure out how to handle changing write barriers without patching their code
* VirtualCallStubManager stuff - will go away after we replace the virtual stub lookup, dispatch, resolve and vtable stubs by some other mechanism.
* DynamicHelpers - I believe these are all dynamically generated R2R helpers. We'll need to come up with an alternative way of doing what R2R needs for their cases.
* ThisPtrRetBufPrecode -  will need to be replaced by some other mechanism


### `src\coreclr\debug\ee\controller.cpp`:

Debugger patching for breakpoints.

```
    79          void *pSharedPatchBypassBufferRX = g_pDebugger->GetInteropSafeExecutableHeap()->Alloc(sizeof(SharedPatchBypassBuffer));
    80  #if defined(HOST_OSX) && defined(HOST_ARM64)
    81:         ExecutableWriterHolder<SharedPatchBypassBuffer> sharedPatchBypassBufferWriterHolder((SharedPatchBypassBuffer*)pSharedPatchBypassBufferRX, sizeof(SharedPatchBypassBuffer));
    82          void *pSharedPatchBypassBufferRW = sharedPatchBypassBufferWriterHolder.GetRW();
    83  #else // HOST_OSX && HOST_ARM64
```

x86: ApplyPatch
```
  1405          _ASSERTE(patch->opcode != CEE_BREAK);
  1406
  1407:         ExecutableWriterHolder<BYTE> breakpointWriterHolder((BYTE*)patch->address, 2);
  1408          *(unsigned short *) (breakpointWriterHolder.GetRW()+1) = CEE_BREAK;
  1409
```

x86: UnapplyPatch
```
  1520          _ASSERTE(*(unsigned short*)(patch->address+1) == CEE_BREAK);
  1521
  1522:         ExecutableWriterHolder<BYTE> breakpointWriterHolder((BYTE*)patch->address, 2);
  1523          *(unsigned short *) (breakpointWriterHolder.GetRW()+1)
  1524            = (unsigned short) patch->opcode;
```

Patch skipping.
```
  4398      m_pSharedPatchBypassBuffer = patch->GetOrCreateSharedPatchBypassBuffer();
  4399  #if defined(HOST_OSX) && defined(HOST_ARM64)
  4400:     ExecutableWriterHolder<SharedPatchBypassBuffer> sharedPatchBypassBufferWriterHolder((SharedPatchBypassBuffer*)m_pSharedPatchBypassBuffer, sizeof(SharedPatchBypassBuffer));
  4401      SharedPatchBypassBuffer *pSharedPatchBypassBufferRW = sharedPatchBypassBufferWriterHolder.GetRW();
  4402  #else // HOST_OSX && HOST_ARM64
```

### `src\coreclr\debug\ee\controller.h`:

Lifetime management of shared patch - `AddRef()` / `Release()`
```
  267      {
  268  #if !defined(DACCESS_COMPILE) && defined(HOST_OSX) && defined(HOST_ARM64)
  269:         ExecutableWriterHolder<LONG> refCountWriterHolder(&m_refCount, sizeof(LONG));
  270          LONG *pRefCountRW = refCountWriterHolder.GetRW();
  271  #else // !DACCESS_COMPILE && HOST_OSX && HOST_ARM64

  281      {
  282  #if !DACCESS_COMPILE && HOST_OSX && HOST_ARM64
  283:         ExecutableWriterHolder<LONG> refCountWriterHolder(&m_refCount, sizeof(LONG));
  284          LONG *pRefCountRW = refCountWriterHolder.GetRW();
  285  #else // !DACCESS_COMPILE && HOST_OSX && HOST_ARM64
```

### `src\coreclr\debug\ee\debugger.cpp`:

arm64/macOS: Used for FuncEval
```
  1328
  1329  #if !defined(DBI_COMPILE) && !defined(DACCESS_COMPILE) && defined(HOST_OSX) && defined(HOST_ARM64)
  1330:     ExecutableWriterHolder<DebuggerEvalBreakpointInfoSegment> bpInfoSegmentWriterHolder(bpInfoSegmentRX, sizeof(DebuggerEvalBreakpointInfoSegment));
  1331      DebuggerEvalBreakpointInfoSegment *bpInfoSegmentRW = bpInfoSegmentWriterHolder.GetRW();
  1332  #else // !DBI_COMPILE && !DACCESS_COMPILE && HOST_OSX && HOST_ARM64
```

### `src\coreclr\debug\ee\debugger.h`:

arm64/macOS: Management of `DebuggerHeapExecutableMemoryPage`
```
  1192      {
  1193  #if defined(HOST_OSX) && defined(HOST_ARM64)
  1194:         ExecutableWriterHolder<DebuggerHeapExecutableMemoryPage> debuggerHeapPageWriterHolder(this, sizeof(DebuggerHeapExecutableMemoryPage));
  1195          DebuggerHeapExecutableMemoryPage *pHeapPageRW = debuggerHeapPageWriterHolder.GetRW();
  1196  #else

  1211          ASSERT(newOccupancy <= MAX_CHUNK_MASK);
  1212  #if defined(HOST_OSX) && defined(HOST_ARM64)
  1213:         ExecutableWriterHolder<DebuggerHeapExecutableMemoryPage> debuggerHeapPageWriterHolder(this, sizeof(DebuggerHeapExecutableMemoryPage));
  1214          DebuggerHeapExecutableMemoryPage *pHeapPageRW = debuggerHeapPageWriterHolder.GetRW();
  1215  #else

  1229          SetPageOccupancy(BOOKKEEPING_CHUNK_MASK); // only the first bit is set.
  1230  #if defined(HOST_OSX) && defined(HOST_ARM64)
  1231:         ExecutableWriterHolder<DebuggerHeapExecutableMemoryPage> debuggerHeapPageWriterHolder(this, sizeof(DebuggerHeapExecutableMemoryPage));
  1232          DebuggerHeapExecutableMemoryPage *pHeapPageRW = debuggerHeapPageWriterHolder.GetRW();
  1233  #else
```

### `src\coreclr\debug\ee\arm64\walker.cpp`:

arm64/macOS: Used for walking and patch skipping - see `controller.cpp` that that is the single caller for this method.
```
  178
  179  #if defined(HOST_OSX) && defined(HOST_ARM64)
  180:         ExecutableWriterHolder<UINT_PTR> ripTargetFixupWriterHolder(&m_pSharedPatchBypassBuffer->RipTargetFixup, sizeof(UINT_PTR));
  181          UINT_PTR *pRipTargetFixupRW = ripTargetFixupWriterHolder.GetRW();
  182  #else // HOST_OSX && HOST_ARM64
```

### `src\coreclr\debug\inc\arm64\primitives.h`:

macOS: Setting of current instruction - `CORDbgSetInstruction()`.
```
  152
  153  #if !defined(DBI_COMPILE) && !defined(DACCESS_COMPILE) && defined(HOST_OSX)
  154:     ExecutableWriterHolder<void> instructionWriterHolder((LPVOID)address, sizeof(PRD_TYPE));
  155
  156      TADDR ptraddr = dac_cast<TADDR>(instructionWriterHolder.GetRW());
```

### `src\coreclr\utilcode\loaderheap.cpp`:

DEBUG mechanism to "invalidate" some memory.
```
   715              {
   716                  void* pMemRW = pMem;
   717:                 ExecutableWriterHolderNoLog<void> memWriterHolder;
   718                  if (pHeap->IsExecutable())
   719                  {
```

The loader heap guarantees allocated memory is zero initialized.
```
   786              {
   787                  void *pResultRW = pResult;
   788:                 ExecutableWriterHolderNoLog<void> resultWriterHolder;
   789                  if (pHeap->IsExecutable())
   790                  {
```

Set to "invalidate" memory in DEBUG and merge blocks together. This check is only
for executable memory.
```
   821                  LoaderHeapFreeBlock *pNextNextBlock = pNextBlock->m_pNext;
   822                  void *pMemRW = pFreeBlock->m_pBlockAddress;
   823:                 ExecutableWriterHolderNoLog<void> memWriterHolder;
   824                  if (pHeap->IsExecutable())
   825                  {
```

`CommitPages()`
```
  1093          }
  1094
  1095:         ExecutableWriterHolder<BYTE> codePageWriterHolder((BYTE*)pData, dwSizeToCommitPart, ExecutableAllocator::DoNotAddToCache);
  1096          m_codePageGenerator(codePageWriterHolder.GetRW(), (BYTE*)pData, dwSizeToCommitPart);
  1097          FlushInstructionCache(GetCurrentProcess(), pData, dwSizeToCommitPart);
```

DEBUG checks in `UnlockedAllocMem_NoThrow`
```
  1445  #ifdef _DEBUG
  1446              BYTE *pAllocatedBytes = (BYTE*)pData;
  1447:             ExecutableWriterHolderNoLog<void> dataWriterHolder;
  1448              if (IsExecutable())
  1449              {
```

`UnlockedLoaderHeap::UnlockedBackoutMem` - back out allocation?
```
  1640          {
  1641              void *pMemRW = pMem;
  1642:             ExecutableWriterHolderNoLog<void> memWriterHolder;
  1643              if (IsExecutable())
  1644              {
```

DEBUG check in `UnlockedLoaderHeap::UnlockedAllocAlignedMem_NoThrow`
```
  1768  #ifdef _DEBUG
  1769      BYTE *pAllocatedBytes = (BYTE *)pResult;
  1770:     ExecutableWriterHolderNoLog<void> resultWriterHolder;
  1771      if (IsExecutable())
  1772      {
```

### `src\coreclr\vm\ceeload.cpp`:

`Module::FixupVTables()` - C++/CLI or COM interop?

All under the following branch:

```cpp
    else if (pFixupTable[iFixup].Type == (COR_VTABLE_PTRSIZED | COR_VTABLE_FROM_UNMANAGED) ||
            (pFixupTable[iFixup].Type == (COR_VTABLE_PTRSIZED | COR_VTABLE_FROM_UNMANAGED_RETAIN_APPDOMAIN)))
```

```
  3336
  3337                      UMEntryThunk *pUMEntryThunk = (UMEntryThunk*)(void*)(GetDllThunkHeap()->AllocAlignedMem(sizeof(UMEntryThunk), CODE_SIZE_ALIGN)); // UMEntryThunk contains code
  3338:                     ExecutableWriterHolder<UMEntryThunk> uMEntryThunkWriterHolder(pUMEntryThunk, sizeof(UMEntryThunk));
  3339                      FillMemory(uMEntryThunkWriterHolder.GetRW(), sizeof(UMEntryThunk), 0);
  3340
  3341                      UMThunkMarshInfo *pUMThunkMarshInfo = (UMThunkMarshInfo*)(void*)(GetThunkHeap()->AllocAlignedMem(sizeof(UMThunkMarshInfo), CODE_SIZE_ALIGN));
  3342:                     ExecutableWriterHolder<UMThunkMarshInfo> uMThunkMarshInfoWriterHolder(pUMThunkMarshInfo, sizeof(UMThunkMarshInfo));
  3343                      FillMemory(uMThunkMarshInfoWriterHolder.GetRW(), sizeof(UMThunkMarshInfo), 0);
  3344
```

### `src\coreclr\vm\class.cpp`:

Clean-up in `EEClass::Destruct` for `Delegate`s that have a shuffle thunk.
```
  138              else
  139              {
  140:                 ExecutableWriterHolder<Stub> stubWriterHolder(pThunk, sizeof(Stub));
  141                  stubWriterHolder.GetRW()->DecRef();
  142              }
```

### `src\coreclr\vm\clrtocomcall.cpp`:

`CLRToCOMCall::GetRetThunk`
```
  983          pRetThunk = (LPVOID)dummyAmTracker.Track(SystemDomain::GetGlobalLoaderAllocator()->GetExecutableHeap()->AllocMem(S_SIZE_T(thunkSize)));
  984
  985:         ExecutableWriterHolder<BYTE> thunkWriterHolder((BYTE *)pRetThunk, thunkSize);
  986          BYTE *pThunkRW = thunkWriterHolder.GetRW();
  987
```

### `src\coreclr\vm\codeman.cpp`:

`CodeFragmentHeap::RealBackoutMem` - Zero out passed in memory.
```
  2129
  2130      {
  2131:         ExecutableWriterHolder<BYTE> memWriterHolder((BYTE*)pMem, dwSize);
  2132          ZeroMemory(memWriterHolder.GetRW(), dwSize);
  2133      }
```

64-bit: `LoaderCodeHeap::CreateCodeHeap`
```
  2459
  2460  #ifdef TARGET_64BIT
  2461:     ExecutableWriterHolder<BYTE> personalityRoutineWriterHolder(pHp->CLRPersonalityRoutine, 12);
  2462      emitJump(pHp->CLRPersonalityRoutine, personalityRoutineWriterHolder.GetRW(), (void *)ProcessCLRException);
  2463  #endif // TARGET_64BIT
```

`EEJitManager::allocJumpStubBlock` - Set up stub
```
  3166
  3167      TADDR                  mem;
  3168:     ExecutableWriterHolderNoLog<JumpStubBlockHeader> blockWriterHolder;
  3169
  3170      // Scope the lock

  3181          // CodeHeader comes immediately before the block
  3182          CodeHeader * pCodeHdr = (CodeHeader *) (mem - sizeof(CodeHeader));
  3183:         ExecutableWriterHolder<CodeHeader> codeHdrWriterHolder(pCodeHdr, sizeof(CodeHeader));
  3184          codeHdrWriterHolder.GetRW()->SetStubCodeBlockKind(STUB_CODE_BLOCK_JUMPSTUB);
  3185
```

`EEJitManager::allocCodeFragmentBlock`
```
  3233          // CodeHeader comes immediately before the block
  3234          CodeHeader * pCodeHdr = (CodeHeader *) (mem - sizeof(CodeHeader));
  3235:         ExecutableWriterHolder<CodeHeader> codeHdrWriterHolder(pCodeHdr, sizeof(CodeHeader));
  3236          codeHdrWriterHolder.GetRW()->SetStubCodeBlockKind(kind);
  3237
```

`ExecutionManager::getNextJumpStub`
```
  5047      JumpStubBlockHeader ** ppHead   = &(pJumpStubCache->m_pBlocks);
  5048      JumpStubBlockHeader *  curBlock = *ppHead;
  5049:     ExecutableWriterHolderNoLog<JumpStubBlockHeader> curBlockWriterHolder;
  5050
  5051      // allocate a new jumpstub from 'curBlock' if it is not fully allocated
```

### `src\coreclr\vm\comcallablewrapper.cpp`:

`ComPreStubWorker` - replace prestub with new stub.
```
   489              UINT_PTR* ppofs = (UINT_PTR*)  (((BYTE*)pCMD) - COMMETHOD_CALL_PRESTUB_SIZE + COMMETHOD_CALL_PRESTUB_ADDRESS_OFFSET);
   490
   491:             ExecutableWriterHolder<UINT_PTR> ppofsWriterHolder(ppofs, sizeof(UINT_PTR));
   492  #ifdef TARGET_X86
   493              *ppofsWriterHolder.GetRW() = ((UINT_PTR)pStub - (size_t)pCMD);
```

`ComMethodTable::LayOutClassMethodTable` - zero initialize memory.
```
  3233      if (!m_pMT->HasGenericClassInstantiationInHierarchy())
  3234      {
  3235:         ExecutableWriterHolderNoLog<BYTE> methodDescMemoryWriteableHolder;
  3236          //
  3237          // Allocate method desc's for the rest of the slots.

  3454              return;
  3455
  3456:         ExecutableWriterHolder<ComMethodTable> comMTWriterHolder(this, sizeof(ComMethodTable) + cbTempVtable.Value());
  3457
  3458          // IDispatch vtable follows the header
```

`ComMethodTable::LayOutInterfaceMethodTable` - Copy over vtable
```
  3609              return TRUE;
  3610
  3611:         ExecutableWriterHolder<ComMethodTable> comMTWriterHolder(this, sizeof(ComMethodTable) + cbTempVtable.Value());
  3612          size_t writeableOffset = (BYTE*)comMTWriterHolder.GetRW() - (BYTE*)this;
  3613
```

`ComMethodTable::GetDispatchInfo` - Set the `DispatchInfo` on the `ComMethodTable`.
```
  3753          pDispInfo->SynchWithManagedView();
  3754
  3755:         ExecutableWriterHolder<ComMethodTable> comMTWriterHolder(this, sizeof(ComMethodTable));
  3756          // Swap the lock into the class member in a thread safe manner.
  3757          if (NULL == InterlockedCompareExchangeT(&comMTWriterHolder.GetRW()->m_pDispatchInfo, pDispInfo.GetValue(), NULL))
```

`ComMethodTable::SetITypeInfo`
```
  3777      CONTRACTL_END;
  3778
  3779:     ExecutableWriterHolder<ComMethodTable> comMTWriterHolder(this, sizeof(ComMethodTable));
  3780      if (InterlockedCompareExchangeT(&comMTWriterHolder.GetRW()->m_pITypeInfo, pNew, NULL) == NULL)
  3781      {
```

`ComCallWrapperTemplate::CreateComMethodTableForClass` - Initialize `ComMethodTable` for `IClassX`.
```
  4333      _ASSERTE(!cbNewSlots.IsOverflow() && !cbTotalSlots.IsOverflow() && !cbVtable.IsOverflow());
  4334
  4335:     ExecutableWriterHolder<ComMethodTable> comMTWriterHolder(pComMT, cbToAlloc.Value());
  4336      ComMethodTable* pComMTRW = comMTWriterHolder.GetRW();
  4337      // set up the header
```

`ComCallWrapperTemplate::CreateComMethodTableForInterface` - Initialize `ComMethodTable` for interface.
```
  4410      _ASSERTE(!cbVtable.IsOverflow() && !cbMethDescs.IsOverflow());
  4411
  4412:     ExecutableWriterHolder<ComMethodTable> comMTWriterHolder(pComMT, cbToAlloc.Value());
  4413      ComMethodTable* pComMTRW = comMTWriterHolder.GetRW();
  4414
```

`ComCallWrapperTemplate::CreateComMethodTableForBasic`
```
  4474      AllocMemHolder<ComMethodTable> pComMT(pMT->GetLoaderAllocator()->GetStubHeap()->AllocMem(S_SIZE_T(cbToAlloc)));
  4475
  4476:     ExecutableWriterHolder<ComMethodTable> comMTWriterHolder(pComMT, cbToAlloc);
  4477      ComMethodTable* pComMTRW = comMTWriterHolder.GetRW();
  4478
```

### `src\coreclr\vm\comcallablewrapper.h`:

Lifetime management, `AddRef()`/`Release()`, for `ComMethodTable`.
```
  411          LIMITED_METHOD_CONTRACT;
  412
  413:         ExecutableWriterHolder<ComMethodTable> comMTWriterHolder(this, sizeof(ComMethodTable));
  414          return InterlockedIncrement(&comMTWriterHolder.GetRW()->m_cbRefCount);
  415      }

  426          CONTRACTL_END;
  427
  428:         ExecutableWriterHolder<ComMethodTable> comMTWriterHolder(this, sizeof(ComMethodTable));
  429          // use a different var here becuase cleanup will delete the object
  430          // so can no longer make member refs
```

`ComMethodTable::GetIID` - generate GUID for `IClassX`, update `ComMethodTable`.
```
  671          if (!(m_Flags & enum_GuidGenerated))
  672          {
  673:             ExecutableWriterHolder<ComMethodTable> comMTWriterHolder(this, sizeof(ComMethodTable));
  674              GenerateClassItfGuid(TypeHandle(m_pMT), &comMTWriterHolder.GetRW()->m_IID);
  675              comMTWriterHolder.GetRW()->m_Flags |= enum_GuidGenerated;
```

### `src\coreclr\vm\comdelegate.cpp`:

`SetupShuffleThunk`
```
   952          else
   953          {
   954:             ExecutableWriterHolder<Stub> shuffleThunkWriterHolder(pShuffleThunk, sizeof(Stub));
   955              shuffleThunkWriterHolder.GetRW()->DecRef();
   956          }
```

`COMDelegate::ConvertToCallback` - backout on losing the allocation race and create a Thunk.
```
  1352                  pUMThunkMarshInfo = (UMThunkMarshInfo*)(void*)pMT->GetLoaderAllocator()->GetStubHeap()->AllocMem(S_SIZE_T(sizeof(UMThunkMarshInfo)));
  1353
  1354:                 ExecutableWriterHolder<UMThunkMarshInfo> uMThunkMarshInfoWriterHolder(pUMThunkMarshInfo, sizeof(UMThunkMarshInfo));
  1355                  uMThunkMarshInfoWriterHolder.GetRW()->LoadTimeInit(pInvokeMeth);
  1356

  1378              PCODE pManagedTargetForDiagnostics = (pDelegate->GetMethodPtrAux() != (PCODE)NULL) ? pDelegate->GetMethodPtrAux() : pDelegate->GetMethodPtr();
  1379
  1380:             ExecutableWriterHolder<UMEntryThunk> uMEntryThunkWriterHolder(pUMEntryThunk, sizeof(UMEntryThunk));
  1381
  1382              // MethodDesc is passed in for profiling to know the method desc of target
```

### `src\coreclr\vm\comtoclrcall.cpp`:

non-x86: `ComCallMethodDesc::CreateCOMToCLRStub`
```
   713  #else // TARGET_X86
   714
   715:     ExecutableWriterHolder<ComCallMethodDesc> comCallMDWriterHolder(this, sizeof(ComCallMethodDesc));
   716
   717      if (pStubMD->IsILStub())
```

x86: `ComCallMethodDesc::InitRuntimeNativeInfo`
```
   811
   812      // write the computed data into this ComCallMethodDesc
   813:     ExecutableWriterHolder<ComCallMethodDesc> comCallMDWriterHolder(this, sizeof(ComCallMethodDesc));
   814      comCallMDWriterHolder.GetRW()->m_dwSlotInfo = (wSourceSlotEDX | (wStubStackSlotCount << 16));
   815      if (pwStubStackSlotOffsets != NULL)
```

`ComCall::GetComCallMethodStub` - Set stub on `ComCallMethodDesc` via `GetAddrOfILStubField`.
```
  1287      pCMD->InitRuntimeNativeInfo(pStubMD);
  1288
  1289:     ExecutableWriterHolder<PCODE> addrOfILStubWriterHolder(pCMD->GetAddrOfILStubField(), sizeof(PCODE));
  1290      InterlockedCompareExchangeT<PCODE>(addrOfILStubWriterHolder.GetRW(), pTempILStub, NULL);
  1291
```

### `src\coreclr\vm\dllimportcallback.cpp`:

`UMEntryThunkFreeList::AddToList` - set next free thunk.
```
   81          else
   82          {
   83:             ExecutableWriterHolder<UMEntryThunk> tailThunkWriterHolder(m_pTail, sizeof(UMEntryThunk));
   84              tailThunkWriterHolder.GetRW()->m_pNextFreeThunk = pThunkRX;
   85              m_pTail = pThunkRX;
```

`UMEntryThunkCache::GetUMEntryThunk` - Cache miss and add a new entry.
```
  163          miHolder.Assign(pMarshInfo);
  164
  165:         ExecutableWriterHolder<UMThunkMarshInfo> marshInfoWriterHolder(pMarshInfo, sizeof(UMThunkMarshInfo));
  166          marshInfoWriterHolder.GetRW()->LoadTimeInit(pMD);
  167
  168:         ExecutableWriterHolder<UMEntryThunk> thunkWriterHolder(pThunk, sizeof(UMEntryThunk));
  169          thunkWriterHolder.GetRW()->LoadTimeInit(pThunk, (PCODE)NULL, NULL, pMarshInfo, pMD);
  170
```

`TheUMEntryPrestubWorker`
```
  218      INSTALL_UNWIND_AND_CONTINUE_HANDLER;
  219
  220:     ExecutableWriterHolder<UMEntryThunk> uMEntryThunkWriterHolder(pUMEntryThunk, sizeof(UMEntryThunk));
  221      uMEntryThunkWriterHolder.GetRW()->RunTimeInit(pUMEntryThunk);
  222
```

`UMEntryThunk::Terminate` - zero initialize member.
```
  258      CONTRACTL_END;
  259
  260:     ExecutableWriterHolder<UMEntryThunk> thunkWriterHolder(this, sizeof(UMEntryThunk));
  261      m_code.Poison();
  262
```

### `src\coreclr\vm\dllimportcallback.h`:

`UMEntryThunk::RunTimeInit` - initialize members.
```
  182          m_pMD->EnsureActive();
  183
  184:         ExecutableWriterHolder<UMThunkMarshInfo> uMThunkMarshInfoWriterHolder(m_pUMThunkMarshInfo, sizeof(UMThunkMarshInfo));
  185          uMThunkMarshInfoWriterHolder.GetRW()->RunTimeInit();
  186
```

### `src\coreclr\vm\dynamicmethod.cpp`:

64-bit: `HostCodeHeap::InitializeHeapList`
```
  470
  471  #ifdef HOST_64BIT
  472:     ExecutableWriterHolder<BYTE> personalityRoutineWriterHolder(pHp->CLRPersonalityRoutine, 12);
  473      emitJump(pHp->CLRPersonalityRoutine, personalityRoutineWriterHolder.GetRW(), (void *)ProcessCLRException);
  474  #endif
```

`HostCodeHeap::AllocFromFreeList` - `TrackAllocation`
```
  506                  LOG((LF_BCL, LL_INFO100, "Level2 - CodeHeap [0x%p] - Block found, size 0x%X\n", this, pCurrent->size));
  507
  508:                 ExecutableWriterHolderNoLog<TrackAllocation> previousWriterHolder;
  509                  if (pPrevious)
  510                  {

  512                  }
  513
  514:                 ExecutableWriterHolder<TrackAllocation> currentWriterHolder(pCurrent, sizeof(TrackAllocation));
  515
  516                  // The space left is not big enough for a new block, let's just

  534                      TrackAllocation *pNewCurrent = (TrackAllocation*)((BYTE*)pCurrent + realSize);
  535
  536:                     ExecutableWriterHolder<TrackAllocation> newCurrentWriterHolder(pNewCurrent, sizeof(TrackAllocation));
  537                      newCurrentWriterHolder.GetRW()->pNext = pCurrent->pNext;
  538                      newCurrentWriterHolder.GetRW()->size = pCurrent->size - realSize;
```

`HostCodeHeap::AddToFreeList`
```
  588                  // found the point of insertion
  589                  pBlockToInsertRW->pNext = pCurrent;
  590:                 ExecutableWriterHolderNoLog<TrackAllocation> previousWriterHolder;
  591
  592                  if (pPrevious)

  636          pBlockToInsertRW->pNext = NULL;
  637          // last in the list
  638:         ExecutableWriterHolder<TrackAllocation> previousWriterHolder2(pPrevious, sizeof(TrackAllocation));
  639
  640          if ((BYTE*)pPrevious + pPrevious->size == (BYTE*)pBlockToInsert)
```

`HostCodeHeap::AllocMemForCode_NoThrow`
```
  694      // Pointer to the TrackAllocation record is stored just before the code header
  695      CodeHeader * pHdr = (CodeHeader *)pCode - 1;
  696:     ExecutableWriterHolder<TrackAllocation *> trackerWriterHolder((TrackAllocation **)(pHdr) - 1, sizeof(TrackAllocation *));
  697      *trackerWriterHolder.GetRW() = pTracker;
  698
```

`HostCodeHeap::AllocMemory_NoThrow`
```
  763
  764              TrackAllocation *pBlockToInsert = (TrackAllocation*)(void*)m_pLastAvailableCommittedAddr;
  765:             ExecutableWriterHolder<TrackAllocation> blockToInsertWriterHolder(pBlockToInsert, sizeof(TrackAllocation));
  766
  767              blockToInsertWriterHolder.GetRW()->pNext = NULL;
```

`HostCodeHeap::FreeMemForCode`
```
  854
  855      TrackAllocation *pTracker = HostCodeHeap::GetTrackAllocation((TADDR)codeStart);
  856:     ExecutableWriterHolder<TrackAllocation> trackerWriterHolder(pTracker, sizeof(TrackAllocation));
  857      AddToFreeList(pTracker, trackerWriterHolder.GetRW());
  858
```

### `src\coreclr\vm\gccover.cpp`:

Various GC stress operations.

```
  456
  457      BYTE * codeStart = (BYTE *)pCode;
  458:     ExecutableWriterHolderNoLog<BYTE> codeWriterHolder;
  459      size_t writeableOffset;
  460

   723          _ASSERTE(!IsGcCoverageInterruptInstruction(instrPtr));
   724
   725:         ExecutableWriterHolder<BYTE> instrPtrWriterHolder(instrPtr, sizeof(DWORD));
   726  #if defined(TARGET_ARM)
   727          size_t instrLen = GetARMInstructionLength(instrPtr);

   839  #endif
   840
   841:     ExecutableWriterHolder<BYTE> instrPtrWriterHolder(instrPtr - instrLen, 2 * instrLen);
   842      if(instructionIsACallThroughRegister)
   843      {

   949      while(acrossHotRegion--)
   950      {
   951:         ExecutableWriterHolder<BYTE> instrPtrWriterHolder(rangeStart, rangeStop - rangeStart);
   952          PBYTE instrPtrRW =  instrPtrWriterHolder.GetRW();
   953          PBYTE rangeStopRW = instrPtrRW + (rangeStop - rangeStart);

  1333  void RemoveGcCoverageInterrupt(TADDR instrPtr, BYTE * savedInstrPtr, GCCoverageInfo* gcCover, DWORD offset)
  1334  {
  1335:     ExecutableWriterHolder<void> instrPtrWriterHolder((void*)instrPtr, 4);
  1336  #ifdef TARGET_ARM
  1337      if (GetARMInstructionLength(savedInstrPtr) == 2)

  1726          if (target != 0)
  1727          {
  1728:             ExecutableWriterHolder<BYTE> nextInstrWriterHolder(nextInstr, sizeof(DWORD));
  1729              if (!pThread->PreemptiveGCDisabled())
  1730              {
```

### `src\coreclr\vm\jitinterface.cpp`:

`CEEJitInfo::WriteCodeBytes` - Called by `CEEJitInfo::WriteCode` and `CEEJitInfo::BackoutJitData`.
```
  10901      if (m_CodeHeaderRW != m_CodeHeader)
  10902      {
  10903:         ExecutableWriterHolder<void> codeWriterHolder((void *)m_CodeHeader, m_codeWriteBufferSize);
  10904          memcpy(codeWriterHolder.GetRW(), m_CodeHeaderRW, m_codeWriteBufferSize);
  10905      }
```

### `src\coreclr\vm\methoddescbackpatchinfo.cpp`:

`EntryPointSlots::Backpatch_Locked` - `SlotType_Executable` and `SlotType_ExecutableRel32` cases.
```
  38          case SlotType_Executable:
  39          {
  40:             ExecutableWriterHolder<void> slotWriterHolder((void*)slot, sizeof(PCODE*));
  41              *(PCODE *)slotWriterHolder.GetRW() = entryPoint;
  42              goto Flush;

  48              _ASSERTE(sizeof(void *) <= 4);
  49
  50:             ExecutableWriterHolder<void> slotWriterHolder((void*)slot, sizeof(PCODE*));
  51              *(PCODE *)slotWriterHolder.GetRW() = entryPoint - ((PCODE)slot + sizeof(PCODE));
  52              // fall through
```

### `src\coreclr\vm\precode.cpp`:

`Precode::Allocate` - Only for `PRECODE_THISPTR_RETBUF`.
```
  230      {
  231          pPrecode = (Precode*)pamTracker->Track(pLoaderAllocator->GetPrecodeHeap()->AllocAlignedMem(size, AlignOf(t)));
  232:         ExecutableWriterHolder<Precode> precodeWriterHolder(pPrecode, size);
  233          precodeWriterHolder.GetRW()->Init(pPrecode, t, pMD, pLoaderAllocator);
  234          ClrFlushInstructionCache(pPrecode, size);
```

`Precode::Reset` - All except `PRECODE_FIXUP`.
```
  353      else
  354      {
  355:         ExecutableWriterHolder<Precode> precodeWriterHolder(this, size);
  356          precodeWriterHolder.GetRW()->Init(this, t, pMD, pMD->GetLoaderAllocator());
  357          ClrFlushInstructionCache(this, SizeOf(), /* hasCodeExecutedBefore */ true);
```

### `src\coreclr\vm\prestub.cpp`:

`MethodDesc::DoPrestub` - Where the stub doesn't have an external entry point, `EXTERNAL_ENTRY_BIT`.
```
  2995              else
  2996              {
  2997:                 ExecutableWriterHolder<Stub> stubWriterHolder(pStub, sizeof(Stub));
  2998                  stubWriterHolder.GetRW()->DecRef();
  2999              }
```

### `src\coreclr\vm\stubcache.cpp`:

`StubCacheBase::~StubCacheBase`
```
   60      {
   61          _ASSERTE(NULL != phe->m_pStub);
   62:         ExecutableWriterHolder<Stub> stubWriterHolder(phe->m_pStub, sizeof(Stub));
   63          stubWriterHolder.GetRW()->DecRef();
   64          phe = (STUBHASHENTRY*)GetNext((BYTE*)phe);
```

`StubCacheBase::Canonicalize` - Only used in the delegate case involving shuffle thunks - see `SetupShuffleThunk`.
```
   97              pstub = phe->m_pStub;
   98
   99:             ExecutableWriterHolder<Stub> stubWriterHolder(pstub, sizeof(Stub));
  100              // IncRef as we're returning a reference to our caller.
  101              stubWriterHolder.GetRW()->IncRef();

  152              }
  153              // IncRef so that caller has firm ownership of stub.
  154:             ExecutableWriterHolder<Stub> stubWriterHolder(pstub, sizeof(Stub));
  155              stubWriterHolder.GetRW()->IncRef();
  156          }
```

### `src\coreclr\vm\stublink.cpp`:

`StubUnwindInfoSegmentBoundaryReservationList` - clean-up and initialization scenarios.
```
   309              ReservationList *pNext = pList->pNext;
   310
   311:             ExecutableWriterHolder<Stub> stubWriterHolder(pList->GetStub(), sizeof(Stub));
   312              stubWriterHolder.GetRW()->DecRef();
   313

   322          ReservationList *pList = ReservationList::FromStub(pStub);
   323
   324:         ExecutableWriterHolder<ReservationList> listWriterHolder(pList, sizeof(ReservationList));
   325          listWriterHolder.GetRW()->pNext = m_pList;
   326          m_pList = pList;
```

`StubLinker::EmitStub` - Stub creation. Used in `interpreter.cpp`, `prestub.cpp` and `jitinterfacex86.cpp`.
```
  1011      BYTE *pCode = (BYTE*)(pStub->GetBlob());
  1012
  1013:     ExecutableWriterHolder<Stub> stubWriterHolder(pStub, sizeof(Stub) + totalSize);
  1014      Stub *pStubRW = stubWriterHolder.GetRW();
  1015
```

`Stub::NewStub` - Used in many scenarios - `GenerateArrayOpStub`, shuffle thunks, `Delegate::Invoke`, Unboxing stubs, Instantiating stubs.
```
  2163      Stub* pStubRX = (Stub*)(pBlock + stubPayloadOffset);
  2164      Stub* pStubRW;
  2165:     ExecutableWriterHolderNoLog<Stub> stubWriterHolder;
  2166
  2167      if (pHeap == NULL)
```

### `src\coreclr\vm\threads.cpp`:

`InitThreadManager()` - `if (IsWriteBarrierCopyEnabled())`
```
  1057          {
  1058              size_t writeBarrierSize = (BYTE*)JIT_PatchedCodeLast - (BYTE*)JIT_PatchedCodeStart;
  1059:             ExecutableWriterHolder<void> barrierWriterHolder(s_barrierCopy, writeBarrierSize);
  1060              memcpy(barrierWriterHolder.GetRW(), (BYTE*)JIT_PatchedCodeStart, writeBarrierSize);
  1061          }
```

### `src\coreclr\vm\threadsuspend.cpp`:

GC stress scenario.
```
  3572          assert(pbSrcCode != NULL);
  3573
  3574:         ExecutableWriterHolder<BYTE> destCodeWriterHolder(pbDestCode, sizeof(DWORD));
  3575
  3576  #if defined(TARGET_X86) || defined(TARGET_AMD64)
```

### `src\coreclr\vm\virtualcallstub.cpp`:

`VirtualCallStubManager::GenerateVTableCallStub`
```
  1054      size_t vtableHolderSize = VTableCallHolder::GetHolderSize(slot);
  1055      VTableCallHolder * pHolder = (VTableCallHolder*)(void*)vtable_heap->AllocAlignedMem(vtableHolderSize, CODE_SIZE_ALIGN);
  1056:     ExecutableWriterHolder<VTableCallHolder> vtableWriterHolder(pHolder, vtableHolderSize);
  1057      vtableWriterHolder.GetRW()->Initialize(slot);
  1058
```

`VirtualCallStubManager::GenerateDispatchStub`
```
  2431  #endif
  2432
  2433:     ExecutableWriterHolder<DispatchHolder> dispatchWriterHolder(holder, dispatchHolderSize);
  2434      dispatchWriterHolder.GetRW()->Initialize(holder, addrOfCode,
  2435                         addrOfFail,
```

`VirtualCallStubManager::GenerateDispatchStubLong`
```
  2494      size_t dispatchHolderSize = DispatchHolder::GetHolderSize(DispatchStub::e_TYPE_LONG);
  2495      DispatchHolder * holder = (DispatchHolder*) (void*)dispatch_heap->AllocAlignedMem(dispatchHolderSize, CODE_SIZE_ALIGN);
  2496:     ExecutableWriterHolder<DispatchHolder> dispatchWriterHolder(holder, dispatchHolderSize);
  2497
  2498      dispatchWriterHolder.GetRW()->Initialize(holder, addrOfCode,
```

`VirtualCallStubManager::GenerateResolveStub`
```
  2602      ResolveHolder * holder = (ResolveHolder*) (void*)
  2603          resolve_heap->AllocAlignedMem(sizeof(ResolveHolder), CODE_SIZE_ALIGN);
  2604:     ExecutableWriterHolder<ResolveHolder> resolveWriterHolder(holder, sizeof(ResolveHolder));
  2605
  2606      resolveWriterHolder.GetRW()->Initialize(holder,
```

`VirtualCallStubManager::GenerateLookupStub`
```
  2642      //allocate from the requisite heap and copy the template over it.
  2643      LookupHolder * holder     = (LookupHolder*) (void*) lookup_heap->AllocAlignedMem(sizeof(LookupHolder), CODE_SIZE_ALIGN);
  2644:     ExecutableWriterHolder<LookupHolder> lookupWriterHolder(holder, sizeof(LookupHolder));
  2645
  2646      lookupWriterHolder.GetRW()->Initialize(holder, addrOfResolver, dispatchToken);
```

### `src\coreclr\vm\amd64\cgenamd64.cpp`:

`UMEntryThunkCode::Poison()`
```
  599      CONTRACTL_END;
  600
  601:     ExecutableWriterHolder<UMEntryThunkCode> thunkWriterHolder(this, sizeof(UMEntryThunkCode));
  602      UMEntryThunkCode *pThisRW = thunkWriterHolder.GetRW();
  603
```

Defined in `BEGIN_DYNAMIC_HELPER_EMIT` macro and used in many functions on `DynamicHelpers`.
```
  769      SIZE_T cbAligned = ALIGN_UP(cb, DYNAMIC_HELPER_ALIGNMENT); \
  770      BYTE * pStartRX = (BYTE *)(void*)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(cbAligned, DYNAMIC_HELPER_ALIGNMENT); \
  771:     ExecutableWriterHolder<BYTE> startWriterHolder(pStartRX, cbAligned); \
  772      BYTE * pStart = startWriterHolder.GetRW(); \
  773      size_t rxOffset = pStartRX - pStart; \
```

`DynamicHelpers::CreateDictionaryLookupHelper` - Called in `prestub.cpp` for `ENCODE_DICTIONARY_LOOKUP_THISOBJ`, `ENCODE_DICTIONARY_LOOKUP_TYPE` and `ENCODE_DICTIONARY_LOOKUP_METHOD`.
```
  996
  997      GenericHandleArgs * pArgs = (GenericHandleArgs *)(void *)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(sizeof(GenericHandleArgs), DYNAMIC_HELPER_ALIGNMENT);
  998:     ExecutableWriterHolder<GenericHandleArgs> argsWriterHolder(pArgs, sizeof(GenericHandleArgs));
  999      argsWriterHolder.GetRW()->dictionaryIndexAndSlot = dictionaryIndexAndSlot;
  1000      argsWriterHolder.GetRW()->signature = pLookup->signature;
```

### `src\coreclr\vm\amd64\jitinterfaceamd64.cpp`:

`WriteBarrierManager::ChangeWriteBarrierTo` - Alter the `JIT_WriteBarrier`?
```
  424      // are actually looking into the JIT_WriteBarrier buffer
  425      {
  426:         ExecutableWriterHolder<void> writeBarrierWriterHolder(GetWriteBarrierCodeLocation((void*)JIT_WriteBarrier), GetCurrentWriteBarrierSize());
  427          memcpy(writeBarrierWriterHolder.GetRW(), (LPVOID)GetCurrentWriteBarrierCode(), GetCurrentWriteBarrierSize());
  428          stompWBCompleteActions |= SWB_ICACHE_FLUSH;
```

`WriteBarrierManager::UpdateEphemeralBounds`
```
  793              if (*(UINT64*)m_pUpperBoundImmediate != (size_t)g_ephemeral_high)
  794              {
  795:                 ExecutableWriterHolder<UINT64> upperBoundWriterHolder((UINT64*)m_pUpperBoundImmediate, sizeof(UINT64));
  796                  *upperBoundWriterHolder.GetRW() = (size_t)g_ephemeral_high;
  797                  stompWBCompleteActions |= SWB_ICACHE_FLUSH;

  807              if (*(UINT64*)m_pLowerBoundImmediate != (size_t)g_ephemeral_low)
  808              {
  809:                 ExecutableWriterHolder<UINT64> lowerBoundImmediateWriterHolder((UINT64*)m_pLowerBoundImmediate, sizeof(UINT64));
  810                  *lowerBoundImmediateWriterHolder.GetRW() = (size_t)g_ephemeral_low;
  811                  stompWBCompleteActions |= SWB_ICACHE_FLUSH;
```

`WriteBarrierManager::UpdateWriteWatchAndCardTableLocations`
```
  862              if (*(UINT64*)m_pWriteWatchTableImmediate != (size_t)g_sw_ww_table)
  863              {
  864:                 ExecutableWriterHolder<UINT64> writeWatchTableImmediateWriterHolder((UINT64*)m_pWriteWatchTableImmediate, sizeof(UINT64));
  865                  *writeWatchTableImmediateWriterHolder.GetRW() = (size_t)g_sw_ww_table;
  866                  stompWBCompleteActions |= SWB_ICACHE_FLUSH;

  881              if (*(UINT64*)m_pRegionToGenTableImmediate != (size_t)g_region_to_generation_table)
  882              {
  883:                 ExecutableWriterHolder<UINT64> writeWatchTableImmediateWriterHolder((UINT64*)m_pRegionToGenTableImmediate, sizeof(UINT64));
  884                  *writeWatchTableImmediateWriterHolder.GetRW() = (size_t)g_region_to_generation_table;
  885                  stompWBCompleteActions |= SWB_ICACHE_FLUSH;

  887              if (*m_pRegionShrDest != g_region_shr)
  888              {
  889:                 ExecutableWriterHolder<UINT8> writeWatchTableImmediateWriterHolder(m_pRegionShrDest, sizeof(UINT8));
  890                  *writeWatchTableImmediateWriterHolder.GetRW() = g_region_shr;
  891                  stompWBCompleteActions |= SWB_ICACHE_FLUSH;

  893              if (*m_pRegionShrSrc != g_region_shr)
  894              {
  895:                 ExecutableWriterHolder<UINT8> writeWatchTableImmediateWriterHolder(m_pRegionShrSrc, sizeof(UINT8));
  896                  *writeWatchTableImmediateWriterHolder.GetRW() = g_region_shr;
  897                  stompWBCompleteActions |= SWB_ICACHE_FLUSH;

  905      if (*(UINT64*)m_pCardTableImmediate != (size_t)g_card_table)
  906      {
  907:          ExecutableWriterHolder<UINT64> cardTableImmediateWriterHolder((UINT64*)m_pCardTableImmediate, sizeof(UINT64));
  908          *cardTableImmediateWriterHolder.GetRW() = (size_t)g_card_table;
  909          stompWBCompleteActions |= SWB_ICACHE_FLUSH;

  913      if (*(UINT64*)m_pCardBundleTableImmediate != (size_t)g_card_bundle_table)
  914      {
  915:          ExecutableWriterHolder<UINT64> cardBundleTableImmediateWriterHolder((UINT64*)m_pCardBundleTableImmediate, sizeof(UINT64));
  916          *cardBundleTableImmediateWriterHolder.GetRW() = (size_t)g_card_bundle_table;
  917          stompWBCompleteActions |= SWB_ICACHE_FLUSH;
```

### `src\coreclr\vm\arm\cgencpu.h`:

`ThisPtrRetBufPrecode::SetTargetInterlocked`
```
  1068          CONTRACTL_END;
  1069
  1070:         ExecutableWriterHolder<ThisPtrRetBufPrecode> precodeWriterHolder(this, sizeof(ThisPtrRetBufPrecode));
  1071          return InterlockedCompareExchange((LONG*)&precodeWriterHolder.GetRW()->m_pTarget, (LONG)target, (LONG)expected) == (LONG)expected;
  1072      }
```

### `src\coreclr\vm\arm\singlestepper.cpp`:

`ArmSingleStepper::Apply`
```
  280      DWORD idxNextInstruction = 0;
  281
  282:     ExecutableWriterHolder<WORD> codeWriterHolder(m_rgCode, kMaxCodeBuffer * sizeof(m_rgCode[0]));
  283
  284      if (m_originalITState.InITBlock() && !ConditionHolds(pCtx, m_originalITState.CurrentCondition()))

```

### `src\coreclr\vm\arm\stubs.cpp`:

`CopyWriteBarrier`
```
  344      size_t size = (PBYTE)end - (PBYTE)src;
  345
  346:     ExecutableWriterHolderNoLog<void> writeBarrierWriterHolder;
  347      if (IsWriteBarrierCopyEnabled())
  348      {

  445          {
  446              to = (PBYTE)PCODEToPINSTR((PCODE)GetWriteBarrierCodeLocation(to));
  447:             ExecutableWriterHolderNoLog<BYTE> barrierWriterHolder;
  448              if (IsWriteBarrierCopyEnabled())
  449              {
```

`UMEntryThunkCode::Poison()`
```
  1740  void UMEntryThunkCode::Poison()
  1741  {
  1742:     ExecutableWriterHolder<UMEntryThunkCode> thunkWriterHolder(this, sizeof(UMEntryThunkCode));
  1743      UMEntryThunkCode *pThisRW = thunkWriterHolder.GetRW();
  1744
```

Defined in `BEGIN_DYNAMIC_HELPER_EMIT` macro and used in many functions on `DynamicHelpers`.
```
  1819      SIZE_T cbAligned = ALIGN_UP(cb, DYNAMIC_HELPER_ALIGNMENT); \
  1820      BYTE * pStartRX = (BYTE *)(void*)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(cbAligned, DYNAMIC_HELPER_ALIGNMENT); \
  1821:     ExecutableWriterHolder<BYTE> startWriterHolder(pStartRX, cbAligned); \
  1822      BYTE * pStart = startWriterHolder.GetRW(); \
  1823      size_t rxOffset = pStartRX - pStart; \
```

`DynamicHelpers::CreateDictionaryLookupHelper` - Called in `prestub.cpp` for `ENCODE_DICTIONARY_LOOKUP_THISOBJ`, `ENCODE_DICTIONARY_LOOKUP_TYPE` and `ENCODE_DICTIONARY_LOOKUP_METHOD`.
```
  2022
  2023      GenericHandleArgs * pArgs = (GenericHandleArgs *)(void *)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(sizeof(GenericHandleArgs), DYNAMIC_HELPER_ALIGNMENT);
  2024:     ExecutableWriterHolder<GenericHandleArgs> argsWriterHolder(pArgs, sizeof(GenericHandleArgs));
  2025      argsWriterHolder.GetRW()->dictionaryIndexAndSlot = dictionaryIndexAndSlot;
  2026      argsWriterHolder.GetRW()->signature = pLookup->signature;
```

### `src\coreclr\vm\arm64\cgencpu.h`:

`ThisPtrRetBufPrecode::SetTargetInterlocked`
```
  611          CONTRACTL_END;
  612
  613:         ExecutableWriterHolder<ThisPtrRetBufPrecode> precodeWriterHolder(this, sizeof(ThisPtrRetBufPrecode));
  614          return (TADDR)InterlockedCompareExchange64(
  615              (LONGLONG*)&precodeWriterHolder.GetRW()->m_pTarget, (TADDR)target, (TADDR)expected) == expected;
```

### `src\coreclr\vm\arm64\singlestepper.cpp`:

`Arm64SingleStepper::Apply`
```
  200      unsigned int idxNextInstruction = 0;
  201
  202:     ExecutableWriterHolder<DWORD> codeWriterHolder(m_rgCode, kMaxCodeBuffer * sizeof(m_rgCode[0]));
  203
  204      if (TryEmulate(pCtx, opcode, false))
```

### `src\coreclr\vm\arm64\stubs.cpp`:

`UpdateWriteBarrierState`
```
  871      BYTE *writeBarrierCodeStart = GetWriteBarrierCodeLocation((void*)JIT_PatchedCodeStart);
  872      BYTE *writeBarrierCodeStartRW = writeBarrierCodeStart;
  873:     ExecutableWriterHolderNoLog<BYTE> writeBarrierWriterHolder;
  874      if (IsWriteBarrierCopyEnabled())
  875      {
```

`UMEntryThunkCode::Poison()` for invalidating thunk.
```
  1041  void UMEntryThunkCode::Poison()
  1042  {
  1043:     ExecutableWriterHolder<UMEntryThunkCode> thunkWriterHolder(this, sizeof(UMEntryThunkCode));
  1044      UMEntryThunkCode *pThisRW = thunkWriterHolder.GetRW();
  1045
```

Defined in `BEGIN_DYNAMIC_HELPER_EMIT` macro and used in many functions on `DynamicHelpers`.
```
  1661      SIZE_T cbAligned = ALIGN_UP(cb, DYNAMIC_HELPER_ALIGNMENT); \
  1662      BYTE * pStartRX = (BYTE *)(void*)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(cbAligned, DYNAMIC_HELPER_ALIGNMENT); \
  1663:     ExecutableWriterHolder<BYTE> startWriterHolder(pStartRX, cbAligned); \
  1664      BYTE * pStart = startWriterHolder.GetRW(); \
  1665      size_t rxOffset = pStartRX - pStart; \
```

`DynamicHelpers::CreateDictionaryLookupHelper` - Called in `prestub.cpp` for `ENCODE_DICTIONARY_LOOKUP_THISOBJ`, `ENCODE_DICTIONARY_LOOKUP_TYPE` and `ENCODE_DICTIONARY_LOOKUP_METHOD`.
```
  1950
  1951      GenericHandleArgs * pArgs = (GenericHandleArgs *)(void *)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(sizeof(GenericHandleArgs), DYNAMIC_HELPER_ALIGNMENT);
  1952:     ExecutableWriterHolder<GenericHandleArgs> argsWriterHolder(pArgs, sizeof(GenericHandleArgs));
  1953      argsWriterHolder.GetRW()->dictionaryIndexAndSlot = dictionaryIndexAndSlot;
  1954      argsWriterHolder.GetRW()->signature = pLookup->signature;
```

### `src\coreclr\vm\i386\cgenx86.cpp`:

`UMEntryThunkCode::Poison()` for invalidating thunk.
```
   999      LIMITED_METHOD_CONTRACT;
  1000
  1001:     ExecutableWriterHolder<UMEntryThunkCode> thunkWriterHolder(this, sizeof(UMEntryThunkCode));
  1002      UMEntryThunkCode *pThisRW = thunkWriterHolder.GetRW();
  1003
```

Defined in `BEGIN_DYNAMIC_HELPER_EMIT` macro and used in many functions on `DynamicHelpers`.
```
  1033      SIZE_T cbAligned = ALIGN_UP(cb, DYNAMIC_HELPER_ALIGNMENT); \
  1034      BYTE * pStartRX = (BYTE *)(void*)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(cbAligned, DYNAMIC_HELPER_ALIGNMENT); \
  1035:     ExecutableWriterHolder<BYTE> startWriterHolder(pStartRX, cbAligned); \
  1036      BYTE * pStart = startWriterHolder.GetRW(); \
  1037      size_t rxOffset = pStartRX - pStart; \
```

`DynamicHelpers::CreateDictionaryLookupHelper` - Called in `prestub.cpp` for `ENCODE_DICTIONARY_LOOKUP_THISOBJ`, `ENCODE_DICTIONARY_LOOKUP_TYPE` and `ENCODE_DICTIONARY_LOOKUP_METHOD`.
```
  1272
  1273      GenericHandleArgs * pArgs = (GenericHandleArgs *)(void *)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(sizeof(GenericHandleArgs), DYNAMIC_HELPER_ALIGNMENT);
  1274:     ExecutableWriterHolder<GenericHandleArgs> argsWriterHolder(pArgs, sizeof(GenericHandleArgs));
  1275      argsWriterHolder.GetRW()->dictionaryIndexAndSlot = dictionaryIndexAndSlot;
  1276      argsWriterHolder.GetRW()->signature = pLookup->signature;
```

### `src\coreclr\vm\i386\jitinterfacex86.cpp`:

Insert write barriers.
```
   860
   861          BYTE * pBufRW = pBuf;
   862:         ExecutableWriterHolderNoLog<BYTE> barrierWriterHolder;
   863          if (IsWriteBarrierCopyEnabled())
   864          {
```

Update write barrier based on ephemeral ranges.
```
  1011
  1012          BYTE * pBufRW = pBuf;
  1013:         ExecutableWriterHolderNoLog<BYTE> barrierWriterHolder;
  1014          if (IsWriteBarrierCopyEnabled())
  1015          {

  1080
  1081          BYTE * pBufRW = pBuf;
  1082:         ExecutableWriterHolderNoLog<BYTE> barrierWriterHolder;
  1083          if (IsWriteBarrierCopyEnabled())
  1084          {
```

### `src\coreclr\vm\i386\stublinkerx86.cpp`:

`ThisPtrRetBufPrecode::SetTargetInterlocked`
```
  3140
  3141      _ASSERTE(IS_ALIGNED(&m_rel32, sizeof(INT32)));
  3142:     ExecutableWriterHolder<INT32> rel32WriterHolder(&m_rel32, sizeof(INT32));
  3143      InterlockedExchange((LONG*)rel32WriterHolder.GetRW(), (LONG)newRel32);
  3144
```

### `src\coreclr\vm\loongarch64\cgencpu.h`:

`ThisPtrRetBufPrecode::SetTargetInterlocked`
```
  527          CONTRACTL_END;
  528
  529:         ExecutableWriterHolder<ThisPtrRetBufPrecode> precodeWriterHolder(this, sizeof(ThisPtrRetBufPrecode));
  530          return (TADDR)InterlockedCompareExchange64(
  531              (LONGLONG*)&precodeWriterHolder.GetRW()->m_pTarget, (TADDR)target, (TADDR)expected) == expected;
```

### `src\coreclr\vm\loongarch64\singlestepper.cpp`:

`LoongArch64SingleStepper::Apply`
```
  198      unsigned int idxNextInstruction = 0;
  199
  200:     ExecutableWriterHolder<DWORD> codeWriterHolder(m_rgCode, kMaxCodeBuffer * sizeof(m_rgCode[0]));
  201
  202      if (TryEmulate(pCtx, opcode, false))
```

### `src\coreclr\vm\loongarch64\stubs.cpp`:

`UpdateWriteBarrierState`
```
  904      BYTE *writeBarrierCodeStart = GetWriteBarrierCodeLocation((void*)JIT_PatchedCodeStart);
  905      BYTE *writeBarrierCodeStartRW = writeBarrierCodeStart;
  906:     ExecutableWriterHolderNoLog<BYTE> writeBarrierWriterHolder;
  907      if (IsWriteBarrierCopyEnabled())
  908      {
```

`UMEntryThunkCode::Poison()` for invalidating thunk.
```
  1072  void UMEntryThunkCode::Poison()
  1073  {
  1074:     ExecutableWriterHolder<UMEntryThunkCode> thunkWriterHolder(this, sizeof(UMEntryThunkCode));
  1075      UMEntryThunkCode *pThisRW = thunkWriterHolder.GetRW();
  1076
```

Defined in `BEGIN_DYNAMIC_HELPER_EMIT` macro and used in many functions on `DynamicHelpers`.
```
  1509      SIZE_T cbAligned = ALIGN_UP(cb, DYNAMIC_HELPER_ALIGNMENT); \
  1510      BYTE * pStartRX = (BYTE *)(void*)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(cbAligned, DYNAMIC_HELPER_ALIGNMENT); \
  1511:     ExecutableWriterHolder<BYTE> startWriterHolder(pStartRX, cbAligned); \
  1512      BYTE * pStart = startWriterHolder.GetRW(); \
  1513      size_t rxOffset = pStartRX - pStart; \
```

`DynamicHelpers::CreateDictionaryLookupHelper` - Called in `prestub.cpp` for `ENCODE_DICTIONARY_LOOKUP_THISOBJ`, `ENCODE_DICTIONARY_LOOKUP_TYPE` and `ENCODE_DICTIONARY_LOOKUP_METHOD`.
```
  1776
  1777      GenericHandleArgs * pArgs = (GenericHandleArgs *)(void *)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(sizeof(GenericHandleArgs), DYNAMIC_HELPER_ALIGNMENT);
  1778:     ExecutableWriterHolder<GenericHandleArgs> argsWriterHolder(pArgs, sizeof(GenericHandleArgs));
  1779      argsWriterHolder.GetRW()->dictionaryIndexAndSlot = dictionaryIndexAndSlot;
  1780      argsWriterHolder.GetRW()->signature = pLookup->signature;
```

### `src\coreclr\vm\riscv64\cgencpu.h`:

`ThisPtrRetBufPrecode::SetTargetInterlocked`
```
  499          CONTRACTL_END;
  500
  501:         ExecutableWriterHolder<ThisPtrRetBufPrecode> precodeWriterHolder(this, sizeof(ThisPtrRetBufPrecode));
  502          return (TADDR)InterlockedCompareExchange64(
  503              (LONGLONG*)&precodeWriterHolder.GetRW()->m_pTarget, (TADDR)target, (TADDR)expected) == expected;
```

### `src\coreclr\vm\riscv64\singlestepper.cpp`:

`RiscV64SingleStepper::Apply`
```
  175      unsigned int idxNextInstruction = 0;
  176
  177:     ExecutableWriterHolder<DWORD> codeWriterHolder(m_rgCode, kMaxCodeBuffer * sizeof(m_rgCode[0]));
  178
  179      if (TryEmulate(pCtx, opcode, false))
```

### `src\coreclr\vm\riscv64\stubs.cpp`:
```
   801      BYTE *writeBarrierCodeStart = GetWriteBarrierCodeLocation((void*)JIT_PatchedCodeStart);
   802      BYTE *writeBarrierCodeStartRW = writeBarrierCodeStart;
   803:     ExecutableWriterHolderNoLog<BYTE> writeBarrierWriterHolder;
   804      if (IsWriteBarrierCopyEnabled())
   805      {
```

`UMEntryThunkCode::Poison()` for invalidating thunk.

`UpdateWriteBarrierState`
```
   970  void UMEntryThunkCode::Poison()
   971  {
   972:     ExecutableWriterHolder<UMEntryThunkCode> thunkWriterHolder(this, sizeof(UMEntryThunkCode));
   973      UMEntryThunkCode *pThisRW = thunkWriterHolder.GetRW();
   974
```

Defined in `BEGIN_DYNAMIC_HELPER_EMIT` macro and used in many functions on `DynamicHelpers`.
```
  1517      SIZE_T cbAligned = ALIGN_UP(cb, DYNAMIC_HELPER_ALIGNMENT); \
  1518      BYTE * pStartRX = (BYTE *)(void*)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(cbAligned, DYNAMIC_HELPER_ALIGNMENT); \
  1519:     ExecutableWriterHolder<BYTE> startWriterHolder(pStartRX, cbAligned); \
  1520      BYTE * pStart = startWriterHolder.GetRW(); \
  1521      size_t rxOffset = pStartRX - pStart; \
```

`DynamicHelpers::CreateDictionaryLookupHelper` - Called in `prestub.cpp` for `ENCODE_DICTIONARY_LOOKUP_THISOBJ`, `ENCODE_DICTIONARY_LOOKUP_TYPE` and `ENCODE_DICTIONARY_LOOKUP_METHOD`.
```
  1799
  1800      GenericHandleArgs * pArgs = (GenericHandleArgs *)(void *)pAllocator->GetDynamicHelpersHeap()->AllocAlignedMem(sizeof(GenericHandleArgs), DYNAMIC_HELPER_ALIGNMENT);
  1801:     ExecutableWriterHolder<GenericHandleArgs> argsWriterHolder(pArgs, sizeof(GenericHandleArgs));
  1802      argsWriterHolder.GetRW()->dictionaryIndexAndSlot = dictionaryIndexAndSlot;
  1803      argsWriterHolder.GetRW()->signature = pLookup->signature;
```