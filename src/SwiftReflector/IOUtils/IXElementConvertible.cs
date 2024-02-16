// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace SwiftReflector.IOUtils
{
    public interface IXElementConvertible
    {
        XElement ToXElement();
    }
}

