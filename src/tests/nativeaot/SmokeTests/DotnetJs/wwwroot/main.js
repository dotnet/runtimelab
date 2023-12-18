// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet, exit } from './dotnet.js'

const { runMain, setModuleImports } = await dotnet
    .withApplicationArguments("A", "B", "C")
    .create();

setModuleImports('main.js', {
    interop: {
        math: (a, b, c) => a + b * c,
    }
});

let result = await runMain();

const exports = getAssemblyExports("DotnetJs.dll");
const square = exports.DotnetJsApp.Interop.Square(5);
if (square != 25) {
    result = 3;
}

console.log(`Exit code ${result}`);
exit(result);
