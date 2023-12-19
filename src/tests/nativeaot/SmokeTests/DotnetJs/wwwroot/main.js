// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet, exit } from './dotnet.js'

const { runMain, setModuleImports, getAssemblyExports } = await dotnet
    .withApplicationArguments("A", "B", "C")
    .create();

setModuleImports('main.js', {
    interop: {
        math: (a, b, c) => a + b * c,
    }
});

let result = await runMain();

try {
    const exports = await getAssemblyExports("DotnetJs.dll");
    const square = exports.DotnetJsApp.Program.Interop.Square(5);
    if (square != 25) {
        result = 13;
    }
} catch (e) {
    console.log(`Square thrown ${e}`);
    result = 14;
}

console.log(`Exit code ${result}`);
exit(result);
