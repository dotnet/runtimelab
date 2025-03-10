# CoreCLR without runtime code generation

## Initial plan
The goal of this experiment was to figure out a way how to make CoreCLR work without any code generation at runtime. The initial plan was to:
* Use the existing CoreCLR interpreter as a driver
* Identify problematic features and figure out fixes for those
* Implement the fixes
* Test functionality and run some micro benchmarks to assess performance implications of the changes

The expected outcome was:
* CoreCLR can work reasonably well without runtime code generation
* Document limitations if any

## Progress
Soon after the work on the experiment begun, it became obvious that the CoreCLR interpreter is in far worse state than it was expected. Just making it execute a hello world .NET application fully interpreted took almost three weeks. 

We have also audited all the places where executable code is generated. The ExecutableWriterHolder that was created during the W^X feature development in the past is used at all such places, so that allowed us to easily find out the locations. Then we have analyzed each of them and documented how we plan to deal with them. Some of them can be ignored because they would not be used in the target scenarios (like COM related stuff, JIT only stuff, C++/CLI, ...). 

We have then focused on figuring out how to handle code pointers that point to interpreted code like function pointers or delegates. A new precode type to represent the interpreted code entry point was implemented for this purpose.

We have also investigated how interpreted methods could be called directly from the native runtime via the CallDescrWorker without using the interpreter stub. We have evaluated a couple of approaches and found one that is usable.

Finally, we have looked into how calling interpreted methods from ReadyToRun code could work, focusing on CoreCLR specific low level details.

## Results
As a result of the experiment, some code changes were made and documents written. Due to the problematic state of the existing CoreCLR interpreter that was discovered during the experiment, we didn't reach many of the goals we have set. Given the time we had for the experiment, we were not able to look into details on how to get rid of various runtime generated stubs and other executable code generation at runtime. With the exception of the new precode implementation, the fixes that we've implemented were just the ones necessary to make hello world run fully interpreted. So we also couldn't do any performance testing.

* Code changes
  * Fix problems preventing fully interpreted execution of a hello world app
  * Experimentally implement new Precode to represent interpreted code
* Documents containing details on the investigated subjects 
  * [Status](experiment_status.md)
  * [Calling interpreted methods from native runtime](TaggedFunctionPrototype.md)
  * [Calling interpreted methods from ReadyToRun code](ReadyToInterpret.md)
