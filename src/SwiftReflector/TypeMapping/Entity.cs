// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SwiftReflector.SwiftXmlReflection;
using SwiftReflector.IOUtils;
using System.Xml.Linq;

namespace SwiftReflector.TypeMapping
{
    public class Entity : IXElementConvertible
    {
        public Entity()
        {
        }

        public string SharpNamespace { get; set; }
        public string SharpTypeName { get; set; }
        public EntityType EntityType { get; set; }
        public string ProtocolProxyModule { get; set; }
        public bool IsDiscretionaryConstraint { get; set; }

        public TypeDeclaration Type { get; set; }

        public DotNetName GetFullType()
        {
            return new DotNetName(SharpNamespace, SharpTypeName);
        }

        public bool IsStructOrEnum
        {
            get
            {
                return EntityType == EntityType.Struct || EntityType == EntityType.Enum ||
                                           EntityType == EntityType.TrivialEnum;
            }
        }

        public bool IsStructClassOrEnum
        {
            get
            {
                return IsStructOrEnum || EntityType == EntityType.Class;
            }
        }

        public bool IsObjCClass
        {
            get
            {
                return EntityType == EntityType.Class && (Type != null && Type.IsObjC);
            }
        }

        public bool IsObjCStruct
        {
            get
            {
                return EntityType == EntityType.Struct && (Type != null && Type.IsObjC);
            }
        }

        public bool IsObjCEnum
        {
            get
            {
                return EntityType == EntityType.Enum && (Type != null && Type.IsObjC);
            }
        }

        public bool IsObjCProtocol
        {
            get
            {
                return EntityType == EntityType.Protocol && (Type != null && Type.IsObjC);
            }
        }

        #region IXElementConvertible implementation

        public XElement ToXElement()
        {
            return new XElement("entity",
                                 new XAttribute("sharpNameSpace", SharpNamespace),
                                 new XAttribute("sharpTypeName", SharpTypeName),
                                 new XAttribute("entityType", EntityType),
                                 new XAttribute("protocolProxyModule", ProtocolProxyModule ?? ""),
                                 new XAttribute("discretionaryConstraint", IsDiscretionaryConstraint ? "true" : "false"),
                                 Type.ToXElement());
        }

        #endregion
    }
}

