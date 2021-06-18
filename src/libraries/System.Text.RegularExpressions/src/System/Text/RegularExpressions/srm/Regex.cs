// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Globalization;

namespace System.Text.RegularExpressions.SRM
{
    internal class Regex
    {
        /// <summary>
        /// The unicode component includes the BDD algebra. It is being shared as a static member for efficiency.
        /// </summary>
        internal static readonly Unicode.UnicodeCategoryTheory<BDD> s_unicode = new(new CharSetSolver());

        internal const string _DFA_incompatible_with = "DFA option is incompatible with ";

        internal IMatcher _matcher;

        private Regex(RegexNode rootNode, System.Text.RegularExpressions.RegexOptions options, TimeSpan matchTimeout, CultureInfo? culture)
        {
            //fix the culture to be the given one unless it is null
            //in which case use the InvariantCulture if the option specifies CultureInvariant
            //otherwise use the current culture
            var theculture = culture ?? ((options & RegexOptions.CultureInvariant) != 0 ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture);
            RegexToAutomatonConverter<BDD> converter = new RegexToAutomatonConverter<BDD>(s_unicode, theculture);
            CharSetSolver solver = (CharSetSolver)s_unicode.solver;
            var root = converter.ConvertNodeToSymbolicRegex(rootNode, true);
            if (!root.info.ContainsSomeCharacter)
                throw new NotSupportedException(_DFA_incompatible_with + "characterless pattern");
            if (root.info.CanBeNullable)
                throw new NotSupportedException(_DFA_incompatible_with + "pattern allowing 0-length match");

            var partition = root.ComputeMinterms();
            if (partition.Length > 64)
            {
                //using BV to represent a predicate
                BVAlgebra algBV = new(solver, partition);
                SymbolicRegexBuilder<BV> builderBV = new(algBV);
                //the default constructor sets the following predicates to False, this update happens after the fact
                //it depends on whether anchors where used in the regex whether the predicates are actually different from False
                builderBV.wordLetterPredicate = algBV.ConvertFromCharSet(solver, converter.srBuilder.wordLetterPredicate);
                builderBV.newLinePredicate = algBV.ConvertFromCharSet(solver, converter.srBuilder.newLinePredicate);
                //convert the BDD based AST to BV based AST
                SymbolicRegexNode<BV> rootBV = converter.srBuilder.Transform(root, builderBV, bdd => builderBV.solver.ConvertFromCharSet(solver, bdd));
                SymbolicRegexMatcher<BV> matcherBV = new(rootBV, solver, partition, options, matchTimeout, theculture);
                _matcher = matcherBV;
            }
            else
            {
                //using ulong to represent a predicate
                var alg64 = new BV64Algebra(solver, partition);
                var builder64 = new SymbolicRegexBuilder<ulong>(alg64);
                //the default constructor sets the following predicates to False, this update happens after the fact
                //it depends on whether anchors where used in the regex whether the predicates are actually different from False
                builder64.wordLetterPredicate = alg64.ConvertFromCharSet(solver, converter.srBuilder.wordLetterPredicate);
                builder64.newLinePredicate = alg64.ConvertFromCharSet(solver, converter.srBuilder.newLinePredicate);
                //convert the BDD based AST to ulong based AST
                SymbolicRegexNode<ulong> root64 = converter.srBuilder.Transform(root, builder64, bdd => builder64.solver.ConvertFromCharSet(solver, bdd));
                SymbolicRegexMatcher<ulong> matcher64 = new(root64, solver, partition, options, matchTimeout, theculture);
                _matcher = matcher64;
            }
        }

        public static Regex Create(RegexNode rootNode, System.Text.RegularExpressions.RegexOptions options, TimeSpan matchTimeout, CultureInfo? culture)
        {
            var regex = new Regex(rootNode, options, matchTimeout, culture);
//#if DEBUG
//            //test the serialization roundtrip
//            //effectively, here all tests in DEBUG mode are run with deserialized matchers, not the original ones
//            StringBuilder sb = new();
//            regex.Serialize(sb);
//            regex = Regex.Deserialize(sb.ToString());
//#endif
            return regex;
        }

        /// <summary>
        /// This constructor is invoked by the deserializer only.
        /// </summary>
        private Regex(IMatcher matcher) => _matcher = matcher;

        /// <summary>
        /// Serialize the matcher by appending it into sb.
        /// Uses characters only from visible ASCII range.
        /// </summary>
        public void Serialize(StringBuilder sb) => _matcher.Serialize(sb);

        //it must not be '\n' or a character used to serialize the fragments: 0-9A-Za-z/\+*()[].,-^$;?
        //avoiding '\n' so that multiple serializations can be stored one per line in an ascii text file
        internal const char s_top_level_separator = '#';

        /// <summary>
        /// Deserializes the matcher from the given input string created with Serialize.
        /// </summary>
        public static Regex Deserialize(string input)
        {
            input.Split(s_top_level_separator);
            string[] fragments = input.Split(s_top_level_separator);
            if (fragments.Length != 15)
                throw new ArgumentException($"{nameof(Regex.Deserialize)} error", nameof(input));

            try
            {
                BVAlgebraBase alg = BVAlgebraBase.Deserialize(fragments[1]);
                IMatcher matcher = alg is BV64Algebra ?
                    new SymbolicRegexMatcher<ulong>(alg as BV64Algebra, fragments) :
                    new SymbolicRegexMatcher<BV>(alg as BVAlgebra, fragments);
                return new Regex(matcher);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"{nameof(Regex.Deserialize)} error", nameof(input), e);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            Serialize(sb);
            return sb.ToString();
        }

        public void SaveDGML(TextWriter writer, int bound, bool hideStateInfo, bool addDotStar, bool inReverse, bool onlyDFAinfo, int maxLabelLength)
        {
            _matcher.SaveDGML(writer, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength);
        }
    }
}
