// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace System.Text.RegularExpressions.SRM
{
    internal class Regex
    {
        private static readonly CharSetSolver solver = new CharSetSolver();
        private static readonly RegexToAutomatonConverter<BDD> converter = new RegexToAutomatonConverter<BDD>(solver);

        internal const string _DFA_incompatible_with = "DFA option is incompatible with ";

        internal IMatcher matcher;

        public Regex(RegexNode rootNode, System.Text.RegularExpressions.RegexOptions options)
        {
            var root = converter.ConvertNodeToSymbolicRegex(rootNode, true);
            if (!root.info.ContainsSomeCharacter)
                throw new NotSupportedException(_DFA_incompatible_with + "characterless pattern");
            if (root.info.CanBeNullable)
                throw new NotSupportedException(_DFA_incompatible_with + "pattern allowing 0-length match");

            var partition = root.ComputeMinterms();
            if (partition.Length > 64)
            {
                //more than 64 bits needed to represent a set
                matcher = new SymbolicRegexBV(root, solver, converter.srBuilder, partition, options);
            }
            else
            {
                //enough to use 64 bits
                matcher = new SymbolicRegexUInt64(root, solver, converter.srBuilder, partition, options);
            }
        }

        public void Serialize(StringBuilder sb) => throw new NotImplementedException();

        public static Regex Deserialize(string regex) => throw new NotImplementedException();
    }
}
