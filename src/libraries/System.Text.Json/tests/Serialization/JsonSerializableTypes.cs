// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if GENERATE_JSON_METADATA
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Tests;

// TODO: we shouldn't have to specify some of these explicitly.
// 1. Generate (linker-trimmable) metadata for some of these type by default.
// 2. Trace the T in JsonSerializer.(De)serialize calls and add those to input serializable types for the generator.

[assembly: JsonSerializable(typeof(byte[]))]
[assembly: JsonSerializable(typeof(int[]))]
[assembly: JsonSerializable(typeof(byte[][]))]
[assembly: JsonSerializable(typeof(int[][]))]
[assembly: JsonSerializable(typeof(int[][][]))]
[assembly: JsonSerializable(typeof(List<byte>))]
[assembly: JsonSerializable(typeof(SimpleTestClass))]
[assembly: JsonSerializable(typeof(SimpleTestClass[]))]
[assembly: JsonSerializable(typeof(List<SimpleTestClass>))]
[assembly: JsonSerializable(typeof(TestClassWithStringArray))]
[assembly: JsonSerializable(typeof(TestClassWithGenericList))]
[assembly: JsonSerializable(typeof(TestClassWithGenericIEnumerable))]
[assembly: JsonSerializable(typeof(TestClassWithGenericIList))]
[assembly: JsonSerializable(typeof(TestClassWithGenericICollection))]
[assembly: JsonSerializable(typeof(TestClassWithGenericIEnumerableT))]
[assembly: JsonSerializable(typeof(TestClassWithGenericIListT))]
[assembly: JsonSerializable(typeof(TestClassWithGenericICollectionT))]
[assembly: JsonSerializable(typeof(TestClassWithGenericIReadOnlyCollectionT))]
[assembly: JsonSerializable(typeof(TestClassWithGenericIReadOnlyListT))]
[assembly: JsonSerializable(typeof(TestClassWithGenericISetT))]
[assembly: JsonSerializable(typeof(TestClassWithInitializedArray))]
[assembly: JsonSerializable(typeof(TestClassWithObjectArray))]
[assembly: JsonSerializable(typeof(TestClassWithObjectList))]
[assembly: JsonSerializable(typeof(TestClassWithObjectIEnumerable))]
[assembly: JsonSerializable(typeof(TestClassWithObjectIList))]
[assembly: JsonSerializable(typeof(TestClassWithObjectICollection))]
[assembly: JsonSerializable(typeof(TestClassWithObjectIEnumerableT))]
[assembly: JsonSerializable(typeof(TestClassWithObjectIListT))]
[assembly: JsonSerializable(typeof(TestClassWithObjectICollectionT))]
[assembly: JsonSerializable(typeof(TestClassWithObjectIReadOnlyCollectionT))]
[assembly: JsonSerializable(typeof(TestClassWithObjectIReadOnlyListT))]
[assembly: JsonSerializable(typeof(TestClassWithObjectISetT))]
[assembly: JsonSerializable(typeof(TestClassWithInitializedArray))]
[assembly: JsonSerializable(typeof(TestClassWithObjectImmutableTypes))]
[assembly: JsonSerializable(typeof(TestClassWithObjectIEnumerableConstructibleTypes))]
#endif
