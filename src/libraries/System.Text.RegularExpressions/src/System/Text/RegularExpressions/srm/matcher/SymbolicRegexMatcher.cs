// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>Represents a precompiled form of a regex that implements match generation using symbolic derivatives.</summary>
    /// <typeparam name="TSetType">character set type</typeparam>
    internal sealed class SymbolicRegexMatcher<TSetType> : IMatcher
    {
        private const int NoMatchExists = -2;
        private const int StateMaxBound = 10000;
        private const int StateBoundLeeway = 1000;
        private const int StateCount = 5;

        internal readonly SymbolicRegexBuilder<TSetType> _builder;

        /// <summary>Maps each character into a partition id in the range 0..K-1.</summary>
        private readonly Classifier _partitions;

        /// <summary>Original regex.</summary>
        internal readonly SymbolicRegexNode<TSetType> _pattern;

        /// <summary>Reverse(<see cref="_pattern"/>).</summary>
        internal readonly SymbolicRegexNode<TSetType> _reversePattern;

        /// <summary>.*<see cref="_pattern"/> start regex</summary>
        internal readonly SymbolicRegexNode<TSetType> _dotstarredPattern;

        /// <summary>The RegexOptions this regex was created with</summary>
        internal RegexOptions Options { get; }

        /// <summary>Corresponding timeout in ms.</summary>
        private readonly int _timeout;
        private readonly bool _checkTimeout;

        /// <summary>Set of elements that matter as first element of A.</summary>
        internal readonly BooleanClassifier _startSetClassifier;

        /// <summary>Predicate over characters that make some progress</summary>
        private readonly TSetType _startSet;

        /// <summary>Maximum allowed size of <see cref="_startSetArray"/>.</summary>
        private const int StartSetArrayMaxSize = StateCount;

        /// <summary>String of at most <see cref="StartSetArrayMaxSize"/> many characters</summary>
        private readonly char[] _startSetArray;

        /// <summary>Number of elements in <see cref="_startSetClassifier"/></summary>
        private readonly int _startSetSize;

        /// <summary>If nonempty then <see cref="_pattern"/> has that fixed prefix</summary>
        private readonly string _prefix;

        /// <summary>Non-null when <see cref="_prefix"/> is nonempty</summary>
        private readonly RegexBoyerMoore _prefixBoyerMoore;

        /// <summary>If true then the fixed prefix of <see cref="_pattern"/> is idependent of case</summary>
        private readonly bool _isPrefixCaseInsensitive;

        /// <summary>Cached skip states from the initial state of <see cref="_dotstarredPattern"/> for the 6 possible previous character kinds.</summary>
        private readonly State<TSetType>[] _prefixSkipStates = new State<TSetType>[StateCount];

        private readonly string _reversePrefix;

        private readonly State<TSetType>[] _initialStates = new State<TSetType>[StateCount];
        private readonly State<TSetType>[] _dotstarredInitialStates = new State<TSetType>[StateCount];
        private readonly State<TSetType>[] _reverseInitialStates = new State<TSetType>[StateCount];

        private readonly uint[] _asciiCharKinds = new uint[128];

        internal readonly CultureInfo _culture;

        private State<TSetType> GetSkipState(uint prevCharKind)
        {
            if (_prefixSkipStates[prevCharKind] is not State<TSetType> state)
            {
                state = DeltaPlus<BrzozowskiTransition>(_prefix, _dotstarredInitialStates[prevCharKind]);
                lock (this)
                {
                    if (_prefixSkipStates[prevCharKind] is State<TSetType> existingState)
                    {
                        state = existingState;
                    }
                    else
                    {
                        _prefixSkipStates[prevCharKind] = state;
                    }
                }
            }

            return state;
        }

        /// <summary>Cached skip states from the initial state of Ar for the 6 possible previous character kinds.</summary>
        private readonly State<TSetType>[] _reversePrefixSkipStates = new State<TSetType>[6];

        private State<TSetType> GetReverseSkipState(uint prevCharKind)
        {
            if (_reversePrefixSkipStates[prevCharKind] is null)
            {
                State<TSetType> state = DeltaPlus<BrzozowskiTransition>(_reversePrefix, _reverseInitialStates[prevCharKind]);
                lock (this)
                {
                    if (_reversePrefixSkipStates[prevCharKind] is State<TSetType> existingState)
                    {
                        state = existingState;
                    }
                    else
                    {
                        _reversePrefixSkipStates[prevCharKind] = state;
                    }
                }
            }
            return _reversePrefixSkipStates[prevCharKind];
        }

        /// <summary>Get the atom of character c</summary>
        /// <param name="c">character code</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TSetType GetAtom(int c) => _builder._atoms[_partitions.Find(c)];

        #region custom serialization/deserialization
        /// <summary>
        /// Append the custom format of this matcher into sb. All characters are in visible ASCII.
        /// Main fragments are separated by a custom separator character not used in any individual fragment.
        /// </summary>
        public void Serialize(StringBuilder sb)
        {
            //-----------------------------------0
            sb.Append(_culture.Name);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------1
            _builder._solver.Serialize(sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------2
            _pattern.Serialize(sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------3
            Base64.Encode((int)Options, sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------4
            _builder._solver.SerializePredicate(_builder._wordLetterPredicate, sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------5
            _builder._solver.SerializePredicate(_builder._newLinePredicate, sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------6
            _builder._solver.SerializePredicate(_startSet, sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------7
            _startSetClassifier.Serialize(sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------8
            Base64.Encode(_startSetSize, sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------9
            Base64.Encode(_startSetArray, sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------10
            Base64.Encode(_prefix, sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------11
            sb.Append(_isPrefixCaseInsensitive);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------12
            Base64.Encode(_reversePrefix, sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------13
            _partitions.Serialize(sb);
            sb.Append(Regex.TopLevelSeparator);

            //-----------------------------------14
            if (_checkTimeout)
            {
                sb.Append(TimeSpan.FromMilliseconds(_timeout).ToString());
            }
        }

        /// <summary>Invoked by Regex.Deserialize</summary>
        internal SymbolicRegexMatcher(ICharAlgebra<TSetType> solver, string[] fragments)
        {
            // deserialize the components in the same order they were serialized
            // fragments[1] contains info that was used to construct the solver
            _culture = fragments[0] == string.Empty ?
                CultureInfo.InvariantCulture :
                (fragments[0] == CultureInfo.CurrentCulture.Name ? CultureInfo.CurrentCulture : new CultureInfo(fragments[0]));

            _builder = new SymbolicRegexBuilder<TSetType>(solver);
            _pattern = _builder.Deserialize(fragments[2]);

            Options = (RegexOptions)Base64.DecodeInt(fragments[3]);

            //these predicates are relevant only when anchors are used
            _builder._wordLetterPredicate = _builder._solver.DeserializePredicate(fragments[4]);
            _builder._newLinePredicate = _builder._solver.DeserializePredicate(fragments[5]);
            _startSet = _builder._solver.DeserializePredicate(fragments[6]);
            _startSetClassifier = BooleanClassifier.Deserialize(fragments[7]);
            _startSetSize = Base64.DecodeInt(fragments[8]);
            _startSetArray = Base64.DecodeCharArray(fragments[9]);
            _prefix = Base64.DecodeString(fragments[10]);
            _isPrefixCaseInsensitive = bool.Parse(fragments[11]);
            _reversePrefix = Base64.DecodeString(fragments[12]);
            _partitions = Classifier.Deserialize(fragments[13]);

            string potentialTimeout = fragments[14].TrimEnd();
            if (potentialTimeout == string.Empty)
            {
                _timeout = System.Threading.Timeout.Infinite;
                _checkTimeout = false;
            }
            else
            {
                TimeSpan timeout = TimeSpan.Parse(potentialTimeout);
                _checkTimeout = true;
                _timeout = (int)(timeout.TotalMilliseconds + 0.5); // Round up, so it will at least 1ms;
            }

            if (_pattern._info.ContainsSomeAnchor)
            {
                //line anchors are being used when builder.newLinePredicate is different from False
                if (!_builder._newLinePredicate.Equals(_builder._solver.False))
                {
                    _asciiCharKinds['\n'] = CharKind.Newline;
                }

                //word boundary is being used when builder.wordLetterPredicate is different from False
                if (!_builder._wordLetterPredicate.Equals(_builder._solver.False))
                {
                    _asciiCharKinds['_'] = CharKind.WordLetter;
                    _asciiCharKinds.AsSpan('0', 9).Fill(CharKind.WordLetter);
                    _asciiCharKinds.AsSpan('A', 26).Fill(CharKind.WordLetter);
                    _asciiCharKinds.AsSpan('a', 26).Fill(CharKind.WordLetter);
                }
            }

            _dotstarredPattern = _builder.MkConcat(_builder._dotStar, _pattern);
            _reversePattern = _pattern.Reverse();
            ConfigureeRegexes();
            _prefixBoyerMoore = InitializePrefixBoyerMoore();
        }
        #endregion

        /// <summary>Constructs matcher for given symbolic regex.</summary>
        internal SymbolicRegexMatcher(SymbolicRegexNode<TSetType> sr, CharSetSolver css, BDD[] minterms, RegexOptions options, TimeSpan matchTimeout, CultureInfo culture)
        {
            _pattern = sr;
            _builder = sr._builder;

            Options = options;

            _checkTimeout = RegularExpressions.Regex.InfiniteMatchTimeout != matchTimeout;
            _timeout = (int)(matchTimeout.TotalMilliseconds + 0.5); // Round up, so it will be at least 1ms
            _culture = culture;

            Debug.Assert(_builder._solver is BV64Algebra or BVAlgebra or CharSetSolver, $"Unsupported algebra: {_builder._solver}");
            _partitions = _builder._solver switch
            {
                BV64Algebra bv64 => bv64._classifier,
                BVAlgebra bv => bv._classifier,
                _ => Classifier.Create((CharSetSolver)(object)_builder._solver, minterms),
            };

            _dotstarredPattern = _builder.MkConcat(_builder._dotStar, _pattern);
            _reversePattern = _pattern.Reverse();
            ConfigureeRegexes();

            _startSet = _pattern.GetStartSet();
            if (!_builder._solver.IsSatisfiable(_startSet) || _pattern.CanBeNullable)
            {
                // If the startset is empty make it full instead by including all characters
                // this is to ensure that startset is nonempty -- as an invariant assumed by operations using it
                //
                // Also, if A can be nullable then effectively disable use of startset by making it true
                // because it may force search of next character in startset and fail to recognize an empty match
                // because (by definition) an empty match has no start character.
                //
                // For example (this is also a unit test):
                // for pattern "\B\W*?" or "\B\W*" or "\B\W?" and input "e.g:abc" there is an empty match in position 5
                // but startset \W will force search beyond position 5 and fails to find that match
                _startSet = _builder._solver.True;
            }

            _startSetSize = (int)_builder._solver.ComputeDomainSize(_startSet);

            BDD startbdd = _builder._solver.ConvertToCharSet(css, _startSet);
            _startSetClassifier = BooleanClassifier.Create(css, startbdd);

            //store the start characters in the A_startset_array if there are not too many characters
            _startSetArray = _startSetSize <= StartSetArrayMaxSize ?
                new List<char>(css.GenerateAllCharacters(startbdd)).ToArray() :
                Array.Empty<char>();

            _prefix = _pattern.GetFixedPrefix(css, culture.Name, out _isPrefixCaseInsensitive);
            _reversePrefix = _reversePattern.GetFixedPrefix(css, culture.Name, out _);

            _prefixBoyerMoore = InitializePrefixBoyerMoore();

            if (_pattern._info.ContainsSomeAnchor)
            {
                for (int i = 0; i < 128; i++)
                {
                    TSetType predicate2;
                    uint charKind;

                    if (i == '\n')
                    {
                        predicate2 = _builder._newLinePredicate;
                        charKind = CharKind.Newline;
                    }
                    else
                    {
                        predicate2 = _builder._wordLetterPredicate;
                        charKind = CharKind.WordLetter;
                    }

                    _asciiCharKinds[i] = _builder._solver.And(GetAtom(i), predicate2).Equals(_builder._solver.False) ? 0 : charKind;
                }
            }
        }

        private RegexBoyerMoore? InitializePrefixBoyerMoore()
        {
            if (_prefix != string.Empty && _prefix.Length <= RegexBoyerMoore.MaxLimit && _prefix.Length > 1)
            {
                // RegexBoyerMoore expects the prefix to be lower case when case is ignored.
                // Use the culture of the matcher.
                string prefix = _isPrefixCaseInsensitive ? _prefix.ToLower(_culture) : _prefix;
                return new RegexBoyerMoore(prefix, _isPrefixCaseInsensitive, rightToLeft: false, _culture);
            }

            return null;
        }

        private void ConfigureeRegexes()
        {
            void Configure(uint i)
            {
                _initialStates[i] = _builder.MkState(_pattern, i);

                // Used to detect if initial state was reentered, then startset can be triggered
                // but observe that the behavior from the state may ultimately depend on the previous
                // input char e.g. possibly causing nullability of \b or \B or of a start-of-line anchor,
                // in that sense there can be several "versions" (not more than StateCount) of the initial state.
                _dotstarredInitialStates[i] = _builder.MkState(_dotstarredPattern, i);
                _dotstarredInitialStates[i].IsInitialState = true;

                _reverseInitialStates[i] = _builder.MkState(_reversePattern, i);
            }

            // Create initial states for A, A1 and Ar.
            if (!_pattern._info.ContainsSomeAnchor)
            {
                // Only the default previous character kind 0 is ever going to be used for all initial states.
                // _A1q0[0] is recognized as special initial state.
                // This information is used for search optimization based on start set and prefix of A.
                Configure(0);
            }
            else
            {
                for (uint i = 0; i < StateCount; i++)
                {
                    Configure(i);
                }
            }
        }

        /// <summary>Return the state after the given input string from the given state q.</summary>
        private State<TSetType> DeltaPlus<TTransition>(string input, State<TSetType> q) where TTransition : struct, ITransition
        {
            for (int i = 0; i < input.Length; i++)
            {
                q = Delta<TTransition>(input, i, q);
            }

            return q;
        }

        /// <summary>Interface for transitions used by the Delta method.</summary>
        private interface ITransition
        {
            /// <summary>Find the next state given the current state and next character.</summary>
            /// <param name="matcher">the current matcher object</param>
            /// <param name="q">the current state</param>
            /// <param name="atom_id">the partition id of the next character</param>
            /// <param name="atom">the partition of the next character</param>
            State<TSetType> TakeTransition(SymbolicRegexMatcher<TSetType> matcher, State<TSetType> q, int atom_id, TSetType atom);
        }

        /// <summary>Compute the target state for source state q and input[i] character.</summary>
        /// <param name="input">input string</param>
        /// <param name="i">refers to i'th character in the input</param>
        /// <param name="q">source state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private State<TSetType> Delta<TTransition>(string input, int i, State<TSetType> q) where TTransition : struct, ITransition
        {
            int c = input[i];

            // atom_id = atoms.Length represents \Z (last \n)
            int atom_id = c == '\n' && i == input.Length - 1 && q.StartsWithLineAnchor ?
                _builder._atoms.Length :
                _partitions.Find(c);

            // atom=False represents \Z
            TSetType atom = atom_id == _builder._atoms.Length ?
                _builder._solver.False :
                _builder._atoms[atom_id];

            return default(TTransition).TakeTransition(this, q, atom_id, atom);
        }

        /// <summary>Transition for Brzozowski-style derivatives (i.e. a DFA).</summary>
        private readonly struct BrzozowskiTransition : ITransition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public State<TSetType> TakeTransition(SymbolicRegexMatcher<TSetType> matcher, State<TSetType> q, int atom_id, TSetType atom)
            {
                int offset = (q.Id << matcher._builder._K) | atom_id;
                return
                    matcher._builder._delta[offset] ??
                    matcher.CreateNewTransition(q, atom, offset);
            }
        }

        /// <summary>Transition for Antimirov-style derivatives (i.e. an NFA).</summary>
        private readonly struct AntimirovTransition : ITransition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public State<TSetType> TakeTransition(SymbolicRegexMatcher<TSetType> matcher, State<TSetType> q, int atom_id, TSetType atom)
            {
                if (q.Node.Kind == SymbolicRegexKind.Or)
                {
                    SymbolicRegexNode<TSetType> union = matcher._builder._nothing;
                    uint kind = 0;

                    // consider transitions from the members one at a time
                    foreach (SymbolicRegexNode<TSetType> r in q.Node._alts)
                    {
                        State<TSetType> s = matcher._builder.MkState(r, q.PrevCharKind);
                        int offset = (s.Id << matcher._builder._K) | atom_id;
                        State<TSetType> p = matcher._builder._delta[offset] ?? matcher.CreateNewTransition(s, atom, offset);

                        // observe that if p.Node is an Or it will be flattened
                        union = matcher._builder.MkOr2(union, p.Node);

                        // kind is just the kind of the atom
                        kind = p.PrevCharKind;
                    }

                    return matcher._builder.MkState(union, kind, true);
                }

                return default(BrzozowskiTransition).TakeTransition(matcher, q, atom_id, atom);
            }
        }

        /// <summary>Critical region for defining a new transition</summary>
        private State<TSetType> CreateNewTransition(State<TSetType> q, TSetType atom, int offset)
        {
            lock (this)
            {
                // check if meanwhile delta[offset] has become defined possibly by another thread
                State<TSetType> p = _builder._delta[offset];
                if (p is null)
                {
                    // this is the only place in code where the Next method is called in the matcher
                    _builder._delta[offset] = p = q.Next(atom);

                    // switch to antimirov mode if the maximum bound has been reached
                    if (p.Id == StateMaxBound)
                    {
                        _builder._antimirov = true;
                    }
                }

                return p;
            }
        }

        private void DoCheckTimeout(int timeoutOccursAt)
        {
            // This code is identical to RegexRunner.DoCheckTimeout(),
            // with the exception of check skipping. RegexRunner calls
            // DoCheckTimeout potentially on every iteration of a loop,
            // whereas this calls it only once per transition.

            int currentMillis = Environment.TickCount;

            if (currentMillis < timeoutOccursAt)
                return;

            if (0 > timeoutOccursAt && 0 < currentMillis)
                return;

            //regex pattern is in general not available in srm and
            //the input is not available here but could be passed as argument to DoCheckTimeout
            throw new RegexMatchTimeoutException(string.Empty, string.Empty, TimeSpan.FromMilliseconds(_timeout));
        }

        #region match generation
        /// <summary>Find a match.</summary>
        /// <param name="quick">if true return null iff there exists a match</param>
        /// <param name="input">input string</param>
        /// <param name="startat">the position to start search in the input string</param>
        /// <param name="k">the next position after the end position in the input</param>
        public Match FindMatch(bool quick, string input, int startat, int k)
        {
            int timeoutOccursAt = 0;
            if (_checkTimeout)
            {
                // Using Environment.TickCount for efficiency instead of Stopwatch -- as in the non-DFA case.
                timeoutOccursAt = Environment.TickCount + (int)(_timeout + 0.5);
            }

            if (startat == k)
            {
                //covers the special case when the remaining input suffix
                //where a match is sought is empty (for example when the input is empty)
                //in this case the only possible match is an empty match
                uint prevKind = GetCharKind(input, startat - 1);
                uint nextKind = GetCharKind(input, startat);

                bool emptyMatchExists = _pattern.IsNullableFor(CharKind.Context(prevKind, nextKind));
                if (emptyMatchExists)
                {
                    return quick ? null : new Match(startat, 0);
                }

                return Match.NoMatch;
            }

            // Find the first accepting state. Initial start position in the input is i == 0.
            int i = startat;

            // May return -1 as a legitimate value when the initial state is nullable and startat == 0.
            // Returns NoMatchExists when there is no match.
            i = FindFinalStatePosition(input, k, i, timeoutOccursAt, out int i_q0_A1, out int watchdog);

            if (i == NoMatchExists)
            {
                return Match.NoMatch;
            }

            if (quick)
            {
                // this means success -- the original call was IsMatch
                return null;
            }

            int i_start;
            int i_end;

            if (watchdog >= 0)
            {
                i_start = i - watchdog + 1;
                i_end = i;
            }
            else
            {
                if (i < startat)
                {
                    Debug.Assert(i == startat - 1);
                    i_start = startat;
                }
                else
                {
                    // Walk in reverse to locate the start position of the match
                    i_start = FindStartPosition(input, i, i_q0_A1);
                }

                i_end = FindEndPosition(input, k, i_start);
            }

            return new Match(i_start, i_end + 1 - i_start);
        }

        /// <summary>Find match end position using A, end position is known to exist.</summary>
        /// <param name="input">input array</param>
        /// <param name="i">inclusive start position</param>
        /// <param name="k">exclusive end position</param>
        /// <returns></returns>
        private int FindEndPosition(string input, int k, int i)
        {
            int i_end = k;

            // Pick the correct start state based on previous character kind.
            uint prevCharKind = GetCharKind(input, i - 1);
            State<TSetType> q = _initialStates[prevCharKind];

            if (q.IsNullable(GetCharKind(input, i)))
            {
                //empty match exists because the initial state is accepting
                i_end = i - 1;

                // stop here if q is lazy
                if (q.IsLazy)
                {
                    return i_end;
                }
            }

            while (i < k)
            {
                int j = Math.Min(k, i + StateBoundLeeway);
                bool done = _builder._antimirov ?
                    FindEndPositionDeltas<AntimirovTransition>(input, ref i, j, ref q, ref i_end) :
                    FindEndPositionDeltas<BrzozowskiTransition>(input, ref i, j, ref q, ref i_end);

                if (done)
                {
                    break;
                }
            }

            Debug.Assert(i_end != k);
            return i_end;
        }

        // Inner loop for FindEndPosition parameterized by an ITransition type.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FindEndPositionDeltas<TTransition>(string input, ref int i, int j, ref State<TSetType> q, ref int i_end) where TTransition : struct, ITransition
        {
            do
            {
                q = Delta<TTransition>(input, i, q);

                if (q.IsNullable(GetCharKind(input, i + 1)))
                {
                    // stop here if q is lazy
                    if (q.IsLazy)
                    {
                        i_end = i;
                        return true;
                    }

                    // Accepting state has been reached. Record the position.
                    i_end = i;
                }
                else if (q.IsDeadend)
                {
                    // Nonaccepting sink state (deadend) has been reached in A.
                    // So the match ended when the last i_end was updated.
                    return true;
                }

                i += 1;
            }
            while (i < j);

            return false;
        }

        /// <summary>Walk back in reverse using Ar to find the start position of match, start position is known to exist.</summary>
        /// <param name="input">the input string</param>
        /// <param name="i">position to start walking back from, i points at the last character of the match</param>
        /// <param name="match_start_boundary">do not pass this boundary when walking back</param>
        /// <returns></returns>
        private int FindStartPosition(string input, int i, int match_start_boundary)
        {
            // Fetch the correct start state for Ar.
            // This depends on previous character --- which, because going backwards, is character number i+1.
            uint prevKind = GetCharKind(input, i + 1);
            State<TSetType> q = _reverseInitialStates[prevKind];

            // Ar may have a fixed prefix sequence
            if (_reversePrefix.Length > 0)
            {
                //skip past the prefix portion of Ar
                q = GetReverseSkipState(prevKind);
                i -= _reversePrefix.Length;
            }

            if (i == -1)
            {
                Debug.Assert(q.IsNullable(GetCharKind(input, i)), "we reached the beginning of the input, thus the state q must be accepting");
                return 0;
            }

            int last_start = -1;
            if (q.IsNullable(GetCharKind(input, i)))
            {
                // The whole prefix of Ar was in reverse a prefix of A,
                // for example when the pattern of A is concrete word such as "abc"
                last_start = i + 1;
            }

            //walk back to the accepting state of Ar
            while (i >= match_start_boundary)
            {
                int j = Math.Max(match_start_boundary, i - StateBoundLeeway);
                bool done = _builder._antimirov ?
                    FindStartPositionDeltas<AntimirovTransition>(input, ref i, j, ref q, ref last_start) :
                    FindStartPositionDeltas<BrzozowskiTransition>(input, ref i, j, ref q, ref last_start);

                if (done)
                {
                    break;
                }
            }

            Debug.Assert(last_start != -1);
            return last_start;
        }

        // Inner loop for FindStartPosition parameterized by an ITransition type.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FindStartPositionDeltas<TTransition>(string input, ref int i, int j, ref State<TSetType> q, ref int last_start) where TTransition : struct, ITransition
        {
            do
            {
                q = Delta<TTransition>(input, i, q);

                // Reached a deadend state, thus the earliest match start point must have occurred already.
                if (q.IsNothing)
                {
                    return true;
                }

                if (q.IsNullable(GetCharKind(input, i - 1)))
                {
                    // Earliest start point so far. This must happen at some point
                    // or else A1 would not have reached a final state after match_start_boundary.
                    last_start = i;
                }

                i -= 1;
            }
            while (i > j);

            return false;
        }

        /// <summary>Returns NoMatchExists if no match exists. Returns -1 when i=0 and the initial state is nullable.</summary>
        /// <param name="input">given input string</param>
        /// <param name="k">input length or bounded input length</param>
        /// <param name="i">start position</param>
        /// <param name="timeoutOccursAt">The time at which timeout occurs, if timeouts are being checked.</param>
        /// <param name="initialStateIndex">last position the initial state of <see cref="_dotstarredPattern"/> was visited</param>
        /// <param name="watchdog">length of match when positive</param>
        private int FindFinalStatePosition(string input, int k, int i, int timeoutOccursAt, out int initialStateIndex, out int watchdog)
        {
            // Get the correct start state of A1, which in general depends on the previous character kind in the input.
            uint prevCharKindId = GetCharKind(input, i - 1);
            State<TSetType> q = _dotstarredInitialStates[prevCharKindId];
            initialStateIndex = i;

            if (q.IsNothing)
            {
                // If q is nothing then it is a deadend from the beginning this happens for example when the original
                // regex started with start anchor and prevCharKindId is not Start
                watchdog = -1;
                return NoMatchExists;
            }

            if (q.IsNullable(GetCharKind(input, i)))
            {
                // The initial state is nullable in this context so at least an empty match exists.
                // The last position of the match is i-1 because the match is empty.
                // This value is -1 if i == 0.
                watchdog = -1;
                return i - 1;
            }

            watchdog = -1;

            // Search for a match end position within input[i..k-1]
            while (i < k)
            {
                if (q.IsInitialState)
                {
                    // i_q0_A1 is the most recent position in the input when A1 is in the initial state
                    initialStateIndex = i;

                    if (_prefixBoyerMoore != null)
                    {
                        #region prefix optimization
                        // Stay in the initial state if the prefix does not match.
                        // Thus advance the current position to the first position where the prefix does match.
                        i = _prefixBoyerMoore.Scan(input, i, 0, input.Length);

                        if (i == -1) // Scan returns -1 when a matching position does not exist
                        {
                            watchdog = -1;
                            return -2;
                        }

                        // Compute the end state for the A prefix.
                        // Skip directly to the resulting state
                        //  --- i.e. do the loop ---
                        // for (int j = 0; j < prefix.Length; j++)
                        //     q = Delta(prefix[j], q, out regex);
                        //  ---
                        q = GetSkipState(q.PrevCharKind);

                        // skip the prefix
                        i += _prefix.Length;

                        // here i points at the next character (the character immediately following the prefix)
                        if (q.IsNullable(GetCharKind(input, i)))
                        {
                            // Return the last position of the match
                            watchdog = q.WatchDog;
                            return i - 1;
                        }

                        if (i == k)
                        {
                            // no match was found
                            return -2;
                        }
                        #endregion
                    }
                    else
                    {
                        // we are still in the initial state, when the prefix is empty
                        // find the first position i that matches with some character in the start set
                        i = IndexOfStartset(input, i);

                        if (i == -1)
                        {
                            // no match was found
                            return NoMatchExists;
                        }

                        initialStateIndex = i;

                        // the start state must be updated
                        // to reflect the kind of the previous character
                        // when anchors are not used, q will remain the same state
                        q = _dotstarredInitialStates[GetCharKind(input, i - 1)];
                        if (q.IsNothing)
                        {
                            return NoMatchExists;
                        }
                    }
                }

                int result;
                int j = Math.Min(k, i + StateBoundLeeway);
                bool done = _builder._antimirov ?
                    FindFinalStatePositionDeltas<AntimirovTransition>(input, j, ref i, ref q, ref watchdog, out result) :
                    FindFinalStatePositionDeltas<BrzozowskiTransition>(input, j, ref i, ref q, ref watchdog, out result);

                if (done)
                {
                    return result;
                }

                if (_checkTimeout)
                {
                    DoCheckTimeout(timeoutOccursAt);
                }
            }

            //no match was found
            return NoMatchExists;
        }

        // Inner loop for FindFinalStatePosition parameterized by an ITransition type.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FindFinalStatePositionDeltas<TTransition>(string input, int j, ref int i, ref State<TSetType> q, ref int watchdog, out int result) where TTransition : struct, ITransition
        {
            do
            {
                // make the transition based on input[i]
                q = Delta<TTransition>(input, i, q);

                if (q.IsNullable(GetCharKind(input, i + 1)))
                {
                    watchdog = q.WatchDog;
                    result = i;
                    return true;
                }

                if (q.IsNothing)
                {
                    // q is a deadend state so any further search is meaningless
                    result = NoMatchExists;
                    return true;
                }

                // continue from the next character
                i += 1;
            }
            while (i < j && !q.IsInitialState);

            result = -3; // This value does not get used
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint GetCharKind(string input, int i)
        {
            if (_pattern._info.ContainsSomeAnchor)
            {
                if (i == -1 || i == input.Length)
                {
                    return CharKind.StartStop;
                }

                char nextChar = input[i];
                if (nextChar == '\n')
                {
                    return
                        _builder._newLinePredicate.Equals(_builder._solver.False) ? 0 : // ignore \n
                        i == 0 || i == input.Length - 1 ? CharKind.NewLineS : // very first or very last \n. Detection of very first \n is needed for rev(\Z).
                        CharKind.Newline;
                }

                return
                    nextChar < 128 ? _asciiCharKinds[nextChar] :
                    _builder._solver.And(GetAtom(nextChar), _builder._wordLetterPredicate).Equals(_builder._solver.False) ? 0 : //apply the wordletter predicate to compute the kind of the next character
                    CharKind.WordLetter;
            }

            // The previous character kind is irrelevant when anchors are not used.
            return 0;
        }

        #endregion

        #region Specialized IndexOf
        /// <summary>
        /// Find first occurrence of startset element in input starting from index i.
        /// Startset here is assumed to consist of a few characters.
        /// </summary>
        /// <param name="input">input string to search in</param>
        /// <param name="i">the start index in input to search from</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexOfStartset(string input, int i)
        {
            if (_startSetSize <= StartSetArrayMaxSize)
            {
                return input.IndexOfAny(_startSetArray, i);
            }

            for (int j = i; j < input.Length; j++)
            {
                if (_startSetClassifier.Contains(input[j]))
                {
                    return j;
                }
            }

            return -1;
        }

        public void SaveDGML(TextWriter writer, int bound = 0, bool hideStateInfo = false, bool addDotStar = false, bool inReverse = false, bool onlyDFAinfo = false, int maxLabelLength = 500)
        {
            var graph = new DGML.RegexDFA<TSetType>(this, bound, addDotStar, inReverse);
            var dgml = new DGML.DgmlWriter(writer, hideStateInfo, maxLabelLength, onlyDFAinfo);
            dgml.Write(graph);
        }

        #endregion
    }
}
