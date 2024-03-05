// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang
{
    public class CSIdentifier : CSBaseExpression
    {
        public CSIdentifier(string name)
        {
            ArgumentNullException.ThrowIfNull(name, nameof(name));
            Name = name;
        }

        public static explicit operator CSIdentifier(string name)
        {
            return new CSIdentifier(name);
        }

        protected override void LLWrite(ICodeWriter writer, object? o)
        {
            writer.Write(Name, false);
        }

        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }

        public static CSIdentifier Create(string name) => new CSIdentifier(name);

        static CSIdentifier thisID = new CSIdentifier("this");
        public static CSIdentifier This { get { return thisID; } }
        static CSIdentifier baseID = new CSIdentifier("base");
        public static CSIdentifier Base { get { return baseID; } }
    }
}
