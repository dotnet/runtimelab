# Exception handling for WebAssembly in NativeAOT

WebAssembly in NativeAOT has a bespoke implementation of exception handling due to the platform's lack of a way to enumerate the currently active stack frames and their contents non-destructively. This document is intended to provide a detailed overview for the internals of this implementation, as well the design reasons behind them.

## Goals

* Correctness. The implementation must fully conform to the CLI semantics of two-pass handling.
* Size. WebAssembly is a very size-conscious target, therefore, optimizing for the size of code and supporting data is a priority.
* Minimal execution overhead. Non-exceptional paths in a program should be minimally affected.
* Flexibility. The implementation must be reasonably agnostic of the underlying unwind scheme, as multiple must be supported.

## Constraints

As has been noted above, WebAssembly lacks what is commonly known as "virtual unwinding", i. e. the ability to walk the current stack of functions in a way that would not affect it. It is possible, however, to unwind, destructively, by throwing and catching JavaScript or native WebAssembly exceptions.

Recall then that the CLI exception handling requires a two-pass algorithm, with the first pass virtually unwinding the stack and calling filters to determine where should the thrown exception be handled, and the second pass unwinding this stack up to the point of the found catch, running fault handlers. Crucially, handlers during the second pass can throw exceptions of their own, which effectively restarts the process. When such nested exceptions occur, they can replace the originals, and be caught at any point in the stack, even below what would have been the catching frame of the original exception. Consider:

```csharp
try
{
    try
    {
        try
        {
            throw new IndexOutOfRangeException();
        }
        finally
        {
            throw new ArgumentOutOfRangeException();
        }
    }
    catch (ArgumentOutOfRangeException)
    {
        // After the first throw, control will eventually reach here.
    }
}
catch (IndexOutOfRangeException)
{
}
```
This means that destructive unwinding cannot be used to implement the first pass, as control must be able to return to an arbitary frame on the native stack in the second pass.

From the above, the basic idea for this implementation is as follows: manually maintain a "virtual unwind stack" of currently active protected regions, to be used by the first pass, and utilize native unwinding for the second.

## The virtual unwind stack

From the above, the virtual unwind stack has the primary purpose of being an accurate representation of the currently active protected regions. Note that for this we only need to explicitly describe regions protected by catch handlers and can skip faults. This turns out to be a rather important optimization as about 60% of handlers are faults (or finallys, which behave identically to faults dispatch-wise).

This stack must also have a way to obtain other data needed for dispatch: the nesting information and shadow frames on which filters should be called. It must also be reasonably cheap to update the "current" state of a method as control travels through it across different protected regions.

All this is achieved with a linked list of the following on-shadow-stack data structures maintained by codegen and exception handling dispatch infrastructure, with its head stored in a thread-static:
```cs
struct VirtualUnwindFrame
{
    VirtualUnwindFrame* Prev;
    void* UnwindTable;
    nuint UnwindIndex;
}
```
These frames are allocated on the shadow stack at a zero offset, which allows them to be passed as-is to filters, and linked into the thread-local chain on method entry. Throughout method execution, `UnwindIndex` is maintained by codegen to remain in sync with the innermost active protected region. Finally, `UnwindTable` contains the means to translate this `UnwindIndex` to concrete dispatch-relevant information such as clause types, filter addresses and enclosed regions.

To better understand how virtual unwind frames are constructed, consider the following example:
```cs
void MethodWithEH()
{
    MayThrow();

    try
    {
        try // T1
        {
            MayThrow();

            try // T0
            {
                MayThrow();
            }
            catch (Exception)
            {
                MayThrow();
            }

            MayThrow();
        }
        catch
        {
            MayThrow();
        }
    }
    fault
    {
        MayThrow();
    }

    try // T2
    {
        MayThrow();
    }
    catch when (true) { }

    MayThrow();
}
```
We have three regions protected by catches, one nested inside another, there is a fault and calls outside any protected region. Our logical unwind table will be as follows:
```
Index | Catch type / filter | Enclosing region |
T0    | System.Exception    | T1               |
T1    | System.Object       | NOT_IN_TRY_CATCH |
T2    | (...) => true       | NOT_IN_TRY       |
```
Note the two special sentinel values for regions not enclosed within another: `NOT_IN_TRY` describes a state where control is outside any protected region, including those with faults, while `NOT_IN_TRY_CATCH` describes a state where control is outside a region protected by a catch handler, effectively inside a protected region of a top-level fault. The two need to be differentiated in order for second-pass unwinding to know which frames can be safely unlinked: control will never return to a `NOT_IN_TRY` frame, while it always will to any other.

