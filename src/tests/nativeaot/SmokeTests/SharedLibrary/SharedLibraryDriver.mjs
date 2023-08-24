import { readFile } from 'node:fs/promises';
import { WASI } from 'wasi';
import { argv, env } from 'node:process';

const wasi = new WASI({
  version: 'preview1',
  args: argv,
  env
});

const wasm = await WebAssembly.compile(  
  await readFile(new URL("./SharedLibrary.wasm", import.meta.url)),
);

const instance = await WebAssembly.instantiate(wasm, wasi.getImportObject());

wasi.initialize(instance);

if (instance.exports.ReturnsPrimitiveInt() != 10)
    process.exit(1);

if (instance.exports.ReturnsPrimitiveBool() != 1)
    process.exit(2);

if (instance.exports.ReturnsPrimitiveChar() != 97) // 'a'
    process.exit(3);

// As long as no unmanaged exception is thrown managed class loaders were initialized successfully.
instance.exports.EnsureManagedClassLoaders();

// #if !CODEGEN_WASI - for some reason tries to create a background thread?
// if (instance.exports.CheckSimpleGCCollect() != 100)
//     process.exit(4);

// #if !CODEGEN_WASI - enable when we support exception handling
// if (instance.exports.CheckSimpleExceptionHandling() != 100)
//    process.exit(5);

process.exit(100);
