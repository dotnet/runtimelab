// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.StreamSourceGeneration.Tests.TestClasses;
using Xunit;

namespace System.IO.StreamSourceGeneration.Tests;

public class GenerateStreamBoilerplateGeneratedCodeTests
{
    [Fact]
    public void Stream_CantRead_CantWrite_CantSeek_MembersThrow()
    {
        var s = new StreamCantReadCantWriteCantSeek();

        //Assert.Throws<NotSupportedException>(() => s.)
    }
}
