// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Globalization;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Represents a precompiled form of a regex that implements match generation using symbolic derivatives.
    /// </summary>
    /// <typeparam name="S">character set type</typeparam>
    internal partial class SymbolicRegexMatcher<S> : IMatcher
    {
        internal SymbolicRegexBuilder<S> builder;

        /// <summary>
        /// Partition of the input space of predicates.
        /// Length of atoms is K.
        /// </summary>
        private S[] atoms;

        /// <summary>
        /// Maps each character into a partition id in the range 0..K-1.
        /// </summary>
        private Classifier dt;

        /// <summary>
        /// Original regex.
        /// </summary>
        internal SymbolicRegexNode<S> A;

        /// <summary>
        /// The RegexOptions this regex was created with
        /// </summary>
        internal System.Text.RegularExpressions.RegexOptions Options { get; set; }

        /// <summary>
        /// Timeout for matching.
        /// </summary>
        private TimeSpan _matchTimeout;
        /// <summary>
        /// corresponding timeout in ms
        /// </summary>
        private int _timeout;
        private int _timeoutOccursAt;
        private bool _checkTimeout;

        /// <summary>
        /// Set of elements that matter as first element of A.
        /// </summary>
        internal BooleanClassifier A_StartSet;

        /// <summary>
        /// predicate over characters that make some progress
        /// </summary>
        private S A_startset;

        /// <summary>
        /// maximum allowed size of A_startset_array
        /// </summary>
        private const int s_A_startset_array_max_size = 5;

        /// <summary>
        /// string of at most s_A_startset_array_max_size many characters
        /// </summary>
        private char[] A_startset_array;

        /// <summary>
        /// Number of elements in A_StartSet
        /// </summary>
        private int A_StartSet_Size;

        /// <summary>
        /// if nonempty then A has that fixed prefix
        /// </summary>
        private string A_prefix;

        /// <summary>
        /// non-null when A_prefix is nonempty
        /// </summary>
        private RegexBoyerMoore A_prefixBM;

        /// <summary>
        /// if true then the fixed prefix of A is idependent of case
        /// </summary>
        private bool A_fixedPrefix_ignoreCase;

        /// <summary>
        /// Cached skip states from the initial state of A1 for the 6 possible previous character kinds.
        /// </summary>
        private State<S>[] _A1_skipState = new State<S>[5];

        private State<S> GetA1_skipState(uint prevCharKind)
        {
            if (_A1_skipState[prevCharKind] == null)
            {
                var state = DeltaPlus(A_prefix, _A1q0[prevCharKind]);
                lock (this)
                {
                    if (_A1_skipState[prevCharKind] == null)
                        _A1_skipState[prevCharKind] = state;
                }
            }
            return _A1_skipState[prevCharKind];
        }

        /// <summary>
        /// Reverse(A).
        /// </summary>
        internal SymbolicRegexNode<S> Ar;

        ///// <summary>
        ///// if nonempty then Ar has that fixed prefix of predicates
        ///// </summary>
        //private S[] Ar_prefix_array;

        private string Ar_prefix;

        /// <summary>
        /// Cached skip states from the initial state of Ar for the 6 possible previous character kinds.
        /// </summary>
        private State<S>[] _Ar_skipState = new State<S>[6];

        private State<S> GetAr_skipState(uint prevCharKind)
        {
            if (_Ar_skipState[prevCharKind] == null)
            {
                var state = DeltaPlus(Ar_prefix, _Arq0[prevCharKind]);
                lock (this)
                {
                    if (_Ar_skipState[prevCharKind] == null)
                        _Ar_skipState[prevCharKind] = state;
                }
            }
            return _Ar_skipState[prevCharKind];
        }

        /// <summary>
        /// .*A start regex
        /// </summary>
        internal SymbolicRegexNode<S> A1;

        private State<S>[] _Aq0 = new State<S>[5];

        private State<S>[] _A1q0 = new State<S>[5];

        private State<S>[] _Arq0 = new State<S>[5];

        private uint[] _asciiCharKind = new uint[128];

        /// <summary>
        /// Initialized to the next power of 2 that is at least the number of atoms
        /// </summary>
        private int K;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal State<S> GetState(int stateId)
        {
            // the builder maintains a mapping
            // from stateIds to states
            return builder.statearray[stateId];
        }

        /// <summary>
        /// Get the atom of character c
        /// </summary>
        /// <param name="c">character code</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private S GetAtom(int c) => atoms[dt.Find(c)];

        /// <summary>
        /// Initial bound (1024) on the nr of states stored in delta.
        /// This value may become larger dynamically if more that 1024 states are generated.
        /// </summary>
        internal int StateLimit = 1024;

        /// <summary>
        /// Bound on the maximum nr of chars that trigger vectorized IndexOf.
        /// </summary>
        internal readonly int StartSetSizeLimit = 1;

        /// <summary>
        /// Holds all transitions for states 0..MaxNrOfStates-1.
        /// each transition q ---atoms[i]---> p is represented by entry p = delta[(q * K) + i].
        /// Length of delta is K*StateLimit. Entry delta[i]=null means that the state is still undefined.
        /// </summary>
        private State<S>[] delta;

        #region custom serialization/deserialization
        /// <summary>
        /// Append the custom format of this matcher into sb. All characters are in visible ASCII.
        /// Main fragments are separated by a custom separator character not used in any individual fragment.
        /// </summary>
        public void Serialize(StringBuilder sb)
        {
            //-----------------------------------0
            sb.Append(_culture.Name);
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------1
            this.builder.solver.Serialize(sb);
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------2
            A.Serialize(sb);
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------3
            sb.Append(Base64.Encode((int)Options));
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------4
            sb.Append(builder.solver.SerializePredicate(builder.wordLetterPredicate));
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------5
            sb.Append(builder.solver.SerializePredicate(builder.newLinePredicate));
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------6
            sb.Append(builder.solver.SerializePredicate(A_startset));
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------7
            A_StartSet.Serialize(sb);
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------8
            sb.Append(Base64.Encode(A_StartSet_Size));
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------9
            Base64.Encode(A_startset_array, sb);
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------10
            Base64.Encode(A_prefix, sb);
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------11
            sb.Append(A_fixedPrefix_ignoreCase);
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------12
            Base64.Encode(Ar_prefix, sb);
            sb.Append(Regex.s_top_level_separator);
            //-----------------------------------13
            dt.Serialize(sb);
            sb.Append(Regex.s_top_level_separator);
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
            _culture = (fragments[0] == string.Empty ? CultureInfo.InvariantCulture :
                (fragments[0] == CultureInfo.CurrentCulture.Name ? CultureInfo.CurrentCulture : new CultureInfo(fragments[0])));
            builder = new SymbolicRegexBuilder<S>(solver);
            atoms = solver.GetPartition();
            A = builder.Deserialize(fragments[2]);
            Options = (RegexOptions)Base64.DecodeInt(fragments[3]);
            //these predicates are relevant only when anchors are used
            builder.wordLetterPredicate = builder.solver.DeserializePredicate(fragments[4]);
            builder.newLinePredicate = builder.solver.DeserializePredicate(fragments[5]);
            A_startset = builder.solver.DeserializePredicate(fragments[6]);
            A_StartSet = BooleanClassifier.Deserialize(fragments[7]);
            A_StartSet_Size = Base64.DecodeInt(fragments[8]);
            A_startset_array = Base64.DecodeCharArray(fragments[9]);
            A_prefix = Base64.DecodeString(fragments[10]);
            A_fixedPrefix_ignoreCase = bool.Parse(fragments[11]);
            Ar_prefix = Base64.DecodeString(fragments[12]);
            dt = Classifier.Deserialize(fragments[13]);
            string potentialTimeout = fragments[14].TrimEnd();
            if (potentialTimeout == string.Empty)
            {
                _matchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout;
                _checkTimeout = false;
            }
            else
            {
                _matchTimeout = TimeSpan.Parse(potentialTimeout);
                _checkTimeout = true;
                _timeout = (int)(_matchTimeout.TotalMilliseconds + 0.5); // Round up, so it will at least 1ms;
                _timeoutChecksToSkip = TimeoutCheckFrequency;
            }
            if (A.info.ContainsSomeAnchor)
            {
                //line anchors are being used when builder.newLinePredicate is different from False
                if (!builder.newLinePredicate.Equals(builder.solver.False))
                    _asciiCharKind[10] = CharKind.Newline;
                //word boundary is being used when builder.wordLetterPredicate is different from False
                if (!builder.wordLetterPredicate.Equals(builder.solver.False))
                {
                    _asciiCharKind['_'] = CharKind.WordLetter;
                    for (char i = '0'; i <= '9'; i++)
                        _asciiCharKind[i] = CharKind.WordLetter;
                    for (char i = 'A'; i <= 'Z'; i++)
                        _asciiCharKind[i] = CharKind.WordLetter;
                    for (char i = 'a'; i <= 'z'; i++)
                        _asciiCharKind[i] = CharKind.WordLetter;
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
            _checkTimeout = (System.Text.RegularExpressions.Regex.InfiniteMatchTimeout != _matchTimeout);
            _timeout = (int)(matchTimeout.TotalMilliseconds + 0.5); // Round up, so it will at least 1ms;
            _timeoutChecksToSkip = TimeoutCheckFrequency;

            this.Options = options;
            this.StartSetSizeLimit = 1;
            this.builder = sr.builder;
            if (builder.solver is BV64Algebra)
            {
                BV64Algebra bva = builder.solver as BV64Algebra;
                atoms = bva.GetPartition() as S[];
                dt = bva._classifier;
            }
            else if (builder.solver is BVAlgebra)
            {
                BVAlgebra bva = builder.solver as BVAlgebra;
                atoms = bva.atoms as S[];
                dt = bva._classifier;
            }
            else if (builder.solver is CharSetSolver)
            {
                atoms = minterms as S[];
                dt = Classifier.Create(builder.solver as CharSetSolver, minterms);
            }
            else
            {
                throw new NotSupportedException($"only {nameof(BV64Algebra)} or {nameof(BVAlgebra)} or {nameof(CharSetSolver)} algebra is supported");
            }

            this.A = sr;

            InitializeRegexes();

            A_startset = A.GetStartSet();
            if (!builder.solver.IsSatisfiable(A_startset))
                //if the startset is empty make it full instead by including all characters
                //this is to ensure that startset is nonempty -- as an invariant assumed by operations using it
                A_startset = builder.solver.True;

            this.A_StartSet_Size = (int)builder.solver.ComputeDomainSize(A_startset);

            var startbdd = builder.solver.ConvertToCharSet(css, A_startset);
            this.A_StartSet = BooleanClassifier.Create(css, startbdd);
            //store the start characters in the A_startset_array if there are not too many characters
            if (this.A_StartSet_Size <= s_A_startset_array_max_size)
                this.A_startset_array = new List<char>(css.GenerateAllCharacters(startbdd)).ToArray();
            else
                this.A_startset_array = Array.Empty<char>();

            this.A_prefix = A.GetFixedPrefix(css, culture.Name, out this.A_fixedPrefix_ignoreCase);
            this.Ar_prefix = Ar.GetFixedPrefix(css, culture.Name, out _);

            InitializePrefixBoyerMoore();

            if (A.info.ContainsSomeAnchor)
                for (int i = 0; i < 128; i++)
                    _asciiCharKind[i] =
                        i == 10 ? (builder.solver.MkAnd(GetAtom(i), builder.newLinePredicate).Equals(builder.solver.False) ? 0 : CharKind.Newline)
                                : (builder.solver.MkAnd(GetAtom(i), builder.wordLetterPredicate).Equals(builder.solver.False) ? 0 : CharKind.WordLetter);
        }

        private void InitializePrefixBoyerMoore()
        {
            if (this.A_prefix != string.Empty && this.A_prefix.Length <= RegexBoyerMoore.MaxLimit && this.A_prefix.Length > 1)
            {
                string prefix = this.A_prefix;
                // RegexBoyerMoore expects the prefix to be lower case when case is ignored
                if (this.A_fixedPrefix_ignoreCase)
                    //use the culture of the matcher
                    prefix = this.A_prefix.ToLower(_culture);
                this.A_prefixBM = new RegexBoyerMoore(prefix, this.A_fixedPrefix_ignoreCase, false, _culture);
            }
        }

        private void InitializeRegexes()
        {
            A1 = builder.MkConcat(builder.dotStar, A);
            Ar = A.Reverse();
            // let K be the smallest k s.t. 2^k >= atoms.Length + 1
            // the extra slot with id atoms.Length is reserved for \Z (last occurrence of \n)
            int k = 1;
            while (atoms.Length >= (1 << k)) k += 1;
            K = k;
            // initialize state lookup table
            StateLimit = this.builder.statearray.Length;
            delta = new State<S>[StateLimit << K];
            // create initial states for A, A1 and Ar
            if (!A.info.ContainsSomeAnchor)
            {
                // only the default previous character kind 0 is ever going to be used for all initial states
                _Aq0[0] = State<S>.MkState(A, 0);
                _A1q0[0] = State<S>.MkState(A1, 0);
                // _A1q0[0] is recognized as special initial state,
                // this information is used for search optimization based on start set and prefix of A
                _A1q0[0].isInitialState = true;
                _Arq0[0] = State<S>.MkState(Ar, 0);
            }
            else
            {
                for (uint i = 0; i < 5; i++)
                {
                    _Aq0[i] = State<S>.MkState(A, i);
                    _A1q0[i] = State<S>.MkState(A1, i);
                    _Arq0[i] = State<S>.MkState(Ar, i);
                    //used to detect if initial state was reentered, then startset can be triggered
                    //but observe that the behavior from the state may ultimately depend on the previous
                    //input char e.g. possibly causing nullability of \b or \B or of a start-of-line anchor,
                    //in that sense there can be several "versions" (not more than 5) of the initial state
                    _A1q0[i].isInitialState = true;
                }
            }
        }

        /// <summary>
        /// Return the state after the given input string from the given state q.
        /// </summary>
        private State<S> DeltaPlus(string input, State<S> q)
        {
            for (int i = 0; i < input.Length; i++)
                q = Delta(input, i, q);
            return q;
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
        private State<S> Delta(string input, int i, State<S> q)
        {
            int c = input[i];
            // atom_id = atoms.Length represents \Z (last \n)
            int atom_id = (c == 10 && i == input.Length - 1 && q.Node.info.StartsWithLineAnchor ? atoms.Length : dt.Find(c));
            int offset = (q.Id << K) | atom_id;
            var p = delta[offset];
            if (p == null)
                //transition atom False means that this is \Z
                return CreateNewTransition(q, atom_id == atoms.Length ? builder.solver.False : atoms[atom_id], offset);
            else
                return p;
        }

        /// <summary>
        /// Critical region for threadsafe applications for defining a new transition
        /// </summary>
        private State<S> CreateNewTransition(State<S> q, S atom, int offset)
        {
            lock (this)
            {
                // check if meanwhile delta[offset] has become defined possibly by another thread
                State<S> p = delta[offset];
                if (p != null)
                    return p;
                else
                {
                    // this is the only place in code where the Next method is called
                    p = q.Next(atom);
                    // if the statearray was extended then delta must be extended accordingly
#if DEBUG
                    if (StateLimit > builder.statearray.Length)
                        throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
                    if (StateLimit < builder.statearray.Length)
                    {
                        StateLimit = builder.statearray.Length;
                        Array.Resize(ref delta, StateLimit << K);
                    }
                    delta[offset] = p;
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
                bool emptyMatchExists = A.IsNullableFor(CharKind.Context(prevKind, nextKind));
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

            int i_q0_A1;
            int watchdog;
            //may return -1 as a legitimate value when the initial state is nullable and startat=0
            //returns -2 when there is no match
            i = FindFinalStatePosition(input, k, i, out i_q0_A1, out watchdog);

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
#if DEBUG
                        if (i != startat - 1)
                            throw new AutomataException(AutomataExceptionKind.InternalError);
#endif
                        i_start = startat;
                    }
                    else
                        //walk in reverse to locate the start position of the match
                        i_start = FindStartPosition(input, i, i_q0_A1);
                    i_end = FindEndPosition(input, k, i_start);
                }

                return new Match(i_start, i_end + 1 - i_start);
            }
        }

        /// <summary>
        /// It is known here that regex is nullable
        /// </summary>
        /// <param name="regex"></param>
        /// <returns></returns>
        private int GetWatchdog(SymbolicRegexNode<S> regex)
        {
            if (regex.kind == SymbolicRegexKind.WatchDog)
            {
                return regex.lower;
            }
            else if (regex.kind == SymbolicRegexKind.Or)
            {
                return regex.alts.watchdog;
            }
            else
            {
                return -1;
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
            State<S> q = _Aq0[prevCharKind];
            if (q.IsNullable(GetCharKind(input, i)))
            {
                //empty match exists because the initial state is accepting
                i_end = i - 1;
                // stop here if q is lazy
                if (q.Node.info.IsLazy)
                    return i_end;
            }
            while (i < k)
            {
                q = Delta(input, i, q);

                if (q.IsNullable(GetCharKind(input, i+1)))
                {
                    // stop here if q is lazy
                    if (q.Node.info.IsLazy)
                        return i;
                    //accepting state has been reached
                    //record the position
                    i_end = i;
                }
                else if (q.Node == builder.nothing)
                {
                    //nonaccepting sink state (deadend) has been reached in A
                    //so the match ended when the last i_end was updated
                    break;
                }
                i += 1;
            }

#if DEBUG
            if (i_end == k)
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
            return i_end;
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
            State<S> q = _Arq0[prevKind];
            //Ar may have a fixed prefix sequence
            if (Ar_prefix.Length > 0)
            {
                //skip past the prefix portion of Ar
                q = GetAr_skipState(prevKind);
                i = i - this.Ar_prefix.Length;
            }
            if (i == -1)
            {
#if DEBUG
                //we reached the beginning of the input, thus the state q must be accepting
                if (!q.IsNullable(GetCharKind(input, i)))
                    throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
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
                q = Delta(input, i, q);

                //reached a deadend state,
                //thus the earliest match start point must have occurred already
                if (q.IsNothing)
                    break;

                if (q.IsNullable(GetCharKind(input, i-1)))
                {
                    //earliest start point so far
                    //this must happen at some point
                    //or else A1 would not have reached a
                    //final state after match_start_boundary
                    last_start = i;
                }
                i -= 1;
            }
#if DEBUG
            if (last_start == -1)
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
            return last_start;
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
            State<S> q = _A1q0[prevCharKindId];

            if (q.IsNothing)
            {
                //if q is nothing then it is a deadend from the beginning
                //this happens for example when the original regex started with start anchor and prevCharKindId is not Start
                i_q0 = i;
                watchdog = -1;
                return -2;
            }

            if (q.IsNullable(GetCharKind(input, i)))
            {
                //the initial state is nullable in this context so at least an empty match exists
                i_q0 = i;
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
                if (q.isInitialState)
                {
                    //i_q0_A1 is the most recent position in the input when A1 is in the initial state
                    i_q0_A1 = i;

                    if (this.A_prefixBM != null)
                    {
                        #region prefix optimization
                        //stay in the initial state if the prefix does not match
                        //thus advance the current position to the
                        //first position where the prefix does match

                        i = A_prefixBM.Scan(input, i, 0, input.Length);

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
                            q = GetA1_skipState(q.PrevCharKind);

                            // skip the prefix
                            i = i + this.A_prefix.Length;
                            // here i points at the next character (the character immediately following the prefix)
                            if (q.IsNullable(GetCharKind(input, i)))
                            {
                                i_q0 = i_q0_A1;
                                watchdog = GetWatchdog(q.Node);
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
                        q = _A1q0[GetCharKind(input, i - 1)];
                        if (q.IsNothing)
                        {
                            i_q0 = i_q0_A1;
                            return -2;
                        }
                    }
                }

                // make the transition based on input[i]
                q = Delta(input, i, q);

                if (q.IsNullable(GetCharKind(input, i + 1)))
                {
                    i_q0 = i_q0_A1;
                    watchdog = GetWatchdog(q.Node);
                    return i;
                }
                else if (q.IsNothing)
                {
                    //q is a deadend state so any further search is meaningless
                    i_q0 = i_q0_A1;
                    return -2;
                }
                // continue from the next character
                i += 1;

                if (_checkTimeout)
                    DoCheckTimeout();
            }

            //no match was found
            i_q0 = i_q0_A1;
            return -2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint GetCharKind(string input, int i)
        {
            if (A.info.ContainsSomeAnchor)
            {
                if (i == -1 || i == input.Length)
                    return CharKind.StartStop;

                char nextChar = input[i];
                if (nextChar == '\n')
                {
                    if (builder.newLinePredicate.Equals(builder.solver.False))
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
                    return _asciiCharKind[nextChar];
                else
                    //apply the wordletter predicate to compute the kind of the next character
                    return builder.solver.MkAnd(GetAtom(nextChar), builder.wordLetterPredicate).Equals(builder.solver.False) ? 0 : CharKind.WordLetter;
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
            if (A_StartSet_Size <= s_A_startset_array_max_size)
                return input.IndexOfAny(A_startset_array, i);
            else
            {
                for (int j = i; j < input.Length; j++)
                {
                    char c = input[j];
                    if (A_StartSet.Contains(c))
                        return j;
                }
            }
            return -1;
        }

        /// <summary>
        ///  Find first occurrence of startset element in input starting from index i.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexOfStartsetUTF8(byte[] input, int i, ref int surrogate_codepoint)
        {
            int k = input.Length;
            int step = 1;
            int codepoint = 0;
            while (i < k)
            {
                int c = input[i];
                if (c > 0x7F)
                {
                    UTF8Encoding.DecodeNextNonASCII(input, i, out step, out codepoint);
                    if (codepoint > 0xFFFF)
                    {
                        throw new NotImplementedException("surrogate pairs");
                    }
                    else
                    {
                        c = codepoint;
                    }
                }

                if (A_StartSet.Contains((ushort)c))
                    break;
                else
                {
                    i += step;
                }
            }
            if (i == k)
                return -1;
            else
                return i;
        }

        /// <summary>
        ///  Find first occurrence of value in input starting from index i.
        /// </summary>
        /// <param name="input">input array to search in</param>
        /// <param name="value">nonempty subarray that is searched for</param>
        /// <param name="i">the search start index in input</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexOf(byte[] input, byte[] value, int i)
        {
            int n = value.Length;
            int k = (input.Length - n) + 1;
            while (i < k)
            {
                i = Array.IndexOf<byte>(input, value[0], i);
                if (i == -1)
                    return -1;
                int j = 1;
                while (j < n && input[i + j] == value[j])
                    j += 1;
                if (j == n)
                    return i;
                i += 1;
            }
            return -1;
        }

        /// <summary>
        ///  Find first occurrence of byte in input starting from index i that maps to true by the predicate.
        /// </summary>
        /// <param name="input">input array to search in</param>
        /// <param name="pred">boolean array of size 256 telling which bytes to match</param>
        /// <param name="i">the search start index in input</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexOf(byte[] input, bool[] pred, int i)
        {
            int k = input.Length;
            while (i < k && !pred[input[i]])
                i += 1;
            return (i == k ? -1 : i);
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
