// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Tests;

// TODO: we shouldn't have to specify some of these explicitly.
// 1. Generate (linker-trimmable) metadata for some of these type by default.
// 2. Trace the T in JsonSerializer.(De)serialize calls and add those to input serializable types for the generator.

[module: JsonSerializable(typeof(byte[]))]
[module: JsonSerializable(typeof(int[]))]
[module: JsonSerializable(typeof(byte[][]))]
[module: JsonSerializable(typeof(int[][]))]
[module: JsonSerializable(typeof(int[][][]))]
[module: JsonSerializable(typeof(List<byte>))]
[module: JsonSerializable(typeof(SimpleTestClass))]
[module: JsonSerializable(typeof(SimpleTestClass[]))]
[module: JsonSerializable(typeof(List<SimpleTestClass>))]
[module: JsonSerializable(typeof(TestClassWithStringArray))]
[module: JsonSerializable(typeof(TestClassWithGenericList))]
[module: JsonSerializable(typeof(TestClassWithGenericIEnumerable))]
[module: JsonSerializable(typeof(TestClassWithGenericIList))]
[module: JsonSerializable(typeof(TestClassWithGenericICollection))]
[module: JsonSerializable(typeof(TestClassWithGenericIEnumerableT))]
[module: JsonSerializable(typeof(TestClassWithGenericIListT))]
[module: JsonSerializable(typeof(TestClassWithGenericICollectionT))]
[module: JsonSerializable(typeof(TestClassWithGenericIReadOnlyCollectionT))]
[module: JsonSerializable(typeof(TestClassWithGenericIReadOnlyListT))]
[module: JsonSerializable(typeof(TestClassWithGenericISetT))]
[module: JsonSerializable(typeof(TestClassWithInitializedArray))]
[module: JsonSerializable(typeof(TestClassWithObjectArray))]
[module: JsonSerializable(typeof(TestClassWithObjectList))]
[module: JsonSerializable(typeof(TestClassWithObjectIEnumerable))]
[module: JsonSerializable(typeof(TestClassWithObjectIList))]
[module: JsonSerializable(typeof(TestClassWithObjectICollection))]
[module: JsonSerializable(typeof(TestClassWithObjectIEnumerableT))]
[module: JsonSerializable(typeof(TestClassWithObjectIListT))]
[module: JsonSerializable(typeof(TestClassWithObjectICollectionT))]
[module: JsonSerializable(typeof(TestClassWithObjectIReadOnlyCollectionT))]
[module: JsonSerializable(typeof(TestClassWithObjectIReadOnlyListT))]
[module: JsonSerializable(typeof(TestClassWithObjectISetT))]
[module: JsonSerializable(typeof(TestClassWithInitializedArray))]
[module: JsonSerializable(typeof(TestClassWithObjectImmutableTypes))]
[module: JsonSerializable(typeof(TestClassWithObjectIEnumerableConstructibleTypes))]
