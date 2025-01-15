// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dynamo;
using Dynamo.CSLang;
using Xunit;

namespace Dynamo.Unit.Tests;

public class UnitTest1
{
    [Fact]
    public void EmptyFile()
    {
        var csFile = new CSFile(new CSNamespace("SomeNamespace"));
        var text = CodeWriter.WriteToString(csFile);
        var contains = text.Contains("namespace SomeNamespace;");
        Assert.True(contains);
    }

    [Fact]
    public void WritesHeaders()
    {
        var csFile = new CSFile("SomeNamespace");
        csFile.Header.And("This is a header comment").And("It should appear at the top of the file.");
        var text = CodeWriter.WriteToString(csFile);
        var contains0 = text.Contains("// This is a header comment");
        Assert.True(contains0);
        var contains1 = text.Contains("// It should appear at the top of the file.");
        Assert.True(contains1);
    }
}
