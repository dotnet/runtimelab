if (_nativeModuleLoaded) throw new Error("Native module already loaded");
_nativeModuleLoaded = true;
<<<<<<< HEAD
// see https://github.com/emscripten-core/emscripten/issues/19832
Module["getMemory"] = function () { return wasmMemory; }
moduleArg = Module = moduleArg(Module);
Module["getMemory"] = function () { return wasmMemory; }
=======
createDotnetRuntime = Module = moduleArg(Module);
>>>>>>> runtime/main
