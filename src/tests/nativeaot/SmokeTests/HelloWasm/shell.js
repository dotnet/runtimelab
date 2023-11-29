// No copyright header as this is largely a cut and paste from the standard
// Emscripten produced shell

// Allows the importing of symbols from a named module

var wasmBinaryFile = 'HelloWasm.wasm';

function instantiateArrayBuffer(binaryFile, imports, receiver) {
    var savedBinary;
    return getBinaryPromise(binaryFile).then((binary) => {
        savedBinary = binary;
        return WebAssembly.instantiate(binary, imports);
    }).then((instance) => {
        // wasmOffsetConverter needs to be assigned before calling the receiver
        // (receiveInstantiationResult).  See comments below in instantiateAsync.
        wasmOffsetConverter = new WasmOffsetConverter(savedBinary, instance.module);
        return instance;
    }).then(receiver, (reason) => {
        err(`failed to asynchronously prepare wasm: ${reason}`);

        // Warn on some common problems.
        if (isFileURI(wasmBinaryFile)) {
            err(`warning: Loading from a file URI (${wasmBinaryFile}) is not supported in most browsers. See https://emscripten.org/docs/getting_started/FAQ.html#how-do-i-run-a-local-webserver-for-testing-why-does-my-program-stall-in-downloading-or-preparing`);
        }
        abort(reason);
    });
}

function instantiateAsync(binaryFile, imports, callback) {
    if (typeof WebAssembly.instantiateStreaming == 'function' &&
        !isDataURI(binaryFile) &&
        // Don't use streaming for file:// delivered objects in a webview, fetch them synchronously.
        !isFileURI(binaryFile) &&
        // Avoid instantiateStreaming() on Node.js environment for now, as while
        // Node.js v18.1.0 implements it, it does not have a full fetch()
        // implementation yet.
        //
        // Reference:
        //   https://github.com/emscripten-core/emscripten/pull/16917
        !ENVIRONMENT_IS_NODE &&
        typeof fetch == 'function') {
        return fetch(binaryFile, { credentials: 'same-origin' }).then((response) => {
            // Suppress closure warning here since the upstream definition for
            // instantiateStreaming only allows Promise<Repsponse> rather than
            // an actual Response.
            // TODO(https://github.com/google/closure-compiler/pull/3913): Remove if/when upstream closure is fixed.
            /** @suppress {checkTypes} */
            var result = WebAssembly.instantiateStreaming(response, imports);

            // We need the wasm binary for the offset converter. Clone the response
            // in order to get its arrayBuffer (cloning should be more efficient
            // than doing another entire request).
            // (We must clone the response now in order to use it later, as if we
            // try to clone it asynchronously lower down then we will get a
            // "response was already consumed" error.)
            var clonedResponsePromise = response.clone().arrayBuffer();

            return result.then(
                function (instantiationResult) {
                    // When using the offset converter, we must interpose here. First,
                    // the instantiation result must arrive (if it fails, the error
                    // handling later down will handle it). Once it arrives, we can
                    // initialize the offset converter. And only then is it valid to
                    // call receiveInstantiationResult, as that function will use the
                    // offset converter (in the case of pthreads, it will create the
                    // pthreads and send them the offsets along with the wasm instance).

                    clonedResponsePromise.then((arrayBufferResult) => {
                        wasmOffsetConverter = new WasmOffsetConverter(new Uint8Array(arrayBufferResult), instantiationResult.module);
                        callback(instantiationResult);
                    },
                        (reason) => err(`failed to initialize offset-converter: ${reason}`)
                    );
                },
                function (reason) {
                    // We expect the most common failure cause to be a bad MIME type for the binary,
                    // in which case falling back to ArrayBuffer instantiation should work.
                    err(`wasm streaming compile failed: ${reason}`);
                    err('falling back to ArrayBuffer instantiation');
                    return instantiateArrayBuffer(binaryFile, imports, callback);
                });
        });
    }
    return instantiateArrayBuffer(binaryFile, imports, callback);
}

// Load the wasm module and create an instance of using native support in the JS engine.
// handle a generated wasm instance, receiving its exports and
// performing other necessary setup
/** @param {WebAssembly.Module=} module*/
function receiveInstance(instance, module) {
    wasmExports = instance.exports;

    wasmMemory = wasmExports['memory'];

    assert(wasmMemory, "memory not found in wasm exports");
    // This assertion doesn't hold when emscripten is run in --post-link
    // mode.
    // TODO(sbc): Read INITIAL_MEMORY out of the wasm file in post-link mode.
    //assert(wasmMemory.buffer.byteLength === 16777216);
    updateMemoryViews();

    addOnInit(wasmExports['__wasm_call_ctors']);

    removeRunDependency('wasm-instantiate');
    return wasmExports;
}

function receiveInstantiationResult(result) {
    // TODO: Due to Closure regression https://github.com/google/closure-compiler/issues/3193, the above line no longer optimizes out down to the following line.
    // When the regression is fixed, can restore the above PTHREADS-enabled path.
    receiveInstance(result['instance']);
}

var Module = {
    'instantiateWasm': function (info, successCallback) {

        // Provide the import from a module named "ModuleName".
        info['ModuleName'] = {
            FunctionInModule: functionInModule
        };

        instantiateAsync(wasmBinary, wasmBinaryFile, info, receiveInstantiationResult);
        return {}; // no exports yet; we'll fill them in later
    },
};

function functionInModule(p) {
    return p;
}
