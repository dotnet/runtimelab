// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.CSLang
{
    public class CSArray1D : CSBaseExpression
    {
        public CSArray1D(CSIdentifier name, CommaListElementCollection<CSBaseExpression> paramList)
        {
            Name = Exceptions.ThrowOnNull(name, "name");
            Parameters = Exceptions.ThrowOnNull(paramList, "paramList");
        }

        public CSArray1D(string name, params CSBaseExpression[] parameters)
            : this(new CSIdentifier(name), new CommaListElementCollection<CSBaseExpression>(parameters))
        {
        }

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            Name.WriteAll(writer);
            writer.Write("[", false);
            Parameters.WriteAll(writer);
            writer.Write("]", false);
        }

        public CSIdentifier Name { get; private set; }
        public CommaListElementCollection<CSBaseExpression> Parameters { get; private set; }

        public static CSArray1D New(CSSimpleType type, bool isStackAlloc, params CSBaseExpression[] parameters)
        {
            string ID = (isStackAlloc ? "stackalloc " : "new ") + type.Name;
            return new CSArray1D(ID, parameters);
        }
    }

    public class CSArray1DInitialized : CSBaseExpression
    {
        public CSArray1DInitialized(CSType type, CommaListElementCollection<CSBaseExpression> initializers)
        {
            Type = Exceptions.ThrowOnNull(type, "type");
            Parameters = Exceptions.ThrowOnNull(initializers, "initializers");
        }

        public CSArray1DInitialized(CSType type, params CSBaseExpression[] parameters)
            : this(type, new CommaListElementCollection<CSBaseExpression>(parameters))
        {
        }

        public CSArray1DInitialized(string typeName, params CSBaseExpression[] parameters)
            : this(new CSSimpleType(typeName), new CommaListElementCollection<CSBaseExpression>(parameters))
        {
        }

        public CSArray1DInitialized(string typeName, IEnumerable<CSBaseExpression> parameters)
            : this(new CSSimpleType(typeName), new CommaListElementCollection<CSBaseExpression>(parameters))
        {
        }

        public CSType Type { get; private set; }
        public CommaListElementCollection<CSBaseExpression> Parameters { get; private set; }
        protected override void LLWrite(ICodeWriter writer, object o)
        {
            writer.Write("new ", true);
            Type.WriteAll(writer);
            writer.Write("[", false);
            writer.Write("] ", true);
            writer.Write("{ ", true);
            Parameters.WriteAll(writer);
            writer.Write("}", false);
        }

    }

    public class CSListInitialized : CSBaseExpression
    {
        public CSListInitialized(CSType type, CommaListElementCollection<CSBaseExpression> initializers)
        {
            Type = Exceptions.ThrowOnNull(type, "type");
            Parameters = Exceptions.ThrowOnNull(initializers, "initializers");
        }

        public CSListInitialized(CSType type, params CSBaseExpression[] parameters)
            : this(type, new CommaListElementCollection<CSBaseExpression>(parameters))
        {
        }

        public CSListInitialized(string typeName, params CSBaseExpression[] parameters)
            : this(new CSSimpleType(typeName), new CommaListElementCollection<CSBaseExpression>(parameters))
        {
        }

        public CSListInitialized(string typeName, IEnumerable<CSBaseExpression> parameters)
            : this(new CSSimpleType(typeName), new CommaListElementCollection<CSBaseExpression>(parameters))
        {
        }

        public CSType Type { get; private set; }
        public CommaListElementCollection<CSBaseExpression> Parameters { get; private set; }
        protected override void LLWrite(ICodeWriter writer, object o)
        {
            writer.Write("new List<", true);
            Type.WriteAll(writer);
            writer.Write(">", true);
            writer.Write("(", false);
            writer.Write(") ", true);
            writer.Write("{ ", true);
            Parameters.WriteAll(writer);
            writer.Write("}", false);
        }

    }
}

