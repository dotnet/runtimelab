// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet } from './dotnet.js'

dotnet.withConfig({
    resources: {
        jsModuleRuntime: { "dotnet.runtime.js": "" },
        jsModuleNative: { "dotnet.native.js": "" },
        wasmNative: { "dotnet.native.wasm": "" }
    }
}).withApplicationArguments("A", "B", "C");

const { runMain } = await dotnet.create();

var result = await runMain();
console.log(`Exit code ${result}`);
