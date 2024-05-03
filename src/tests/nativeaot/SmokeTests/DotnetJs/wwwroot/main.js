// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet, exit } from './dotnet.js'

const { runMain, setModuleImports, getAssemblyExports } = await dotnet
    .withApplicationArguments("A", "B", "C")
    .withMainAssembly("DotnetJs")
    .create();

setModuleImports('main.js', {
    interop: {
        math: (a, b, c) => a + b * c,
    }
});

let result = await runMain();

const exports = await getAssemblyExports("DotnetJs.dll");
const square = exports.DotnetJsApp.Program.Interop.Square(5);
if (square != 25) {
    result = 13;
}

try {
    exports.DotnetJsApp.Program.Interop.Throw();
    result = 14;
} catch (e) {
    console.log(`Thrown expected exception: ${e}`);
}

const concat = exports.DotnetJsApp.Program.Interop.Concat("Aaa", "Bbb");
if (concat != "AaaBbb") {
    result = 15;
}

let isPromiseResolved = false;
let promise = new Promise(resolve => setTimeout(() => { console.log("Promise resolved"); isPromiseResolved = true; resolve(); }, 2000));
let asyncResult = await exports.DotnetJsApp.Program.Interop.Async(promise);
console.log(`Async result: ${asyncResult}`);
if (!isPromiseResolved) {
    result = 16;
}
if (asyncResult != 87) {
    result = 17;
}

try {
    isPromiseResolved = false;
    promise = new Promise(resolve => setTimeout(() => { console.log("Promise resolved"); isPromiseResolved = true; resolve(); }, 2000));
    asyncResult = await exports.DotnetJsApp.Program.Interop.Async(promise, true);
    console.log(`Async result: ${asyncResult}`);
    if (!isPromiseResolved) {
        result = 18;
    }
    result = 19;
} catch (e) {
    console.log(`Thrown expected exception: ${e}`);
}

const cancelResult = await exports.DotnetJsApp.Program.Interop.AsyncWithCancel();
if (cancelResult !== 0) {
    console.log(`Unexpected result from AsyncWithCancel: ${cancelResult}`);
    result = 20;
}

console.log(`Exit code ${result}`);
exit(result);
