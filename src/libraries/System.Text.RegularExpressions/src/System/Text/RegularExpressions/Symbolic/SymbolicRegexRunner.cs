// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions.Symbolic.Unicode;

namespace System.Text.RegularExpressions.Symbolic
{
    internal sealed class SymbolicRegexRunner : RegexRunner
    {
        /// <summary>The unicode component, including the BDD algebra.</summary>
        internal static readonly UnicodeCategoryTheory<BDD> s_unicode = new UnicodeCategoryTheory<BDD>(new CharSetSolver());

        internal readonly SymbolicRegexMatcher _matcher;

        private SymbolicRegexRunner(RegexNode rootNode, RegexOptions options, TimeSpan matchTimeout, CultureInfo culture)
        {
            var converter = new RegexNodeToSymbolicConverter(s_unicode, culture);
            var solver = (CharSetSolver)s_unicode._solver;
            SymbolicRegexNode<BDD> root = converter.Convert(rootNode, topLevel: true);

            BDD[] minterms = root.ComputeMinterms();
            if (minterms.Length > 64)
            {
                // Use BV to represent a predicate
                var algBV = new BVAlgebra(solver, minterms);
                var builderBV = new SymbolicRegexBuilder<BV>(algBV);

                // The default constructor sets the following predicates to False; this update happens after the fact.
                // It depends on whether anchors where used in the regex whether the predicates are actually different from False.
                builderBV._wordLetterPredicate = algBV.ConvertFromCharSet(solver, converter._builder._wordLetterPredicate);
                builderBV._newLinePredicate = algBV.ConvertFromCharSet(solver, converter._builder._newLinePredicate);

                //Convert the BDD based AST to BV based AST
                SymbolicRegexNode<BV> rootBV = converter._builder.Transform(root, builderBV, bdd => builderBV._solver.ConvertFromCharSet(solver, bdd));
                _matcher = new SymbolicRegexMatcher<BV>(rootBV, solver, minterms, options, matchTimeout, culture);
            }
            else
            {
                // Use ulong to represent a predicate
                var alg64 = new BV64Algebra(solver, minterms);
                var builder64 = new SymbolicRegexBuilder<ulong>(alg64)
                {
                    // The default constructor sets the following predicates to False, this update happens after the fact
                    // It depends on whether anchors where used in the regex whether the predicates are actually different from False
                    _wordLetterPredicate = alg64.ConvertFromCharSet(solver, converter._builder._wordLetterPredicate),
                    _newLinePredicate = alg64.ConvertFromCharSet(solver, converter._builder._newLinePredicate)
                };

                // Convert the BDD-based AST to ulong-based AST
                SymbolicRegexNode<ulong> root64 = converter._builder.Transform(root, builder64, bdd => builder64._solver.ConvertFromCharSet(solver, bdd));
                _matcher = new SymbolicRegexMatcher<ulong>(root64, solver, minterms, options, matchTimeout, culture);
            }
        }

        /// <summary>This constructor is invoked by the deserializer only.</summary>
        private SymbolicRegexRunner(SymbolicRegexMatcher matcher) => _matcher = matcher;

        public static RegexRunnerFactory CreateFactory(RegexNode rootNode, RegexOptions options, TimeSpan matchTimeout, CultureInfo culture)
        {
            // RightToLeft and ECMAScript are currently not supported in conjunction with NonBacktracking.
            if ((options & (RegexOptions.RightToLeft | RegexOptions.ECMAScript)) != 0)
            {
                throw new NotSupportedException(
                    SR.Format(SR.NotSupported_NonBacktrackingConflictingOption,
                        (options & RegexOptions.RightToLeft) != 0 ? nameof(RegexOptions.RightToLeft) : nameof(RegexOptions.ECMAScript)));
            }

            var runner = new SymbolicRegexRunner(rootNode, options, matchTimeout, culture);
#if DEBUG
            // Test the serialization roundtrip.
            // Effectively, here all tests in DEBUG mode are run with deserialized matchers, not the original ones.
            runner = Deserialize(runner.Serialize());
#endif
            return new SymbolicRegexRunnerFactory(runner);
        }

