// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace SwiftReflector
{
    public interface IXElementConvertible
    {
        XElement ToXElement();
    }
}
