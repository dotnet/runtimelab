// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SwiftReflector.IOUtils;

namespace SwiftReflector.SwiftXmlReflection
{
    public class GenericDeclarationCollection : List<GenericDeclaration>, IXElementConvertible
    {
        public GenericDeclarationCollection()
            : base()
        {
        }

        public GenericDeclarationCollection(int capacity)
            : base(capacity)
        {
        }

        public XElement ToXElement()
        {
            if (Count == 0)
                return null;
            XElement genparms = new XElement("genericparameters");

            foreach (GenericDeclaration decl in this)
            {
                XElement param = new XElement("param", new XAttribute("name", decl.Name));
                genparms.Add(param);
            }
            foreach (GenericDeclaration decl in this)
            {
                if (decl.Constraints.Count > 0)
                {
                    foreach (BaseConstraint bc in decl.Constraints)
                    {
                        XElement bcel = bc.ToXElement();
                        if (bcel != null)
                            genparms.Add(bcel);
                    }
                }
            }
            return genparms;
        }
    }
}