        internal override Match? ScanInternal(
            Regex regex, string text, int textbeg, int textend, int textstart, int prevlen, bool quick, TimeSpan timeout)
        {
            int length = textend - textbeg;

            // If the previous match was empty, advance by one before matching
            // or terminate the matching if there is no remaining input to search in
            if (prevlen == 0)
            {
                if (textstart == textend)
                {
                    return Match.Empty;
                }

                textstart += 1;
            }

            SymbolicMatch pos = _matcher.FindMatch(quick, text, textstart, textend);
            if (!pos.Success)
            {
                return Match.Empty;
            }

            if (quick)
            {
                return null;
            }

            var m = new Match(regex, 1, text, textbeg, length, textstart);
            m._matches[0][0] = pos.Index;
            m._matches[0][1] = pos.Length;
            m._matchcount[0] = 1;
            m.Tidy(pos.Index + pos.Length);
            return m;
        }

        internal override void ScanInternal<TState>(Regex regex, string text, int textstart, ref TState state, MatchCallback<TState> callback, bool reuseMatchObject, TimeSpan timeout)
        {
            Match? m = null;
            while (true)
            {
                // If the previous match was empty, advance by one before matching or terminate the
                // matching if there is no remaining input to search in
                if (m?.Length == 0)
                {
                    if (textstart == text.Length)
                    {
                        break;
                    }

                    textstart += 1;
                }

                SymbolicMatch pos = _matcher.FindMatch(isMatch: false, text, textstart, text.Length);
                if (!pos.Success)
                {
                    break;
                }

                if (!reuseMatchObject || m is null)
                {
                    m = new Match(regex, capcount: 1, text, begpos: 0, text.Length, textstart);
                }

                m._matches[0][0] = pos.Index;
                m._matches[0][1] = pos.Length;
                m._matchcount[0] = 1;
                m.Tidy(pos.Index + pos.Length);

                if (!callback(ref state, m))
                {
                    break;
                }

                textstart = m.Index + m.Length;
            }
        }

        /// <summary>Serialize the matcher.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            _matcher.Serialize(sb);
            return sb.ToString();
        }

        //it must not be '\n' or a character used to serialize the fragments: 0-9A-Za-z/\+*()[].,-^$;?
        //avoiding '\n' so that multiple serializations can be stored one per line in an ascii text file
        internal const char TopLevelSeparator = '#';

        /// <summary>Deserializes the matcher from the given input string created with Serialize.</summary>
        public static SymbolicRegexRunner Deserialize(string serializedNonBacktrackingRegex)
        {
            Exception? error = null;
            try
            {
                string[] fragments = serializedNonBacktrackingRegex.Split(TopLevelSeparator);
                if (fragments.Length == 15)
                {
                    BVAlgebraBase alg = BVAlgebraBase.Deserialize(fragments[1]);
                    Debug.Assert(alg is BV64Algebra or BVAlgebra);

                    SymbolicRegexMatcher matcher = alg is BV64Algebra bv64 ?
                        new SymbolicRegexMatcher<ulong>(bv64, fragments) :
                        new SymbolicRegexMatcher<BV>((BVAlgebra)alg, fragments);

                    return new SymbolicRegexRunner(matcher);
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            throw new ArgumentOutOfRangeException(nameof(serializedNonBacktrackingRegex), error);
        }

        public static Regex DeserializeRegex(string serializedNonBacktrackingRegex) =>
            new SymbolicRegex(Deserialize(serializedNonBacktrackingRegex));

        private sealed class SymbolicRegex : Regex
        {
            public SymbolicRegex(SymbolicRegexRunner runner) =>
                factory = new SymbolicRegexRunnerFactory(runner);
        }

        public void SaveDGML(TextWriter writer, int bound, bool hideStateInfo, bool addDotStar, bool inReverse, bool onlyDFAinfo, int maxLabelLength) => _matcher.SaveDGML(writer, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength);

        protected override void Go() => throw NotSupported();
        protected override bool FindFirstChar() => throw NotSupported();
        protected override void InitTrackCount() => throw NotSupported();
        private static Exception NotSupported()
        {
            Debug.Fail("Should never be invoked.  Only applicable to the base ScanInternal implementations.");
            throw new NotSupportedException();
        }
    }
}
