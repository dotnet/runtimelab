// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using System.Xml;

namespace Swift.Runtime.Tests;

public class XmlFileTests
{
    [Fact]
    public void TestXmlFormat()
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load("TypeDatabase.xml");
        bool isValidXml = TypeDatabase.ValidateXmlSchema(xmlDoc);
        Assert.True(isValidXml);
    }
}
