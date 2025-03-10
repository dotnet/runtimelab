# Debugger integration with the interpreter
## Debugger to interpreter calls
### Mono
The interpreter exposes a subset of functions implemented in the interp.c as an interface that Mono runtime calls into. The functions listed below are called by the debugger related code. 
* interp_set_resume_state
* interp_get_resume_state
* interp_set_breakpoint
* interp_clear_breakpoint
* interp_frame_get_jit_info
* interp_frame_get_ip - dtto
* interp_frame_get_local
* interp_frame_get_this
* interp_frame_get_arg
* interp_start_single_stepping
* interp_stop_single_stepping

Mono supports debugger connection via the ICorDebug interface. The calls to that interface are translated to Mono debugging protocol that delivers messages to the debuggee side and calls some of the functions listed above to handle the specific operations. 
The interp_set_resume_state and interp_get_resume_state don't seem to be used in the ICorDebug related code paths.
The interp_frame_get_jit_info and interp_frame_get_ip seem to be used during single step / breakpoint processing, but it is not clear how they precisely relate to the ICorDebug stuff.

Here is how relevant ICorDebug interface methods are wired to the interpreter functions:
* CordbJITILFrame::GetLocalVariable -> interp_frame_get_local
* CordbJITILFrame::GetArgument(0) -> interp_frame_get_this
* CordbJITILFrame::GetArgument(1..n) -> interp_frame_get_arg
* CordbFunctionBreakpoint::Activate(true) -> interp_set_breakpoint
* CordbFunctionBreakpoint::Activate(false) -> interp_clear_breakpoint
* CordbStepper::Step, StepRange, StepOut -> interp_start_single_stepping
* CordbStepper::Deactivate -> interp_stop_single_stepping

### CoreCLR
CoreCLR also uses the ICorDebug interface for debugger connection. Most of the calls to that interface are translated to IPC events and the debuggee side handles the events in Debugger::HandleIPCEvent. So we can handle the events stemming from some of the above mentioned ICorDebug interface methods there by calling the same interpreter functions that Mono calls. 
The CordbJITILFrame methods don't send IPC events though and rather uses DAC and remote memory access to get the variable and argument data. It ends up getting the variable locations using the IJitManager::GetBoundariesAndVars method. We would have a JIT manager for the interpreted code and the implementation of this method could use the DAC-ified interp_frame_get_local, interp_frame_get_this and interp_frame_get_arg to get the actual details.

## Interpreter to debugger calls
### Mono
There are just two "events" that the interpreter notifies the debugger about:
* Single stepping: when MINT_SDB_INTR_LOC IR opcode is executed, a trampoline obtained from mini_get_single_step_trampoline() is called. This trampoline ends up calling mono_component_debugger()->single_step_from_context. That in turn end up calling CordbProcess()->GetCallback()->StepComplete
* Breakpoint hit: when MINT_SDB_BREAKPOINT IR opcode is executed a trampoline obtained from mini_get_breakpoint_trampoline() is called. This trampoline ends up calling mono_component_debugger()->breakpoint_from_context. That in turn end up calling CordbProcess()->GetCallback()->Breakpoint
* System.Diagnostics.Debugger.Break(): when MINT_BREAK IR opcode is executed, a trampoline obtained from mono_component_debugger()->user_break is called. That in turn end up calling CordbProcess()->GetCallback()->Break
### CoreCLR
* Single stepping: when MINT_SDB_INTR_LOC IR opcode is executed, Debugger::SendStep will be called. That sends DB_IPCE_STEP_COMPLETE event and the debugger processes it by ICorDebugManagedCallback::StepComplete)
* Breakpoint hit: when MINT_SDB_BREAKPOINT IR opcode is executed, Debugger::SendBreakpoint will be called (that sends DB_IPCE_BREAKPOINT and the debugger processes it by calling ICorDebugManagedCallback::Breakpoint)
* System.Diagnostics.Debugger.Break(): when MINT_BREAK IR opcode is executed, Debugger::SendRawUserBreakpoint will be called (that sends DB_IPCE_USER_BREAKPOINT and the debugger processes it by calling ICorDebugManagedCallback::Break)

## WASM debugger event loop
The Mono debugger runs a debugger event loop that receives commands from the debugger in a separate thread. Since WASM is a single threaded environment, the debugger has to use a different mechanism. The Mono interpreter calls mono_component_debugger()->receive_and_process_command_from_debugger_agent() for each MINT_SDB_SEQ_POINT it interprets. 