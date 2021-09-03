// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Represents a precompiled form of a regex that implements match generation using symbolic derivatives.
    /// </summary>
    /// <typeparam name="S">character set type</typeparam>
    internal partial class SymbolicRegexMatcher<S> : IMatcher
    {
        internal readonly SymbolicRegexBuilder<S> _builder;

        /// <summary>
        /// Maps each character into a partition id in the range 0..K-1.
        /// </summary>
        private readonly Classifier _partitions;

        /// <summary>
        /// Original regex.
        /// </summary>
        internal readonly SymbolicRegexNode<S> _pattern;

        /// <summary>
        /// Reverse regex.
        /// </summary>
        internal SymbolicRegexNode<S> _reversePattern;

        /// <summary>
        /// Regex with a .* in the beginning.
        /// </summary>
        internal SymbolicRegexNode<S> _dotstarredPattern;

        /// <summary>
        /// The RegexOptions this regex was created with
        /// </summary>
        internal RegexOptions _options { get; }

        /// <summary>
        /// Timeout for matching.
        /// </summary>
        private readonly TimeSpan _matchTimeout;
        /// <summary>
        /// corresponding timeout in ms
        /// </summary>
        private readonly int _timeout;
        private int _timeoutOccursAt;
        private readonly bool _checkTimeout;

        /// <summary>
        /// predicate over characters that make some progress
        /// </summary>
        private readonly S _startSet;

        /// <summary>
        /// Set of elements that matter as first element of A.
        /// </summary>
        internal BooleanClassifier _startSetClassifier;

        /// <summary>
        /// maximum allowed size of A_startset_array
        /// </summary>
        private const int StartSetArrayMaxSize = 5;

        /// <summary>
        /// string of at most s_A_startset_array_max_size many characters
        /// </summary>
        private readonly char[] _startSetArray;

        /// <summary>
        /// Number of elements in A_StartSet
        /// </summary>
        private readonly int _startSetSize;

        /// <summary>
        /// if nonempty then A has that fixed prefix
        /// </summary>
        private readonly string _prefix;

        /// <summary>
        /// non-null when A_prefix is nonempty
        /// </summary>
        private RegexBoyerMoore _prefixBoyerMoore;

        /// <summary>
        /// if true then the fixed prefix of A is idependent of case
        /// </summary>
        private readonly bool _isPrefixCaseInsensitive;

        /// <summary>
        /// Cached skip states from the initial state of A1 for the 6 possible previous character kinds.
        /// </summary>
        private readonly State<S>[] _prefixSkipStates = new State<S>[5];

        private State<S> GetSkipState(uint prevCharKind)
        {
            if (_prefixSkipStates[prevCharKind] is null)
            {
                State<S> state = DeltaPlus<BrzozowskiTransition>(_prefix, _dotstarredInitialStates[prevCharKind]);
                lock (this)
                {
                    _prefixSkipStates[prevCharKind] ??= state;
                }
            }

            return _prefixSkipStates[prevCharKind];
        }

        private readonly string _reversePrefix;

        /// <summary>
        /// Cached skip states from the initial state of Ar for the 6 possible previous character kinds.
        /// </summary>
        private readonly State<S>[] _reversePrefixSkipStates = new State<S>[6];

        private State<S> GetReverseSkipState(uint prevCharKind)
        {
            if (_reversePrefixSkipStates[prevCharKind] is null)
            {
                State<S> state = DeltaPlus<BrzozowskiTransition>(_reversePrefix, _reverseInitialStates[prevCharKind]);
                lock (this)
                {
                    _reversePrefixSkipStates[prevCharKind] ??= state;
                }
            }
            return _reversePrefixSkipStates[prevCharKind];
        }

        private readonly State<S>[] _initialStates = new State<S>[5];

        private readonly State<S>[] _dotstarredInitialStates = new State<S>[5];

        private readonly State<S>[] _reverseInitialStates = new State<S>[5];

        private readonly uint[] _asciiCharKinds = new uint[128];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal State<S> GetState(int stateId) =>
            // the builder maintains a mapping
            // from stateIds to states
            _builder._statearray[stateId];

        /// <summary>
        /// Get the atom of character c
        /// </summary>
        /// <param name="c">character code</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private S GetAtom(int c) => _builder._atoms[_partitions.Find(c)];

        private const int StateMaxBound = 10000;
        private const int StateBoundLeeway = 1000;

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
            Base64.Encode((int)_options, sb);
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
                sb.Append(_matchTimeout.ToString());
        }

        /// <summary>
        /// Invoked by Regex.Deserialize
        /// </summary>
        internal SymbolicRegexMatcher(ICharAlgebra<S> solver, string[] fragments)
        {
            //deserialize the components in the same order they were serialized
            //fragments[1] contains info that was used to construct the solver
            _culture = fragments[0] == string.Empty ? CultureInfo.InvariantCulture :
                (fragments[0] == CultureInfo.CurrentCulture.Name ? CultureInfo.CurrentCulture : new CultureInfo(fragments[0]));
            _builder = new SymbolicRegexBuilder<S>(solver);
            _pattern = _builder.Deserialize(fragments[2]);
            _options = (RegexOptions)Base64.DecodeInt(fragments[3]);
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
                _matchTimeout = RegularExpressions.Regex.InfiniteMatchTimeout;
                _checkTimeout = false;
            }
            else
            {
                _matchTimeout = TimeSpan.Parse(potentialTimeout);
                _checkTimeout = true;
                _timeout = (int)(_matchTimeout.TotalMilliseconds + 0.5); // Round up, so it will at least 1ms;
                _timeoutChecksToSkip = TimeoutCheckFrequency;
            }
            if (_pattern._info.ContainsSomeAnchor)
            {
                //line anchors are being used when builder.newLinePredicate is different from False
                if (!_builder._newLinePredicate.Equals(_builder._solver.False))
                    _asciiCharKinds[10] = CharKind.Newline;

                //word boundary is being used when builder.wordLetterPredicate is different from False
                if (!_builder._wordLetterPredicate.Equals(_builder._solver.False))
                {
                    _asciiCharKinds['_'] = CharKind.WordLetter;
                    _asciiCharKinds.AsSpan('0', 9).Fill(CharKind.WordLetter);
                    _asciiCharKinds.AsSpan('A', 26).Fill(CharKind.WordLetter);
                    _asciiCharKinds.AsSpan('a', 26).Fill(CharKind.WordLetter);
                }
            }
            InitializeRegexes();
            InitializePrefixBoyerMoore();
        }
        #endregion

        internal CultureInfo _culture;

        /// <summary>
        /// Constructs matcher for given symbolic regex
        /// </summary>
        internal SymbolicRegexMatcher(SymbolicRegexNode<S> sr, CharSetSolver css, BDD[] minterms, RegexOptions options, TimeSpan matchTimeout, CultureInfo culture)
        {
            _culture = culture;
            _matchTimeout = matchTimeout;
            _checkTimeout = RegularExpressions.Regex.InfiniteMatchTimeout != _matchTimeout;
            _timeout = (int)(matchTimeout.TotalMilliseconds + 0.5); // Round up, so it will at least 1ms;
            _timeoutChecksToSkip = TimeoutCheckFrequency;

            _options = options;
            _builder = sr._builder;
            if (_builder._solver is BV64Algebra)
            {
                BV64Algebra bva = _builder._solver as BV64Algebra;
                _partitions = bva._classifier;
            }
            else if (_builder._solver is BVAlgebra)
            {
                BVAlgebra bva = _builder._solver as BVAlgebra;
                _partitions = bva._classifier;
            }
            else if (_builder._solver is CharSetSolver)
            {
                _partitions = Classifier.Create(_builder._solver as CharSetSolver, minterms);
            }
            else
            {
                throw new NotSupportedException($"only {nameof(BV64Algebra)} or {nameof(BVAlgebra)} or {nameof(CharSetSolver)} algebra is supported");
            }

            _pattern = sr;

            InitializeRegexes();

            _startSet = _pattern.GetStartSet();
            if (!_builder._solver.IsSatisfiable(_startSet) || _pattern.CanBeNullable)
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

            _startSetSize = (int)_builder._solver.ComputeDomainSize(_startSet);

            BDD startbdd = _builder._solver.ConvertToCharSet(css, _startSet);
            _startSetClassifier = BooleanClassifier.Create(css, startbdd);
            //store the start characters in the A_startset_array if there are not too many characters
            _startSetArray = _startSetSize <= StartSetArrayMaxSize ?
                new List<char>(css.GenerateAllCharacters(startbdd)).ToArray() :
                Array.Empty<char>();

            _prefix = _pattern.GetFixedPrefix(css, culture.Name, out _isPrefixCaseInsensitive);
            _reversePrefix = _reversePattern.GetFixedPrefix(css, culture.Name, out _);

            InitializePrefixBoyerMoore();

            if (_pattern._info.ContainsSomeAnchor)
                for (int i = 0; i < 128; i++)
                    _asciiCharKinds[i] =
                        i == 10 ? (_builder._solver.And(GetAtom(i), _builder._newLinePredicate).Equals(_builder._solver.False) ? 0 : CharKind.Newline)
                                : (_builder._solver.And(GetAtom(i), _builder._wordLetterPredicate).Equals(_builder._solver.False) ? 0 : CharKind.WordLetter);
        }

        private void InitializePrefixBoyerMoore()
        {
            if (_prefix != string.Empty && _prefix.Length <= RegexBoyerMoore.MaxLimit && _prefix.Length > 1)
            {
                string prefix = _prefix;
                // RegexBoyerMoore expects the prefix to be lower case when case is ignored
                if (_isPrefixCaseInsensitive)
                    //use the culture of the matcher
                    prefix = _prefix.ToLower(_culture);
                _prefixBoyerMoore = new RegexBoyerMoore(prefix, _isPrefixCaseInsensitive, false, _culture);
            }
        }

        private void InitializeRegexes()
        {
            _dotstarredPattern = _builder.MkConcat(_builder._dotStar, _pattern);
            _reversePattern = _pattern.Reverse();
            // create initial states for A, A1 and Ar
            if (!_pattern._info.ContainsSomeAnchor)
            {
                // only the default previous character kind 0 is ever going to be used for all initial states
                _initialStates[0] = _builder.MkState(_pattern, 0);
                _dotstarredInitialStates[0] = _builder.MkState(_dotstarredPattern, 0);
                // _A1q0[0] is recognized as special initial state,
                // this information is used for search optimization based on start set and prefix of A
                _dotstarredInitialStates[0].IsInitialState = true;
                _reverseInitialStates[0] = _builder.MkState(_reversePattern, 0);
            }
            else
            {
                for (uint i = 0; i < 5; i++)
                {
                    _initialStates[i] = _builder.MkState(_pattern, i);
                    _dotstarredInitialStates[i] = _builder.MkState(_dotstarredPattern, i);
                    _reverseInitialStates[i] = _builder.MkState(_reversePattern, i);
                    //used to detect if initial state was reentered, then startset can be triggered
                    //but observe that the behavior from the state may ultimately depend on the previous
                    //input char e.g. possibly causing nullability of \b or \B or of a start-of-line anchor,
                    //in that sense there can be several "versions" (not more than 5) of the initial state
                    _dotstarredInitialStates[i].IsInitialState = true;
                }
            }
        }

        /// <summary>
        /// Return the state after the given input string from the given state q.
        /// </summary>
        private State<S> DeltaPlus<Transition>(string input, State<S> q) where Transition : struct, ITransition
        {
            for (int i = 0; i < input.Length; i++)
                q = Delta<Transition>(input, i, q);
            return q;
        }

        /// <summary>
        /// Interface for transitions used by the Delta method.
        /// </summary>
        private interface ITransition
        {
            /// <summary>
            /// Find the next state given the current state and next character.
            /// </summary>
            /// <param name="matcher">the current matcher object</param>
            /// <param name="q">the current state</param>
            /// <param name="atom_id">the partition id of the next character</param>
            /// <param name="atom">the partition of the next character</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            State<S> TakeTransition(SymbolicRegexMatcher<S> matcher, State<S> q, int atom_id, S atom);
        }

        /// <summary>
        /// Compute the target state for source state q and input[i] character.
        /// All uses of Delta must be inlined for efficiency.
        /// This is the purpose of the MethodImpl(MethodImplOptions.AggressiveInlining) attribute.
        /// </summary>
        /// <param name="input">input string</param>
        /// <param name="i">refers to i'th character in the input</param>
        /// <param name="q">source state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private State<S> Delta<Transition>(string input, int i, State<S> q) where Transition : struct, ITransition
        {
            int c = input[i];
            // atom_id = atoms.Length represents \Z (last \n)
            int atom_id = c == 10 && i == input.Length - 1 && q.StartsWithLineAnchor ? _builder._atoms.Length : _partitions.Find(c);
            // atom=False represents \Z
            S atom = atom_id == _builder._atoms.Length ? _builder._solver.False : _builder._atoms[atom_id];
            return default(Transition).TakeTransition(this, q, atom_id, atom);
        }

        /// <summary>
        /// Transition for Brzozowski style derivatives (i.e. a DFA).
        /// </summary>
        private struct BrzozowskiTransition : ITransition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public State<S> TakeTransition(SymbolicRegexMatcher<S> matcher, State<S> q, int atom_id, S atom)
            {
                int offset = (q.Id << matcher._builder._K) | atom_id;
                State<S> p = matcher._builder._delta[offset];
                if (p is null)
                    return matcher.CreateNewTransition(q, atom, offset);
                else
                    return p;
            }
        }

        /// <summary>
        /// Transition for Antimirov style derivatives (i.e. an NFA).
        /// </summary>
        private struct AntimirovTransition : ITransition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public State<S> TakeTransition(SymbolicRegexMatcher<S> matcher, State<S> q, int atom_id, S atom)
            {
                if (q.Node.Kind == SymbolicRegexKind.Or)
                {
                    SymbolicRegexNode<S> union = matcher._builder._nothing;
                    uint kind = 0;
                    // consider transitions from the members one at a time
                    foreach (SymbolicRegexNode<S> r in q.Node._alts)
                    {
                        State<S> s = matcher._builder.MkState(r, q.PrevCharKind);
                        int offset = (s.Id << matcher._builder._K) | atom_id;
                        State<S> p = matcher._builder._delta[offset];
                        if (p is null)
                            p = matcher.CreateNewTransition(s, atom, offset);
                        // observe that if p.Node is an Or it will be flattened
                        union = matcher._builder.MkOr2(union, p.Node);
                        // kind is just the kind of the atom
                        kind = p.PrevCharKind;
                    }
                    State<S> powerstate = matcher._builder.MkState(union, kind, true);
                    return powerstate;
                }
                else
                {
                    return default(BrzozowskiTransition).TakeTransition(matcher, q, atom_id, atom);
                }
            }
        }

        /// <summary>
        /// Critical region for threadsafe applications for defining a new transition
        /// </summary>
        private State<S> CreateNewTransition(State<S> q, S atom, int offset)
        {
            lock (this)
            {
                // check if meanwhile delta[offset] has become defined possibly by another thread
                State<S> p = _builder._delta[offset];
                if (p != null)
                    return p;
                else
                {
                    // this is the only place in code where the Next method is called in the matcher
                    p = q.Next(atom);
                    _builder._delta[offset] = p;
                    //switch to antimirov mode if the maximum bound has been reached
                    if (p.Id == StateMaxBound)
                        _builder._antimirov = true;
                    return p;
                }
            }
        }

        /// <summary>
        /// The frequence is lower in DFA mode because timeout tests are performed much
        /// less frequently here, once per transition, compared to non-DFA mode.
        /// So, e.g., 5 here imples checking after every 5 transitions.
        /// </summary>
        private const int TimeoutCheckFrequency = 5;
        private int _timeoutChecksToSkip;

        /// <summary>
        /// This code is identical to RegexRunner.DoCheckTimeout()
        /// </summary>
        private void DoCheckTimeout()
        {
            if (--_timeoutChecksToSkip != 0)
                return;

            _timeoutChecksToSkip = TimeoutCheckFrequency;

            int currentMillis = Environment.TickCount;

            if (currentMillis < _timeoutOccursAt)
                return;

            if (0 > _timeoutOccursAt && 0 < currentMillis)
                return;

            //regex pattern is in general not available in srm and
            //the input is not available here but could be passed as argument to DoCheckTimeout
            throw new RegexMatchTimeoutException(string.Empty, string.Empty, _matchTimeout);
        }

        #region match generation
        /// <summary>
        /// Find a match.
        /// <param name="quick">if true return null iff there exists a match</param>
        /// <param name="input">input string</param>
        /// <param name="startat">the position to start search in the input string</param>
        /// <param name="k">the next position after the end position in the input</param>
        /// </summary>
        public Match FindMatch(bool quick, string input, int startat, int k)
        {
            if (_checkTimeout)
            {
                // Using Environment.TickCount for efficiency instead of Stopwatch -- as in the non-DFA case.
                int timeout = (int)(_matchTimeout.TotalMilliseconds + 0.5);
                _timeoutOccursAt = Environment.TickCount + timeout;
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
                    if (quick)
                        return null;
                    else
                        return new Match(startat, 0);
                }
                else
                    return Match.NoMatch;
            }

            //find the first accepting state
            //initial start position in the input is i = 0
            int i = startat;

            //may return -1 as a legitimate value when the initial state is nullable and startat=0
            //returns -2 when there is no match
            i = FindFinalStatePosition(input, k, i, out int i_q0_A1, out int watchdog);

            if (i == -2)
                return Match.NoMatch;
            else
            {
                if (quick)
                    //this means success -- the original call was IsMatch
                    return null;

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
                        //walk in reverse to locate the start position of the match
                        i_start = FindStartPosition(input, i, i_q0_A1);
                    }

                    i_end = FindEndPosition(input, k, i_start);
                }

                return new Match(i_start, i_end + 1 - i_start);
            }
        }

        /// <summary>
        /// Find match end position using A, end position is known to exist.
        /// </summary>
        /// <param name="input">input array</param>
        /// <param name="i">inclusive start position</param>
        /// <param name="k">exclusive end position</param>
        /// <returns></returns>
        private int FindEndPosition(string input, int k, int i)
        {
            int i_end = k;
            uint prevCharKind = GetCharKind(input, i - 1);
            // pick the correct start state based on previous character kind
            State<S> q = _initialStates[prevCharKind];
            if (q.IsNullable(GetCharKind(input, i)))
            {
                //empty match exists because the initial state is accepting
                i_end = i - 1;
                // stop here if q is lazy
                if (q.IsLazy)
                    return i_end;
            }
            while (i < k)
            {
                bool done;
                int j = Math.Min(k, i + StateBoundLeeway);
                if (!_builder._antimirov)
                {
                    done = FindEndPositionDeltas<BrzozowskiTransition>(input, ref i, j, ref q, ref i_end);
                }
                else
                {
                    done = FindEndPositionDeltas<AntimirovTransition>(input, ref i, j, ref q, ref i_end);
                }
                if (done)
                    break;
            }

            Debug.Assert(i_end != k);
            return i_end;
        }

        // Inner loop for FindEndPosition parameterized by an ITransition type.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FindEndPositionDeltas<Transition>(string input, ref int i, int j, ref State<S> q, ref int i_end) where Transition : struct, ITransition
        {
            do
            {
                q = Delta<Transition>(input, i, q);

                if (q.IsNullable(GetCharKind(input, i + 1)))
                {
                    // stop here if q is lazy
                    if (q.IsLazy)
                    {
                        i_end = i;
                        return true;
                    }
                    //accepting state has been reached
                    //record the position
                    i_end = i;
                }
                else if (q.IsDeadend)
                {
                    //nonaccepting sink state (deadend) has been reached in A
                    //so the match ended when the last i_end was updated
                    return true;
                }
                i += 1;
            } while (i < j);
            return false;
        }

        /// <summary>
        /// Walk back in reverse using Ar to find the start position of match, start position is known to exist.
        /// </summary>
        /// <param name="input">the input string</param>
        /// <param name="i">position to start walking back from, i points at the last character of the match</param>
        /// <param name="match_start_boundary">do not pass this boundary when walking back</param>
        /// <returns></returns>
        private int FindStartPosition(string input, int i, int match_start_boundary)
        {
            // fetch the correct start state for Ar
            // this depends on previous character ---
            // which, because going backwards, is character number i+1
            uint prevKind = GetCharKind(input, i + 1);
            State<S> q = _reverseInitialStates[prevKind];
            //Ar may have a fixed prefix sequence
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
                // the whole prefix of Ar was in reverse a prefix of A
                // for example when the pattern of A is concrete word such as "abc"
                last_start = i + 1;
            }

            //walk back to the accepting state of Ar
            while (i >= match_start_boundary)
            {
                bool done;
                int j = Math.Max(match_start_boundary, i - StateBoundLeeway);
                if (!_builder._antimirov)
                {
                    done = FindStartPositionDeltas<BrzozowskiTransition>(input, ref i, j, ref q, ref last_start);
                }
                else
                {
                    done = FindStartPositionDeltas<AntimirovTransition>(input, ref i, j, ref q, ref last_start);
                }
                if (done)
                    break;
            }

            Debug.Assert(last_start != -1);
            return last_start;
        }

        // Inner loop for FindStartPosition parameterized by an ITransition type.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FindStartPositionDeltas<Transition>(string input, ref int i, int j, ref State<S> q, ref int last_start) where Transition : struct, ITransition
        {
            do
            {
                q = Delta<Transition>(input, i, q);

                //reached a deadend state,
                //thus the earliest match start point must have occurred already
                if (q.IsNothing)
                    return true;

                if (q.IsNullable(GetCharKind(input, i - 1)))
                {
                    //earliest start point so far
                    //this must happen at some point
                    //or else A1 would not have reached a
                    //final state after match_start_boundary
                    last_start = i;
                }
                i -= 1;
            } while (i > j);
            return false;
        }

        /// <summary>
        /// Returns -2 if no match exists. Returns -1 when i=0 and the initial state is nullable.
        /// </summary>
        /// <param name="input">given input string</param>
        /// <param name="k">input length or bounded input length</param>
        /// <param name="i">start position</param>
        /// <param name="i_q0">last position the initial state of A1 was visited</param>
        /// <param name="watchdog">length of match when positive</param>
        private int FindFinalStatePosition(string input, int k, int i, out int i_q0, out int watchdog)
        {
            // get the correct start state of A1,
            // which in general depends on the previous character kind in the input
            uint prevCharKindId = GetCharKind(input, i - 1);
            State<S> q = _dotstarredInitialStates[prevCharKindId];
            i_q0 = i;

            if (q.IsNothing)
            {
                //if q is nothing then it is a deadend from the beginning
                //this happens for example when the original regex started with start anchor and prevCharKindId is not Start
                watchdog = -1;
                return -2;
            }

            if (q.IsNullable(GetCharKind(input, i)))
            {
                //the initial state is nullable in this context so at least an empty match exists
                watchdog = -1;
                //the last position of the match is i-1 because the match is empty
                //this value is -1 if i=0
                return i - 1;
            }

            int i_q0_A1 = i;
            watchdog = -1;

            // search for a match end position within input[i..k-1]
            while (i < k)
            {
                if (q.IsInitialState)
                {
                    //i_q0_A1 is the most recent position in the input when A1 is in the initial state
                    i_q0_A1 = i;

                    if (_prefixBoyerMoore != null)
                    {
                        #region prefix optimization
                        //stay in the initial state if the prefix does not match
                        //thus advance the current position to the
                        //first position where the prefix does match

                        i = _prefixBoyerMoore.Scan(input, i, 0, input.Length);

                        if (i == -1)
                        {
                            // when a matching position does not exist then Scan returns -1
                            i_q0 = i_q0_A1;
                            watchdog = -1;
                            return -2;
                        }
                        else
                        {
                            //compute the end state for the A prefix
                            //skip directly to the resulting state
                            // --- i.e. do the loop ---
                            //for (int j = 0; j < prefix.Length; j++)
                            //    q = Delta(prefix[j], q, out regex);
                            // ---
                            q = GetSkipState(q.PrevCharKind);

                            // skip the prefix
                            i += _prefix.Length;
                            // here i points at the next character (the character immediately following the prefix)
                            if (q.IsNullable(GetCharKind(input, i)))
                            {
                                i_q0 = i_q0_A1;
                                watchdog = q.WatchDog;
                                //return the last position of the match
                                return i - 1;
                            }
                            if (i == k)
                            {
                                // no match was found
                                i_q0 = i_q0_A1;
                                return -2;
                            }
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
                            i_q0 = i_q0_A1;
                            return -2;
                        }

                        i_q0_A1 = i;
                        // the start state must be updated
                        // to reflect the kind of the previous character
                        // when anchors are not used, q will remain the same state
                        q = _dotstarredInitialStates[GetCharKind(input, i - 1)];
                        if (q.IsNothing)
                        {
                            i_q0 = i_q0_A1;
                            return -2;
                        }
                    }
                }

                int result;
                bool done;
                int j = Math.Min(k, i + StateBoundLeeway);
                if (!_builder._antimirov)
                {
                    done = FindFinalStatePositionDeltas<BrzozowskiTransition>(input, j, ref i, ref q, i_q0_A1, ref i_q0, ref watchdog, out result);
                }
                else
                {
                    done = FindFinalStatePositionDeltas<AntimirovTransition>(input, j, ref i, ref q, i_q0_A1, ref i_q0, ref watchdog, out result);
                }
                if (done)
                    return result;
                if (_checkTimeout)
                    DoCheckTimeout();
            }

            //no match was found
            i_q0 = i_q0_A1;
            return -2;
        }

        // Inner loop for FindFinalStatePosition parameterized by an ITransition type.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool FindFinalStatePositionDeltas<Transition>(string input, int j, ref int i, ref State<S> q, int i_q0_A1, ref int i_q0, ref int watchdog, out int result) where Transition : struct, ITransition
        {
            do
            {
                // make the transition based on input[i]
                q = Delta<Transition>(input, i, q);

                if (q.IsNullable(GetCharKind(input, i + 1)))
                {
                    i_q0 = i_q0_A1;
                    watchdog = q.WatchDog;
                    result = i;
                    return true;
                }
                else if (q.IsNothing)
                {
                    //q is a deadend state so any further search is meaningless
                    i_q0 = i_q0_A1;
                    result = -2;
                    return true;
                }
                // continue from the next character
                i += 1;
            } while (i < j && !q.IsInitialState);
            result = -3; // This value does not get used
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint GetCharKind(string input, int i)
        {
            if (_pattern._info.ContainsSomeAnchor)
            {
                if (i == -1 || i == input.Length)
                    return CharKind.StartStop;

                char nextChar = input[i];
                if (nextChar == '\n')
                {
                    if (_builder._newLinePredicate.Equals(_builder._solver.False))
                        //ignore \n
                        return 0;
                    else
                    {
                        if (i == 0 || i == input.Length - 1)
                            //very first of very last \n
                            //detection of very first \n is needed for rev(\Z)
                            return CharKind.NewLineS;
                        else
                            return CharKind.Newline;
                    }
                }

                if (nextChar < 128)
                    return _asciiCharKinds[nextChar];
                else
                    //apply the wordletter predicate to compute the kind of the next character
                    return _builder._solver.And(GetAtom(nextChar), _builder._wordLetterPredicate).Equals(_builder._solver.False) ? 0 : CharKind.WordLetter;
            }
            else
            {
                // the previous character kind is irrelevant when anchors are not used
                return 0;
            }
        }

        #endregion

        #region Specialized IndexOf
        /// <summary>
        ///  Find first occurrence of startset element in input starting from index i.
        ///  Startset here is assumed to consist of a few characters
        /// </summary>
        /// <param name="input">input string to search in</param>
        /// <param name="i">the start index in input to search from</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexOfStartset(string input, int i)
        {
            if (_startSetSize <= StartSetArrayMaxSize)
                return input.IndexOfAny(_startSetArray, i);
            else
            {
                for (int j = i; j < input.Length; j++)
                {
                    char c = input[j];
                    if (_startSetClassifier.Contains(c))
                        return j;
                }
            }
            return -1;
        }

        public void SaveDGML(TextWriter writer, int bound = 0, bool hideStateInfo = false, bool addDotStar = false, bool inReverse = false, bool onlyDFAinfo = false, int maxLabelLength = 500)
        {
            var graph = new DGML.RegexDFA<S>(this, bound, addDotStar, inReverse);
            var dgml = new DGML.DgmlWriter(writer, hideStateInfo, maxLabelLength, onlyDFAinfo);
            dgml.Write<S>(graph);
        }

        #endregion
    }
}
