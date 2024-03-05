// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang
{
    public class CSVariableDeclaration : LineCodeElementCollection<ICodeElement>, ICSExpression, ICSLineable
    {
        public CSVariableDeclaration(CSType type, IEnumerable<CSBinding> bindings)
            : base(null, false, true)
        {
            ArgumentNullException.ThrowIfNull(type, nameof(type));
            ArgumentNullException.ThrowIfNull(bindings, nameof(bindings));
            Type = type;
            And(Type).And(SimpleElement.Spacer);
            Bindings = new CommaListElementCollection<CSBinding>(bindings);
            Add(Bindings);
        }

        public CSVariableDeclaration(CSType type, string name, ICSExpression? value = null)
            : this(type, new CSIdentifier(name), value)
        {
        }

        public CSVariableDeclaration(CSType type, CSIdentifier name, ICSExpression? value = null)
            : this(type, new CSBinding[] { new CSBinding(name, value) })
        {
        }

        public CSType Type { get; private set; }
        public CommaListElementCollection<CSBinding> Bindings { get; private set; }

        public static CSLine VarLine(CSType type, CSIdentifier name, ICSExpression? value = null)
        {
            return new CSLine(new CSVariableDeclaration(type, name, value));
        }

        public static CSLine VarLine(CSType type, string name, ICSExpression? value = null)
        {
            return new CSLine(new CSVariableDeclaration(type, name, value));
        }

        public static CSLine VarLine(string name, ICSExpression value)
        {
            return new CSLine(new CSVariableDeclaration(CSSimpleType.Var, name, value));
        }

        public static CSLine VarLine(CSIdentifier name, ICSExpression value)
        {
            return new CSLine(new CSVariableDeclaration(CSSimpleType.Var, name, value));
        }
    }

    public class CSFieldDeclaration : CSVariableDeclaration
    {
        public CSFieldDeclaration(CSType type, IEnumerable<CSBinding> bindings, CSVisibility vis = CSVisibility.None, bool isStatic = false, bool isReadonly = false, bool isUnsafe = false)
            : base(type, bindings)
        {
            Visibilty = vis;
            IsStatic = isStatic;
            IsUnsafe = isUnsafe;
            if (isReadonly)
            {
                this.Insert(0, new SimpleElement("readonly"));
                this.Insert(1, SimpleElement.Spacer);
            }
            if (IsUnsafe)
            {
                this.Insert(0, new SimpleElement("unsafe"));
                this.Insert(1, SimpleElement.Spacer);
            }
            if (isStatic)
            {
                this.Insert(0, new SimpleElement("static"));
                this.Insert(1, SimpleElement.Spacer);
            }
            if (vis != CSVisibility.None)
            {
                this.Insert(0, new SimpleElement(CSMethod.VisibilityToString(vis)));
                this.Insert(1, SimpleElement.Spacer);
            }
        }

        public CSFieldDeclaration(CSType type, string name, ICSExpression? value = null, CSVisibility vis = CSVisibility.None, bool isStatic = false, bool isReadonly = false, bool isUnsafe = false)
            : this(type, new CSIdentifier(name), value, vis, isStatic, isReadonly, isUnsafe)
        {
        }

        public CSFieldDeclaration(CSType type, CSIdentifier name, ICSExpression? value = null, CSVisibility vis = CSVisibility.None, bool isStatic = false, bool isReadOnly = false, bool isUnsafe = false)
            : this(type, new CSBinding[] { new CSBinding(name, value) }, vis, isStatic, isReadOnly, isUnsafe)
        {
        }

        public CSVisibility Visibilty { get; private set; }
        public bool IsStatic { get; private set; }
        public bool IsUnsafe { get; private set; }

        public static CSLine FieldLine(CSType type, CSIdentifier name, ICSExpression? value = null, CSVisibility vis = CSVisibility.None, bool isStatic = false)
        {
            return new CSLine(new CSFieldDeclaration(type, name, value, vis, isStatic));
        }

        public static CSLine FieldLine(CSType type, string name, ICSExpression? value = null, CSVisibility vis = CSVisibility.None, bool isStatic = false)
        {
            return new CSLine(new CSFieldDeclaration(type, name, value, vis, isStatic));
        }
    }
}
