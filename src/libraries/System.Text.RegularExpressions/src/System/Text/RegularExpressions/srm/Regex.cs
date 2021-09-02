// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.IO;

namespace System.Text.RegularExpressions.SRM
{
    internal sealed class Regex
    {
        /// <summary>
        /// The unicode component includes the BDD algebra. It is being shared as a static member for efficiency.
        /// </summary>
        internal static readonly Unicode.UnicodeCategoryTheory<BDD> s_unicode = new(new CharSetSolver());

        internal readonly IMatcher _matcher;

        private Regex(RegexNode rootNode, RegexOptions options, TimeSpan matchTimeout, CultureInfo culture)
        {
            //fix the culture to be the given one unless it is null
            //in which case use the InvariantCulture if the option specifies CultureInvariant
            //otherwise use the current culture
            RegexToAutomatonConverter<BDD> converter = new RegexToAutomatonConverter<BDD>(s_unicode, culture);
            CharSetSolver solver = (CharSetSolver)s_unicode._solver;
            SymbolicRegexNode<BDD> root = converter.ConvertNodeToSymbolicRegex(rootNode, true);

            BDD[] partition = root.ComputeMinterms();
            if (partition.Length > 64)
            {
                //using BV to represent a predicate
                BVAlgebra algBV = new(solver, partition);
                SymbolicRegexBuilder<BV> builderBV = new(algBV);
                //the default constructor sets the following predicates to False, this update happens after the fact
                //it depends on whether anchors where used in the regex whether the predicates are actually different from False
                builderBV._wordLetterPredicate = algBV.ConvertFromCharSet(solver, converter._srBuilder._wordLetterPredicate);
                builderBV._newLinePredicate = algBV.ConvertFromCharSet(solver, converter._srBuilder._newLinePredicate);
                //convert the BDD based AST to BV based AST
                SymbolicRegexNode<BV> rootBV = converter._srBuilder.Transform(root, builderBV, bdd => builderBV._solver.ConvertFromCharSet(solver, bdd));
                SymbolicRegexMatcher<BV> matcherBV = new(rootBV, solver, partition, options, matchTimeout, culture);
                _matcher = matcherBV;
            }
            else
            {
                //using ulong to represent a predicate
                var alg64 = new BV64Algebra(solver, partition);
                var builder64 = new SymbolicRegexBuilder<ulong>(alg64)
                {
                    //the default constructor sets the following predicates to False, this update happens after the fact
                    //it depends on whether anchors where used in the regex whether the predicates are actually different from False
                    _wordLetterPredicate = alg64.ConvertFromCharSet(solver, converter._srBuilder._wordLetterPredicate),
                    _newLinePredicate = alg64.ConvertFromCharSet(solver, converter._srBuilder._newLinePredicate)
                };
                //convert the BDD based AST to ulong based AST
                SymbolicRegexNode<ulong> root64 = converter._srBuilder.Transform(root, builder64, bdd => builder64._solver.ConvertFromCharSet(solver, bdd));
                SymbolicRegexMatcher<ulong> matcher64 = new(root64, solver, partition, options, matchTimeout, culture);
                _matcher = matcher64;
            }
        }

        public static Regex Create(RegexNode rootNode, RegexOptions options, TimeSpan matchTimeout, CultureInfo culture)
        {
            var regex = new Regex(rootNode, options, matchTimeout, culture);
#if DEBUG
            //// Test the serialization roundtrip.
            //// Effectively, here all tests in DEBUG mode are run with deserialized matchers, not the original ones.
            //regex = Deserialize(regex.Serialize());
#endif
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
        public string Serialize()
        {
            var sb = new StringBuilder();
            _matcher.Serialize(sb);
            return sb.ToString();
        }

        //it must not be '\n' or a character used to serialize the fragments: 0-9A-Za-z/\+*()[].,-^$;?
        //avoiding '\n' so that multiple serializations can be stored one per line in an ascii text file
        internal const char TopLevelSeparator = '#';

        /// <summary>
        /// Deserializes the matcher from the given input string created with Serialize.
        /// </summary>
        public static Regex Deserialize(string input)
        {
            string[] fragments = input.Split(TopLevelSeparator);
            if (fragments.Length != 15)
                throw new ArgumentException($"{nameof(Regex.Deserialize)} error", nameof(input));

            try
            {
                BVAlgebraBase alg = BVAlgebraBase.Deserialize(fragments[1]);
                IMatcher matcher = alg is BV64Algebra bv64 ?
                    new SymbolicRegexMatcher<ulong>(bv64, fragments) :
                    new SymbolicRegexMatcher<BV>(alg as BVAlgebra, fragments);
                return new Regex(matcher);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"{nameof(Regex.Deserialize)} error", nameof(input), e);
            }
        }

        public override string ToString() => Serialize();

        public void SaveDGML(TextWriter writer, int bound, bool hideStateInfo, bool addDotStar, bool inReverse, bool onlyDFAinfo, int maxLabelLength) => _matcher.SaveDGML(writer, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength);
    }
}
