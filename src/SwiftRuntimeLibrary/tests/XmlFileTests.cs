// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using System.Xml;
using System.Configuration;

namespace SwiftRuntimeLibrary.Tests;

public class XmlFileTests
{
    [Fact]
    public void TestXmlFormat()
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load("SwiftCore.xml");
        bool isValidXml = ValidateXmlDoc(xmlDoc);
        Assert.True(isValidXml);
    }

    private bool ValidateXmlDoc(XmlDocument xmlDoc)
    {
        if (xmlDoc == null)
            return false;

        if (xmlDoc.DocumentElement.Name != "xamtypedatabase")
            return false;

        if (xmlDoc.DocumentElement.Attributes["version"]?.Value != "1.0")
            return false;

        XmlNode entitiesNode = xmlDoc.SelectSingleNode("//xamtypedatabase/entities");
        if (entitiesNode == null)
            return false;

        if (entitiesNode.ChildNodes.Count == 0)
            return false;

        foreach (XmlNode entityNode in entitiesNode.ChildNodes)
        {
            if (entityNode.Name != "entity")
                return false;

            XmlNode typeDeclarationNode = entityNode?.SelectSingleNode("typedeclaration");
            if (typeDeclarationNode == null)
                return false;
        }

        return true;
    }
}
