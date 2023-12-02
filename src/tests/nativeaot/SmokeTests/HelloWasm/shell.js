// No copyright header as this is largely a cut and paste from the standard
// Emscripten produced shell

// Allows the importing of symbols from a named module

var wasmBinaryFile = 'HelloWasm.wasm';

//// Load the wasm module and create an instance of using native support in the JS engine.
//// handle a generated wasm instance, receiving its exports and
//// performing other necessary setup
function receiveInstantiationResult(result) {
    wasmExports = result['instance'].exports;

    const wasmImports = WebAssembly.Module.imports(result['module']);
    const dupImport = (element) => element.module == 'ModuleName' && element.name == 'DupImportTest' && element.kind == 'function';
    if (wasmImports.some(dupImport)) {
        throw 'DupImportTest was imported when it should have been removed as a duplicate.';
    }

    wasmMemory = wasmExports['memory'];

    updateMemoryViews();

    addOnInit(wasmExports['__wasm_call_ctors']);

    removeRunDependency('wasm-instantiate');
    return wasmExports;
}

var Module = {
    'instantiateWasm': function (info, successCallback) {

        // Provide the import from a module named "ModuleName".
        info['ModuleName'] = {
            FunctionInModule: functionInModule
        };

        instantiateAsync(wasmBinary, wasmBinaryFile, info, receiveInstantiationResult);
        return {};
    },
};

function functionInModule(p) {
    return p;
}
