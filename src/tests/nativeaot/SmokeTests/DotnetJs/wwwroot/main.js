// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet, exit } from './dotnet.js'

const { runMain, setModuleImports, getAssemblyExports } = await dotnet
    .withApplicationArguments("A", "B", "C")
    .withMainAssembly("DotnetJs.App")
    .create();

setModuleImports('main.js', {
    interop: {
        math: (a, b, c) => a + b * c,
    }
});

let result = await runMain();

const exports = await getAssemblyExports("DotnetJs.App.dll");

const square = exports.DotnetJs.App.Program.Interop.Square(5);
if (square != 25) {
    result = 13;
}

try {
    exports.DotnetJs.App.Program.Interop.Throw();
    result = 14;
} catch (e) {
    console.log(`Thrown expected exception: ${e.message}`);
}

const concat = exports.DotnetJs.App.Program.Interop.Concat("Aaa", "Bbb");
if (concat != "AaaBbb") {
    result = 15;
}

let isPromiseResolved = false;
let promise = new Promise(resolve => setTimeout(() => { console.log("Promise resolved"); isPromiseResolved = true; resolve(); }, 100));
let asyncResult = await exports.DotnetJs.App.Program.Interop.Async(promise);
console.log(`Async result: ${asyncResult}`);
if (!isPromiseResolved) {
    result = 16;
}
if (asyncResult != 87) {
    result = 17;
}

try {
    isPromiseResolved = false;
    promise = new Promise(resolve => setTimeout(() => { console.log("Promise resolved"); isPromiseResolved = true; resolve(); }, 100));
    asyncResult = await exports.DotnetJs.App.Program.Interop.Async(promise, true);
    if (asyncResult != 87) {
        console.log(`Unexpected async result: ${asyncResult}`);
        result = 18;
    }
    result = 19;
} catch (e) {
    if (!isPromiseResolved) {
        result = 20;
    }
    console.log(`Thrown expected exception: ${e.message}`);
}

const cancelResult = await exports.DotnetJs.App.Program.Interop.AsyncWithCancel();
if (cancelResult !== 0) {
    console.log(`Unexpected result from AsyncWithCancel: ${cancelResult}`);
    result = 21;
}

var jsObject = { x: 42 };
var jsObjectResult = exports.DotnetJs.App.Program.Interop.JSObject(jsObject);
if (!jsObjectResult) {
    console.log(`Unexpected result from JSObject: ${jsObjectResult}`);
    result = 22;
}

if (jsObject.y != jsObject.x + 1) {
    console.log(`Unexpected y value on JSObject: ${jsObject.y}`);
    result = 23;
}

const msgs = [];
const csharpFunc = exports.DotnetJs.App.Program.Interop.DelegateMarshalling(() => "String from JavaScript", msg => { msgs.push(msg); console.log(`Message from C# '${msg}'`); });
if (msgs.length !== 1 || msgs[0] !== "Wrapping value in C# 'String from JavaScript'") {
    console.log(`Unexpected number of messages from Func: ${JSON.stringify(msgs)}`);
    result = 24;
}

const csharpFuncResult = csharpFunc();
if (csharpFuncResult !== 42) {
    console.log(`Unexpected result from Func returned from DelegateMarshalling: ${csharpFuncResult}`);
    result = 25;
}

console.log(`Exit code ${result}`);
exit(result);
