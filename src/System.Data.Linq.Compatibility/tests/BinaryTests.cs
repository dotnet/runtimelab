// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace System.Data.Linq
{
    public class RequestContextTests
    {
        [Fact]
        public void BinaryDefaultCtor()
        {
            Binary bin = new Binary( new byte[] { 0xDE, 0xAD, 0xF0, 0x0D });
        }
    }
}
