// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Define for a small subset of performance-critical FCalls that do not
// have a shadow stack argument. Has not real functional importance and
// serves as simply a marker for such FCalls.
//
#define FCIMPL_NO_SS(_rettype, _name, ...) extern "C" _rettype _name(__VA_ARGS__) {