With that said, here is one way codegen could maintain the unwind index:
```cs
void MethodWithEH()
{
    VirtualUnwindFrame frame;
    RhpPushFrame(&frame, <UnwindTable>, NOT_IN_TRY);

    frame.UnwindIndex = NOT_IN_TRY;
    MayThrow();

    try
    {
        try // T1
        {
            frame.UnwindIndex = T1;
            MayThrow();

            try // T0
            {
                frame.UnwindIndex = T0;
                MayThrow();
            }
            catch (Exception)
            {
                frame.UnwindIndex = T1;
                MayThrow();
            }

            frame.UnwindIndex = T1;
            MayThrow();
        }
        catch
        {
            frame.UnwindIndex = NOT_IN_TRY_CATCH;
            MayThrow();
        }
    }
    fault
    {
        frame.UnwindIndex = NOT_IN_TRY;
        MayThrow();
    }

    try // T2
    {
        frame.UnwindIndex = NOT_IN_TRY;
        MayThrow();
    }
    catch when (true) { }

    frame.UnwindIndex = NOT_IN_TRY;
    MayThrow();

    RhpPopFrame();
}
```
This is correct, as each potential throwing call has the index defined right before it, but the actually used strategy is a bit more sophisticated and tries to avoid redundant definitions.

Notice as well the `RhpPushFrame` and `RhpPopFrame` helper calls - these link and unlink the frame from the chain, ensuring the stack is balanced in non-exceptional flow cases.

## Unwinding and the second-pass algorithm

The second pass presents three problems:

- The place to store information associated with a given dispatch. Note how this data includes the exception itself, which must be visible as live to the GC before control reaches the catch handler.
- Virtual unwind stack maintainance. As control travels up the stack and enters fault handlers, frames corresponding to those natively unwound must be unlinked.
- Abandonment. As we have seen above, exceptions can "replace" those thrown up the call stack, and this must be handled correctly.

For the storage location of the dispatch information, we choose managed thread-static storage, mainly by method of exclusion:
- We need something thread-local.
- Thus, it is either the shadow stack, native thread-local storage or managed thread-local storage.
- The shadow stack is unwound as the second pass progresses, and multiple exceptions can target the same catching frame, so it is difficult to make it work well in this case.
- Between native and managed TLS, we need GC reporting, so we choose managed. This is reinforced by the desire to have dispatch code be managed.

Virtual unwind stack maintainance is trickier. We observe the following:
- `NOT_IN_TRY` frames must be unlinked "in advance" as control will not be reach their native counterparts.
- `NOT_IN_TRY_CATCH` frames **must not** be unlinked as the faults they transfer control to may yet use the unwind index, as in the following example:
```cs
try
{
    ...
}
fault // We are unwinding into this handler
{
    try
    {
        // Virtual unwind frame still in use here.
        frame.UnwindIndex = T0;
        ...
    }
    catch { }
}
```
- Frames representing catches past which we will unwind (because they did not satisfy the first pass) must also unlink their frames as necessary.

Considering all of the above, here are the exact points at which frames must be unlinked:
- When throwing an exception (`NOT_IN_TRY` ones only).
- On exit from a fault handler that is top-level - such that no upstream handler in the same frame will receive control and access that frame. Note, of course, that we needn't unlink anything if there was nothing to unlink to begin with, and so frames with fault handlers but without catch handlers don't need to be considered here.
- When unwinding past a top-level catch handler.

