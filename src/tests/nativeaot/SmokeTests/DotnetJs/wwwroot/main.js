// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet } from './dotnet.js'

const { runMain } = await dotnet
    .withApplicationArguments("A", "B", "C")
    .create();

var result = await runMain();
console.log(`Exit code ${result}`);
