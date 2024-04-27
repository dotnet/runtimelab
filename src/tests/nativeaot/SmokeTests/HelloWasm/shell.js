// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Allows testing of Wasm imports from a named module

var wasmBinaryFile = 'HelloWasm.wasm';

var Module = {
    'instantiateWasm': function (info, successCallback) {

        // Provide the import from a module named "ModuleName".
        info['ModuleName'] = {
            ModuleFunc: functionInModule,
        };

        // Same function in different modules
        info['ModuleName1'] = {
            CommonFunctionName: functionInModule,
            CommonWasmImportFunctionName: functionInModule,
        };
        info['ModuleName2'] = {
            CommonFunctionName: functionInModule2,
        };

        // Check that we have the right imports after having instantiated the module.
        function receiveInstantiationResult(result) {
            const wasmImports = WebAssembly.Module.imports(result['module']);
            const dupImport = (element) => element.module == 'ModuleName' && element.name == 'DupImportTest' && element.kind == 'function';
            if (wasmImports.some(dupImport)) {
                throw 'DupImportTest was imported when it should have been removed as a duplicate.';
            }

            return successCallback(result['instance']);
        }

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
