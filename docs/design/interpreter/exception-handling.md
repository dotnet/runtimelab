# CoreCLR exception handling integration with the interpreter
There are several parts of the exception handling that need to take into consideration presence of interpreter frames on the call stack. 
* Stack walking
* Exception clauses enumeration
* Exception handlers invocation
* Resuming execution after a catch handler exits

The stack walking changes are described in the [stack walking](stackwalk.md) document.
Exception clauses enumeration in CoreCLR is agnostic of the actual low level details of EH clauses information storage. It uses an interface to a JIT manager that does the real work. As described in the stack walking document, a new InterpreterJitManager will be implemented for the interpreted code.
To enumerate the EH clauses, it will use data provided by the InterpMethod::clauses/num_clauses.

Regarding the exception handlers invocation, CoreCLR uses native helpers CallCatchFunclet, CallFinallyFunclet and CallFilterFunclet. These will need to be modified to recognize interpreted frames and call the related interpreter functions. CallFinallyFunclet would call interp_run_finally and CallFilterFunclet would call the interp_run_filter. As for the CallCatchFunclet, Mono handles calling catch funclets by calling interp_set_resume_state to record the catch handler IR code address and then returning to the mono_interp_exec_method that checks for this stored state and redirects execution to the catch handler if it is set. In CoreCLR, we may want to handle it differently, in a manner close to how interp_run_finally works.

Resuming execution in the parent of a catch handler after the catch handler exits depends on whether the catch handler is in an interpreted code or in compiled managed code. 
For the compiled code case, the existing CoreCLR EH code will handle it without changes. It would just restore the context at the resume location.
For the interpreted code though, CoreCLR will resume execution in the mono_interp_exec_method, then pop some InterpFrame instances from the local linked list (depending on how many interpreted managed frames need to be removed) and restore the interpreter SP from the last popped one and the interpreter IP will be set to the resume location IP.

WASM doesn't support stack unwinding and context manipulation, so the resuming mechanism cannot use context restoring. Mono currently returns from the mono_handle_exception after the catch is executed back to the interp_throw. When the resume frame is in the interpreted frames belonging to the current mono_interp_exec_method, it uses the mechanism described in the previous paragraph to "restore" to the resume context. But when the resume frame is above all the frames belonging to the current mono_interp_exec_method, it exits from the mono_interp_exec_method and then throws a C++ exception (of int32 * type set to NULL) that will propagate through native frames until it is caught in a compiled managed code or in another interpreter function up the call chain (see usage of mono_llvm_catch_exception) where the propagation through the interpreted frames continues the same way as in the previous interpreted frames block.
