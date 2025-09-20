// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Xunit;

namespace System.Net.Http.Tests
{
    public class WasiHttpInteropTests
    {
        [Fact]
        public void IsValidHeaderValue_ContentLengthNegative_ReturnsFalse()
        {
            // Test that Content-Length with negative values (sentinel values) are filtered out
            bool result = WasiHttpInterop.IsValidHeaderValue("Content-Length", "-1");
            Assert.False(result);
        }

        [Fact]
        public void IsValidHeaderValue_ContentLengthValid_ReturnsTrue()
        {
            // Test that valid Content-Length values are allowed
            bool result = WasiHttpInterop.IsValidHeaderValue("Content-Length", "1024");
            Assert.True(result);
        }

        [Fact]
        public void IsValidHeaderValue_ContentLengthZero_ReturnsTrue()
        {
            // Test that zero Content-Length is valid
            bool result = WasiHttpInterop.IsValidHeaderValue("Content-Length", "0");
            Assert.True(result);
        }

        [Fact]
        public void IsValidHeaderValue_ContentLengthInvalidFormat_ReturnsFalse()
        {
            // Test that invalid formatted Content-Length values are filtered out
            bool result = WasiHttpInterop.IsValidHeaderValue("Content-Length", "invalid");
            Assert.False(result);
        }

        [Fact]
        public void IsValidHeaderValue_OtherHeaders_ReturnsTrue()
        {
            // Test that other headers are not affected by Content-Length validation
            bool result = WasiHttpInterop.IsValidHeaderValue("Server", "nginx/1.0");
            Assert.True(result);
        }

        [Fact]
        public void IsValidHeaderValue_ContentLengthCaseInsensitive_ReturnsFalse()
        {
            // Test that Content-Length validation is case-insensitive
            bool result = WasiHttpInterop.IsValidHeaderValue("content-length", "-1");
            Assert.False(result);
        }
    }
}
