// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Xunit;

namespace System.Text.Json.SourceGeneration.Tests
{
    public class JsonSerializerSourceGeneratorTests
    {
        [JsonSerializable]
        public class SampleInternalTest
        {
        }

        [JsonSerializable(typeof(KeyValuePair))]
        public class SampleExternalTest
        {
        }

        [Fact]
        public void TestGeneratedCode()
        {
            var internalTypeTest = new HelloWorldGenerated.SampleInternalTestClassInfo();
            var externalTypeTest = new HelloWorldGenerated.SampleExternalTestClassInfo();

            Assert.Equal("SampleInternalTest", internalTypeTest.testMethod());
            Assert.Equal("SampleExternalTest", externalTypeTest.testMethod());
        }
    }
}