For the fault case, we insert a helper call in codegen that will unlink the currently active frame as well as all `NOT_IN_TRY` ones after the handler exits. For the catch case, we do the same in the corresponding helper, inserted at the beginning of each catch:
```cs
catch (exception)
{
    UserCode(exception);
}

==>

catch
{
    object exception = RhpWasmCatch(<unwind index of the corresponding protected region>)
    if (exception == null)
        <continue unwinding by e. g. rethrowing the native exception>
    UserCode(exception);
}
```
Note how in the catch case, all of the code to maintain the stack is folded into the helper call. This helps to keep the code size impact minimal.

Finally, the last major part of the dispatch algorithm and another user of the virtual unwind stack is abandonment detection. First, let's define what an "abandoned" exception is: it is one that will not reach its designated catch handler. Exceptions can become abandoned due to nested dispatch, when a nested exception escapes a fault handler triggered by the original:
```
[try                             ][catch C0] ; Will catch E0
...
[try                   ][catch C1]           ; Would have caught E1
...
[try     ][active fault]                     ; Triggered by E1
...
 ^         ^
 |         |
 |         |
 |        [throw E0]                         ; Will cause the abandonment of E1
 |
[throw E1]
```
A given nested exception can cause abandonment of a dynamically determined number of prior exceptions via, for example, filters that change their values based on some non-static criteria, so we cannot know at the time of the first pass' end whether any given exception will be abandoned and must detect it in the second pass. The example above has the nested exception escape not just the fault, but unwind past the original's catch handler, however, in the general case, the nested exception's catch be below or exactly the same as that of the original:
```
[try                             ][catch C1] ; Would have caught E1
...
[try                   ][catch C0]           ; Will catch E0
...
[try     ][active fault]                     ; Triggered by E1
...
 ^         ^
 |         |
 |         |
 |        [throw E0]                         ; Will cause the abandonment of E1
 |
[throw E1]
```
Indeed, even in the first example, the nested exception can itself be abandoned mid-flight via another nested throw.

To correctly handle all of this, we must know when to unlink a given exception from the thread-local list of active ones. It turns out we can do so at the very end of the second pass, before transferring control to the catch handler. Consider that for an exception to not cause abandonment, its catch **must** lie below that of its predecessor's next one and that the oppossite is, crucially, also true:
```
; Case 1: no abandonment
; If C0 lies below C1, then it must lie below F1, as otherwise C0 would have been the next catch for C1 due to the clause nesting rules
[try                     ][C1, E1's next catch]
[try][F1, E1's fault     ]
     [try][C0, E0's catch]

; Case 2: abandonment (same catch)
; Since F1 must lie below C1, it must have been unwound past by E0
[try                     ][C1, E1's next catch and E0's last catch]
[try][F1, E1's fault     ]

; Case 3: abandonment (upstream catch)
; Same as above
[try                                          ][C0, E0's catch]
[try                     ][C1, E1's next catch]
[try][F1, E1's fault     ]
```
With this in mind, we need two things to determine which exceptions should go abandoned:
1) The next catch that will be unwound to by an exception. This can be kept up-to-date by the catch helper mentioned above, using the virtual unwind stack which provides exactly this information.
2) Means to compare two "unwind positions". This can be achived by storing those positions as virtual unwind frame pointers plus unwind indices. Since the frames are all allocated on the shadow stack, which has a known growth direction, and the unwind indices are constructed such that enclosed regions come before enclosing ones, the relation can be ascertained using simple comparisons.

Combining all of the above, we have a fully general exception handling algorithm with CLI-compatible semantics.

## Addenum: codegen implications

The algorithm described above carries with it a positive implication for the LLVM-based code generator: there is no need to treat locals live into handlers specially, since control will only be transferred to them during the second pass, when the stack below has already been unwound. Only filters need special handling as they are called by the first pass while live state still exists above them. This also means that only filters need to be funclets for correctness reasons and all other handlers can be part of the main method body (although finallys do present some challenges due to their multi-entry nature).

In this way, the WASM exception handling model is unique in that it is neither truly funclet-based, nor x86-like. Still, the current implementation does define `FEATURE_EH_FUNCLETS`, to hide this detail from the rest of the compiler.
