# Stack walking for GC, exception handling and debugger
To enable seamless integration of interpreter with GC, exception handling and debugger, we need to be able to walk stack that contains interleaved sequences of interpreted and AOT / JIT compiled frames. Fortunately, the same StackFrameIterator is used by the GC, exception handling and debugger, so adding a support to walk stack containing interpreter frames to this StackFrameIterator will give us support for all of the three scenarios.

Here is the list of necessary changes:
* Add a new explicit frame, the for the sake of this document named ClrInterpreterFrame. Instance of this frame would be created in the mono_interp_exec_method. It will enable stack frame iterator to move through the interpreted frames managed by that mono_interp_exec_method. Please note that interpreter calls to other interpreted code are not recursively calling mono_interp_exec_method and each interpreted frame is represented by an instance of Mono InterpFrame (these form a linked list).
* Add a new code manager derived from IJitManager, the InterpreterJitManager that will take care of the IR code ranges and enable getting GC info, enumerating EH clauses etc.
* Add RangeSectionFlags::RANGE_SECTION_INTERPRETER to represent interpreter IR code ranges. The RangeSection::_pjit would point to the InterpreterJitManager for the ranges in the interpreted code. So EECodeInfo::Init with an address in the interpreted IR code would get the InterpreterJitManager.
* It may need some changes in the EECodeInfo too
* Add the current Mono InterpFrame (the name will likely change) to the CrawlFrame. When stack walk hits the ClrInterpreterFrame, it would set that to the first Mono InterpFrame
* Modify the EECodeManager::UnwindStackFrame to walk the list of Mono InterpFrame instances for interpreted frames belonging to single mono_interp_exec_method. The Mono interpreter functions interp_frame_iter_next performs the stack walking for interpreted frames. It will likely need to be modified to be better suited for CoreCLR needs.

The interpreter frames would be reported as StackFrameIterator::SFITER_FRAMELESS_METHOD like AOT / JIT compiled ones.
The REGDISPLAY::ControlPC would carry the PC of the interpreter IR code, the REGDISPLAY::SP the Mono InterpFrame instance address.

## GC
* The GC reporting should mostly just work using the GC info provided by the InterpreterJitManager. The offsets of the GC references would be relative to the interpreter stack that the Mono InterpFrame::stack points to.

## Exception handling
* It seems no or minimal changes will be needed, all the details related to the interpreter frame should be hidden behind the InterpreterJitManager

## Debugger
* Add FrameType::kInterpreterStackFrame. The debugger code needs to know which frames represent interpreted code so that it can call proper implementations of functions to set a breakpoint, single step, get local variables etc. in the interpreter.
* Modify DacDbiInterfaceImpl::GetStackWalkCurrentFrameInfo to figure the frame type is kInterpreterStackFrame
* Modify DacDbiInterfaceImpl::InitFrameData to handle that frame type. The handling of this frame type will be likely very similar to the handling of the FrameType::kManagedStackFrame
* For more details, see the [debugger](debugger.md) document.
