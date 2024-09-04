
var DotNetSupportLib = {
    $DOTNET: {
        _dotnet_get_global: function () {
            return globalThis;
        },
    },
    corert_wasm_invoke_js_unmarshalled: function (js, length, arg0, arg1, arg2, exception) {

        var jsFuncName = UTF8ToString(js, length);
        var dotNetExports = DOTNET._dotnet_get_global().DotNet;
        if (!dotNetExports) {
            throw new Error('The Microsoft.JSInterop.js library is not loaded.');
        }
        var funcInstance = dotNetExports.jsCallDispatcher.findJSFunction(jsFuncName);

        return funcInstance.call(null, arg0, arg1, arg2);
    },
    TestForeignModuleInStackTraceJS__deps: ['$wasmTable'],
    TestForeignModuleInStackTraceJS: function (pReason, pCallee) {
        // We force an index space collision with the managed 'pCallee'
        // by padding the index space with empty functions.
        const pCalleeFunc = wasmTable.get(pCallee);
        const pCalleeFuncIndex = parseInt(pCalleeFunc.name);

        const encodeULeb128 = (bytes, val) =>
        {
            do
            {
                let byteVal = val & 0x7F;
                val >>>= 7;
                if (val) byteVal |= 0x80;
                bytes.push(byteVal);
            }
            while (val)
        };
        const appendSection = (bytes, header, sectionBytes) =>
        {
            bytes.push(header);
            encodeULeb128(bytes, sectionBytes.length);
            for (const elem of sectionBytes) bytes.push(elem);
        };

        let bytes = [
            0x00, 0x61, 0x73, 0x6d, // magic ("\0asm")
            0x01, 0x00, 0x00, 0x00, // version: 1
            0x01, 0x05, // Type section header
            0x01, 0x60, 0x01, 0x7C, 0x00, // [functype([f64], [])]
        ];

        // Import section: pad the function index space with dummy imports.
        const exportedFuncIndex = pCalleeFuncIndex;
        const importFuncCount = exportedFuncIndex;
        const importSectionBytes = [];
        encodeULeb128(importSectionBytes, importFuncCount);
        for (let i = 0; i < importFuncCount; i++)
        {
            importSectionBytes.push(0x01, 0x6D, 0x01, 0x6E, 0x00, 0x00); // import<module: m, name: n, typeidx: 0>
        }
        appendSection(bytes, 0x02, importSectionBytes);

        // Function section.
        bytes.push(
            0x03, 0x02, // Function section header
            0x01, 0x00, // [typeidx: 0]
        );

        // Export section.
        const exportSectionBytes = [0x01, 0x01, 0x65, 0x00]; // export<name: "e", funcidx: exportedFuncIndex>
        encodeULeb128(exportSectionBytes, exportedFuncIndex);
        appendSection(bytes, 0x07, exportSectionBytes);

        bytes.push(
            0x0A, 0x08,       // Code section header
            0x01, 0x06, 0x00, // [Function<code: 6 bytes, locals: 0>]
            0x20, 0x00,       // local.get 0
            0x10, 0x00,       // call <funcidx: 0>
            0x0B,             // end
        );

        // Finally, the 'names' section s.t. we can check we have the right method in the trace.
        const funcsNamesBytes = [0x01]; // [<funcidx: exportedFuncIndex, "ForeignModuleFrame">]
        encodeULeb128(funcsNamesBytes, exportedFuncIndex);
        funcsNamesBytes.push(0x12, 0x46, 0x6F, 0x72, 0x65, 0x69, 0x67, 0x6E, 0x4D, 0x6F, 0x64, 0x75, 0x6C, 0x65, 0x46, 0x72, 0x61, 0x6D, 0x65);

        const namesSectionBytes = [0x04, 0x6E, 0x61, 0x6D, 0x65]; // "names"
        appendSection(namesSectionBytes, 0x01, funcsNamesBytes); // funcsnames subsection

        appendSection(bytes, 0x00, namesSectionBytes);

        const mod = new WebAssembly.Module(new Uint8Array(bytes));
        const inst = new WebAssembly.Instance(mod, { 'm': { 'n': wasmTable.get(pCallee) } });
        inst.exports['e'](pReason);
    },
};

autoAddDeps(DotNetSupportLib, '$DOTNET');
mergeInto(LibraryManager.library, DotNetSupportLib);
