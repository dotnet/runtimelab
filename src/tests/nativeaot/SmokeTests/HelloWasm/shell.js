// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Allows testing of Wasm imports from a named module

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
            ModuleFunc: functionInModule,
        };

        // Same function in different modules
        info['ModuleName1'] = {
            CommonFunctionName: functionInModule,
        };
        info['ModuleName2'] = {
            CommonFunctionName: functionInModule2,
        };

        instantiateAsync(wasmBinary, wasmBinaryFile, info, receiveInstantiationResult);
        return {};
    },
};

function functionInModule(p) {
    return p;
}

// Do something different to functionInModule so we can test we have called the correct function.
function functionInModule2(p) {
    return p + 1;
}
