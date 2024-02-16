// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace SwiftReflector.SwiftXmlReflection
{
    public class OperatorDeclaration
    {
        public OperatorDeclaration()
        {
        }

        public string ModuleName { get; private set; }
        public string Name { get; private set; }
        public OperatorType OperatorType { get; private set; }
        public string PrecedenceGroup { get; private set; }

        public XElement ToXElement()
        {
            var xobjects = new List<XObject>();
            GatherXObjects(xobjects);
            XElement typeDecl = new XElement("operator", xobjects.ToArray());
            return typeDecl;
        }

        void GatherXObjects(List<XObject> xobjects)
        {
            xobjects.Add(new XAttribute("name", Name));
            if (PrecedenceGroup != null)
                xobjects.Add(new XAttribute("precedenceGroup", PrecedenceGroup));
            xobjects.Add(new XAttribute("operatorKind", OperatorType.ToString()));
        }

        public static OperatorDeclaration FromXElement(XElement elem, string module)
        {
            return new OperatorDeclaration()
            {
                ModuleName = module ?? elem.Attribute("moduleName")?.Value ?? "",
                Name = elem.Attribute("name").Value,
                PrecedenceGroup = NullOnNullOrEmpty(elem.Attribute("precedenceGroup")?.Value),
                OperatorType = FunctionDeclaration.OperatorTypeFromElement((string)elem.Attribute("operatorKind"))
            };
        }

        static string NullOnNullOrEmpty(string s)
        {
            return String.IsNullOrEmpty(s) ? null : s;
        }
    }
}
