﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Wraps an instance of SymbolicRegex&lt;BV&gt;, the number of needed partition blocks is &gt; 64
    /// </summary>
    [Serializable]
    internal class SymbolicRegexBV : SymbolicRegex<BV>
    {
        private SymbolicRegexBV(SymbolicRegexBuilder<BV> builder, SymbolicRegexNode<BDD> sr,
                                CharSetSolver solver, SymbolicRegexBuilder<BDD> srBuilder, BDD[] minterms, System.Text.RegularExpressions.RegexOptions options)
            : base(srBuilder.Transform(sr, builder, set => builder.solver.ConvertFromCharSet(solver, set)),
                  solver, minterms, options)
        {
        }

        /// <summary>
        /// Is called with minterms.Length at least 65
        /// </summary>
        internal SymbolicRegexBV(SymbolicRegexNode<BDD> sr,
                                 CharSetSolver solver, SymbolicRegexBuilder<BDD> srBuilder, BDD[] minterms, System.Text.RegularExpressions.RegexOptions options)
            : this(new SymbolicRegexBuilder<BV>(BVAlgebra.Create(solver, minterms)), sr,
                  solver, srBuilder, minterms, options)
        {
            //update the word letter predicate in the BV solver to the correct one
            this.builder.wordLetterPredicate = this.builder.solver.ConvertFromCharSet(solver, srBuilder.wordLetterPredicate);
            //update the \n predicate in the BV solver to the correct one
            this.builder.newLinePredicate = this.builder.solver.ConvertFromCharSet(solver, srBuilder.newLinePredicate);
        }

        /// <summary>
        /// Invoked by deserializer
        /// </summary>
        public SymbolicRegexBV(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Wraps an instance of SymbolicRegex&lt;ulong&gt;, the number of needed partition blocks is &lt; 65
    /// </summary>
    [Serializable]
    internal class SymbolicRegexUInt64 : SymbolicRegex<ulong>
    {
        private SymbolicRegexUInt64(SymbolicRegexBuilder<ulong> builder, SymbolicRegexNode<BDD> sr,
                                CharSetSolver solver, SymbolicRegexBuilder<BDD> srBuilder, BDD[] minterms, System.Text.RegularExpressions.RegexOptions options)
            : base(srBuilder.Transform(sr, builder, set => builder.solver.ConvertFromCharSet(solver, set)),
                  solver, minterms, options)
        {
            //update the word letter predicate in the ulong solver to the correct one
            this.builder.wordLetterPredicate = this.builder.solver.ConvertFromCharSet(solver, srBuilder.wordLetterPredicate);
            //update the \n predicate in the BV solver to the correct one
            this.builder.newLinePredicate = this.builder.solver.ConvertFromCharSet(solver, srBuilder.newLinePredicate);
        }

        /// <summary>
        /// Is called with minterms.Length at most 64
        /// </summary>
        internal SymbolicRegexUInt64(SymbolicRegexNode<BDD> sr,
                                 CharSetSolver solver, SymbolicRegexBuilder<BDD> srBuilder, BDD[] minterms, System.Text.RegularExpressions.RegexOptions options)
            : this(new SymbolicRegexBuilder<ulong>(BV64Algebra.Create(solver, minterms)), sr,
                  solver, srBuilder, minterms, options)
        {
        }

        /// <summary>
        /// Invoked by deserializer
        /// </summary>
        public SymbolicRegexUInt64(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Represents a precompiled form of a regex that implements match generation using symbolic derivatives.
    /// </summary>
    /// <typeparam name="S">character set type</typeparam>
    [Serializable]
    internal class SymbolicRegex<S> : IMatcher, ISerializable
    {
        [NonSerialized]
        internal SymbolicRegexBuilder<S> builder;

        /// <summary>
        /// Partition of the input space of predicates.
        /// Length of atoms is K.
        /// </summary>
        [NonSerialized]
        private S[] atoms;

        /// <summary>
        /// Maps each character into a partition id in the range 0..K-1.
        /// </summary>
        [NonSerialized]
        private DecisionTree dt;

#if UNSAFE
        /// <summary>
        /// If not null then contains all relevant start characters as vectors
        /// </summary>
        [NonSerialized]
        Vector<ushort>[] A_StartSet_Vec = null;

        /// <summary>
        /// If A_StartSet_Vec is length 1 then contains the corresponding character
        /// </summary>
        [NonSerialized]
        ushort A_StartSet_singleton;
#endif

        ///// <summary>
        ///// First byte of A_prefixUTF8 in vector
        ///// </summary>
        //[NonSerialized]
        //private Vector<byte> A_prefixUTF8_first_byte;

        /// <summary>
        /// Original regex.
        /// </summary>
        [NonSerialized]
        internal SymbolicRegexNode<S> A;

        /// <summary>
        /// The RegexOptions this regex was created with
        /// </summary>
        internal System.Text.RegularExpressions.RegexOptions Options { get; set; }

        /// <summary>
        /// Set of elements that matter as first element of A.
        /// </summary>
        internal BooleanDecisionTree A_StartSet;

        /// <summary>
        /// predicate over characters that make some progress
        /// </summary>
        private S A_startset;

        /// <summary>
        /// Number of elements in A_StartSet
        /// </summary>
        private int A_StartSet_Size;

        /// <summary>
        /// if nonempty then A has that fixed prefix
        /// </summary>
        private string A_prefix;

        /// <summary>
        /// if nonempty then A has that fixed prefix
        /// </summary>>
        [NonSerialized]
        private byte[] A_prefixUTF8;

        /// <summary>
        /// predicate array corresponding to fixed prefix of A
        /// </summary>
        private S[] A_prefix_array;

        /// <summary>
        /// if true then the fixed prefix of A is idependent of case
        /// </summary>
        [NonSerialized]
        private bool A_fixedPrefix_ignoreCase;

        /// <summary>
        /// Cached skip states from the initial state of A1 for the 6 possible previous character kinds.
        /// </summary>
        [NonSerialized]
        private State<S>[] _A1_skipState = new State<S>[6];

        private State<S> GetA1_skipState(CharKindId prevCharKindId)
        {
            int id = (int)prevCharKindId;
            if (_A1_skipState[id] == null)
            {
                var state = DeltaPlus(A_prefix, _A1q0[id]);
                lock (this)
                {
                    if (_A1_skipState[id] == null)
                        _A1_skipState[id] = state;
                }
            }
            return _A1_skipState[id];
        }

        /// <summary>
        /// Reverse(A).
        /// </summary>
        [NonSerialized]
        private SymbolicRegexNode<S> Ar;

        /// <summary>
        /// if nonempty then Ar has that fixed prefix of predicates
        /// </summary>
        private S[] Ar_prefix_array;

        private string Ar_prefix;

        /// <summary>
        /// Cached skip states from the initial state of Ar for the 6 possible previous character kinds.
        /// </summary>
        [NonSerialized]
        private State<S>[] _Ar_skipState = new State<S>[6];

        private State<S> GetAr_skipState(CharKindId prevCharKindId)
        {
            int id = (int)prevCharKindId;
            if (_Ar_skipState[id] == null)
            {
                var state = DeltaPlus(Ar_prefix, _Arq0[id]);
                lock (this)
                {
                    if (_Ar_skipState[id] == null)
                        _Ar_skipState[id] = state;
                }
            }
            return _Ar_skipState[id];
        }

        /// <summary>
        /// .*A start regex
        /// </summary>
        [NonSerialized]
        private SymbolicRegexNode<S> A1;

        [NonSerialized]
        private State<S>[] _Aq0 = new State<S>[6];

        [NonSerialized]
        private State<S>[] _A1q0 = new State<S>[6];

        [NonSerialized]
        private State<S>[] _Arq0 = new State<S>[6];

        /// <summary>
        /// Initialized to atoms.Length.
        /// </summary>
        [NonSerialized]
        private int K;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private State<S> GetState(int stateId)
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
        private S GetAtom(int c)
        {
            return atoms[c < dt.precomputed.Length ? dt.precomputed[c] : dt.bst.Find(c)];
        }

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
        [NonSerialized]
        private State<S>?[] delta;

        #region custom serialization

        [NonSerialized]
        internal bool serializeInSimplifiedForm = false;
        /// <summary>
        /// This serialization method is invoked by BinaryFormatter.Serialize via Serialize method.
        /// </summary>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (serializeInSimplifiedForm)
            {
                #region special case for replacing all character classes with a single character
                BV64Algebra bvalg = this.builder.solver as BV64Algebra;
                if (bvalg == null)
                    throw new NotSupportedException("Simplified serialization is only supported for BV64Algebra");
                var simpl_bvalg = bvalg.ReplaceMintermsWithVisibleCharacters();

                info.AddValue("solver", simpl_bvalg);
                info.AddValue("A", A.Serialize());
                info.AddValue("Options", (object)Options);

                ulong A_startset_ulong = (ulong)(object)A_startset;
                ulong wordLetter_ulong = (ulong)(object)builder.wordLetterPredicate;
                ulong newLine_ulong = (ulong)(object)builder.newLinePredicate;

                var simpl_precomputed = Array.ConvertAll(simpl_bvalg.dtree.precomputed, atomId => simpl_bvalg.IsSatisfiable(simpl_bvalg.MkAnd(simpl_bvalg.atoms[atomId], A_startset_ulong)));
                BooleanDecisionTree simpl_A_StartSet;
                if (simpl_bvalg.IsSatisfiable(simpl_bvalg.MkAnd(simpl_bvalg.atoms[0], A_startset_ulong)))
                    simpl_A_StartSet = new BooleanDecisionTree(simpl_precomputed, new DecisionTree.BST(1, null, null));
                else
                    simpl_A_StartSet = new BooleanDecisionTree(simpl_precomputed, new DecisionTree.BST(0, null, null));
                var simpl_A_StartSet_Size = simpl_bvalg.ComputeDomainSize(A_startset_ulong);

                info.AddValue("A_StartSet", simpl_A_StartSet);
                info.AddValue("A_startset", A_startset_ulong);
                info.AddValue("wordLetter", wordLetter_ulong);
                info.AddValue("newLine", newLine_ulong);

                var simpl_A_prefix = "";

                for (int i = 0; i < A_prefix_array.Length; i++)
                {
                    ulong set = (ulong)(object)A_prefix_array[i];
                    ulong size = simpl_bvalg.ComputeDomainSize(set);
                    if (size > 1)
                        break;
                    else
                        simpl_A_prefix += simpl_bvalg.PrettyPrint(set);
                }

                info.AddValue("A_prefix", StringUtility.SerializeStringToCharCodeSequence(simpl_A_prefix));
                info.AddValue("A_fixedPrefix_ignoreCase", false);
                info.AddValue("A_prefix_array", A_prefix_array);

                var simpl_Ar_prefix = "";

                for (int i = 0; i < Ar_prefix_array.Length; i++)
                {
                    ulong set = (ulong)(object)Ar_prefix_array[i];
                    ulong size = simpl_bvalg.ComputeDomainSize(set);
                    if (size > 1)
                        break;
                    else
                        simpl_Ar_prefix += simpl_bvalg.PrettyPrint(set);
                }

                info.AddValue("Ar_prefix_array", Ar_prefix_array);
                info.AddValue("Ar_prefix", StringUtility.SerializeStringToCharCodeSequence(simpl_Ar_prefix));
                #endregion
            }
            else
            {
                info.AddValue("solver", this.builder.solver);
                info.AddValue("A", A.Serialize());
                info.AddValue("Options", (object)Options);
                info.AddValue("wordLetter", this.builder.wordLetterPredicate);
                info.AddValue("newLine", this.builder.newLinePredicate);

                info.AddValue("StateLimit", StateLimit);
                info.AddValue("StartSetSizeLimit", StartSetSizeLimit);

                info.AddValue("A_StartSet", A_StartSet);
                info.AddValue("A_startset", A_startset);
                info.AddValue("A_StartSet_Size", A_StartSet_Size);

                info.AddValue("A_prefix", StringUtility.SerializeStringToCharCodeSequence(A_prefix));
                info.AddValue("A_fixedPrefix_ignoreCase", A_fixedPrefix_ignoreCase);
                info.AddValue("A_prefix_array", A_prefix_array);

                info.AddValue("Ar_prefix_array", Ar_prefix_array);
                info.AddValue("Ar_prefix", StringUtility.SerializeStringToCharCodeSequence(Ar_prefix));
            }
        }

        /// <summary>
        /// This deserialization constructor is invoked by IFormatter.Deserialize via Deserialize method
        /// </summary>
        public SymbolicRegex(SerializationInfo info, StreamingContext context)
        {
            this.Options = (System.Text.RegularExpressions.RegexOptions)info.GetValue("Options", typeof(System.Text.RegularExpressions.RegexOptions));

            var solver = (ICharAlgebra<S>)info.GetValue("solver", typeof(ICharAlgebra<S>));
            this.builder = new SymbolicRegexBuilder<S>(solver);
            this.builder.wordLetterPredicate = (S)info.GetValue("wordLetter", typeof(S));
            this.builder.newLinePredicate = (S)info.GetValue("newLine", typeof(S));

            this.atoms = builder.solver.GetPartition();
            this.dt = ((BVAlgebraBase)builder.solver).dtree;

            A = builder.Deserialize(info.GetString("A"));

            InitializeRegexes();

            this.A_startset = A.GetStartSet();
            this.A_StartSet_Size = (int)builder.solver.ComputeDomainSize(A_startset);

            this.A_StartSet = (BooleanDecisionTree)info.GetValue("A_StartSet", typeof(BooleanDecisionTree));
            this.A_startset = (S)info.GetValue("A_startset", typeof(S));

            this.A_prefix_array = info.GetValue("A_prefix_array", typeof(S[])) as S[];
            this.A_prefix = StringUtility.DeserializeStringFromCharCodeSequence(info.GetString("A_prefix"));
            this.A_prefixUTF8 = System.Text.UnicodeEncoding.UTF8.GetBytes(this.A_prefix);

            this.A_fixedPrefix_ignoreCase = info.GetBoolean("A_fixedPrefix_ignoreCase");

            this.Ar_prefix_array = (S[])info.GetValue("Ar_prefix_array", typeof(S[]));
            this.Ar_prefix = StringUtility.DeserializeStringFromCharCodeSequence(info.GetString("Ar_prefix"));

            //InitializeVectors();
        }

        /// <summary>
        /// Parse a symbolic regex from its serialized form.
        /// </summary>
        /// <param name="symbolicregex">serialized form of a symbolic regex</param>
        public SymbolicRegexNode<S> Parse(string symbolicregex)
        {
            return builder.Deserialize(symbolicregex);
        }

        #endregion

        /// <summary>
        /// Constructs matcher for given symbolic regex
        /// </summary>
        internal SymbolicRegex(SymbolicRegexNode<S> sr, CharSetSolver css, BDD[] minterms, System.Text.RegularExpressions.RegexOptions options)
        {
            if (sr.IsNullable)
                throw new NotSupportedException( SRM.Regex._DFA_incompatible_with + "nullable regex (accepting the empty string)");

            this.Options = options;
            this.StartSetSizeLimit = 1;
            this.builder = sr.builder;
            if (builder.solver is BV64Algebra)
            {
                BV64Algebra bva = builder.solver as BV64Algebra;
                atoms = bva.atoms as S[];
                dt = bva.dtree;
            }
            else if (builder.solver is BVAlgebra)
            {
                BVAlgebra bva = builder.solver as BVAlgebra;
                atoms = bva.atoms as S[];
                dt = bva.dtree;
            }
            else if (builder.solver is CharSetSolver)
            {
                atoms = minterms as S[];
                dt = DecisionTree.Create(builder.solver as CharSetSolver, minterms);
            }
            else
            {
                throw new NotSupportedException(string.Format("only {0} or {1} or {2} algebra is supported", typeof(BV64Algebra), typeof(BVAlgebra), typeof(CharSetSolver)));
            }

            this.A = sr;

            InitializeRegexes();

            A_startset = A.GetStartSet();
            this.A_StartSet_Size = (int)builder.solver.ComputeDomainSize(A_startset);
            if (this.A_StartSet_Size == 0)
                throw new NotSupportedException(SRM.Regex._DFA_incompatible_with + "characterless regex");

            this.A_StartSet = BooleanDecisionTree.Create(css, builder.solver.ConvertToCharSet(css, A_startset));

            this.A_prefix_array = A.GetPrefix();
            this.A_prefix = A.GetFixedPrefix(css, out this.A_fixedPrefix_ignoreCase);
            this.A_prefixUTF8 = System.Text.UnicodeEncoding.UTF8.GetBytes(this.A_prefix);
            this.Ar_prefix_array = Ar.GetPrefix();
            this.Ar_prefix = new string(Array.ConvertAll(this.Ar_prefix_array, x => (char)css.GetMin(builder.solver.ConvertToCharSet(css, x))));

            //InitializeVectors();
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
                // only the default previous character kind is going to be used for all initial states
                _Aq0[(int)CharKindId.None] = State<S>.MkState(A, CharKindId.None, false);
                _A1q0[(int)CharKindId.None] = State<S>.MkState(A1, CharKindId.None, false);
                // _A1q0[0] is recognized as special initial state,
                // this information is used for search optimization based on start set and prefix of A
                _A1q0[(int)CharKindId.None].isInitialState = true;
                // do not mark states of Ar as reverse because this info is irrelevant when no anchors are used
                _Arq0[(int)CharKindId.None] = State<S>.MkState(Ar, CharKindId.None, false);
            }
            else
            {
                for (int i=0; i < 6; i++)
                {
                    _Aq0[i] = State<S>.MkState(A, (CharKindId)i, false);
                    _A1q0[i] = State<S>.MkState(A1, (CharKindId)i, false);
                    // each _A1q0[i] is recognized as special initial state,
                    // this information is used for search optimization based on start set and prefix of A
                    _A1q0[i].isInitialState = true;
                    // mark states of Ar in reverse only if line anchors are used somewhere
                    // this effects the semantics of nulllability of line anchors as the
                    // character kind order is then relevant (prev vs next character kind)
                    // not marking states in reverse utilizes exiting state space better
                    _Arq0[i] = State<S>.MkState(Ar, (CharKindId)i, A.info.ContainsLineAnchor ? true : false);
                }
            }
        }

//        private void InitializeVectors()
//        {
//#if UNSAFE
//            if (A_StartSet_Size > 0 && A_StartSet_Size <= StartSetSizeLimit)
//            {
//                char[] startchars = new List<char>(builder.solver.GenerateAllCharacters(A_startset)).ToArray();
//                A_StartSet_Vec = Array.ConvertAll(startchars, c => new Vector<ushort>(c));
//                A_StartSet_singleton = (ushort)startchars[0];
//            }
//#endif

//            if (this.A_prefix != string.Empty)
//            {
//                this.A_prefixUTF8_first_byte = new Vector<byte>(this.A_prefixUTF8[0]);
//            }
//        }

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
            int atom_id = (c == 10 && i == input.Length ? atoms.Length : (c < dt.precomputed.Length ? dt.precomputed[c] : dt.bst.Find(c)));
            int offset = (q.Id << K) | atom_id;
            var p = delta[offset];
            if (p == null)
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
        /// Generate all matches.
        /// <param name="isMatch">if true return null iff there exists a match</param>
        /// <param name="input">input string</param>
        /// <param name="startat">the position to start search in the input string</param>
        /// <param name="endat">end position in the input, negative value means unspecified and taken to be input.Length-1</param>
        /// </summary>
        public Match FindMatch(bool isMatch, string input, int startat = 0, int endat = -1)
        {
#if UNSAFE
            if ((Options & RegexOptions.Vectorize) != RegexOptions.None)
            {
                return FindMatch_(input, 1, startat, endat);
            }
#endif
            return FindMatchSafe(isMatch, input, startat, endat);
        }

        #region safe version of Matches and IsMatch for string input

        /// <summary>
        /// Find a match.
        /// <param name="quick">if true return null iff there exists a match</param>
        /// <param name="input">input string</param>
        /// <param name="startat">the position to start search in the input string</param>
        /// <param name="endat">end position in the input, negative value means unspecified and taken to be input.Length-1</param>
        /// </summary>
        internal Match FindMatchSafe(bool quick, string input, int startat = 0, int endat = -1)
        {
#if DEBUG
            if (string.IsNullOrEmpty(input))
                    throw new ArgumentException($"'{nameof(input)}' must be a nonempty string");

            if (startat >= input.Length || startat < 0)
                    throw new ArgumentOutOfRangeException(nameof(startat));
#endif

            int k = ((endat < 0 | endat >= input.Length) ? input.Length : endat + 1);

            //find the first accepting state
            //initial start position in the input is i = 0
            int i = startat;

            bool AisSingleSeq = A.IsSequenceOfSingletons;

            int i_q0_A1;
            int watchdog;
            i = FindFinalStatePosition(input, k, i, out i_q0_A1, out watchdog);

            if (i == k)
            {
                //end of input has been reached without reaching a final state, so no match exists
                return Match.NoMatch;
            }
            else
            {
                if (quick)
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
                    ////If A is lazy then there is no need to maximize length of end-position
                    //if (AisLazy)
                    //{
                    //    if (AisSingleSeq)
                    //        i_start = i - A.sequenceOfSingletons_count + 1;
                    //    else
                    //        i_start = FindStartPosition(input, i, i_q0_A1);
                    //    i_end = i;
                    //}
                    if (AisSingleSeq)
                    {
                        // TBD: this case should be covered by watchdog
                        i_start = i - A.sequenceOfSingletons_count + 1;
                        i_end = i;
                    }
                    else
                    {
                        i_start = FindStartPosition(input, i, i_q0_A1);
                        i_end = FindEndPosition(input, k, i_start);
                    }
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
            CharKindId prevCharKindId = GetCharKindId(input, i - 1);
            // pick the correct start state based on previous character kind
            State<S> q = _Aq0[(int)prevCharKindId];
            while (i < k)
            {
                //TBD: prefix optimization for A, i.e., to skip ahead
                //over the initial prefix once it has been computed
                q = Delta(input, i, q);

                if (q.IsNullable(input, i+1))
                {
                    // stop here if q is not eager
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
            CharKindId prevKind = GetCharKindId(input, i + 1);
            State<S> q = _Arq0[(int)prevKind];
            //Ar may have a fixed prefix sequence
            if (Ar_prefix_array.Length > 0)
            {
                //skip past the prefix portion of Ar
                q = GetAr_skipState(prevKind);
                i = i - this.Ar_prefix_array.Length;
            }
            if (i == -1)
            {
#if DEBUG
                //we reached the beginning of the input, thus the state q must be accepting
                if (!q.IsNullable(input, i))
                    throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
                return 0;
            }

            int last_start = -1;
            if (q.IsNullable(input, i))
            {
                // the whole prefix of Ar was in reverse a prefix of A
                // for example when the pattern of A is concrete word such as "abc"
                last_start = i + 1;
            }

            //walk back to the accepting state of Ar
            while (i >= match_start_boundary)
            {
                q = Delta(input, i, q);

                if (q.IsNullable(input, i-1))
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
        /// FindFinalStatePosition is optimized for the case when A starts with a fixed prefix
        /// </summary>
        /// <param name="input">given input string</param>
        /// <param name="i">start position</param>
        /// <param name="i_q0">last position the initial state of A1 was visited</param>
        /// <param name="k">input length or bounded input length</param>
        /// <param name="watchdog">length of the match or -1</param>
        private int FindFinalStatePosition(string input, int k, int i, out int i_q0, out int watchdog)
        {
            // get the correct start state of A1,
            // which in general depends on the previous character kind in the input
            CharKindId prevCharKindId = GetCharKindId(input, i - 1);
            State<S> q = _A1q0[(int)prevCharKindId];
            int i_q0_A1 = i;
            // use Ordinal/OrdinalIgnoreCase to avoid culture dependent semantics of IndexOf
            StringComparison comparison = (this.A_fixedPrefix_ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            watchdog = -1;

            // search for a match end position within input[i..k-1]
            while (i < k)
            {
                if (this.A_prefix != string.Empty)
                {
                    // ++++ the prefix optimization can be omitted without affecting correctness ++++
                    // but this optimization has a major perfomance boost when a fixed prefix exists
                    // .... in some cases in the order of 10x
                    // TBD: checking correctness when anchors are used
                    #region prefix optimization
                    //stay in the initial state if the prefix does not match
                    //thus advance the current position to the
                    //first position where the prefix does match
                    if (q.isInitialState)
                    {
                        i_q0_A1 = i;
                        i = input.IndexOf(this.A_prefix, i, comparison);

                        if (i == -1)
                        {
                            // when a matching position does not exist then IndexOf returns -1
                            i_q0 = i_q0_A1;
                            watchdog = -1;
                            return k;
                        }
                        else
                        {
                            //compute the end state for the A prefix
                            //skip directly to the resulting state
                            // --- i.e. do the loop ---
                            //for (int j = 0; j < prefix.Length; j++)
                            //    q = Delta(prefix[j], q, out regex);
                            // ---
                            q = GetA1_skipState(q.PrevCharKindId);

                            // skip the prefix
                            i = i + this.A_prefix.Length;
                            // here i points at the next character (the character immediately following the prefix)
                            if (q.IsNullable(input, i))
                            {
                                i_q0 = i_q0_A1;
                                watchdog = GetWatchdog(q.Node);
                                // return the last position of the match
                                return i - 1;
                            }
                            if (i == k)
                            {
                                // no match was found
                                i_q0 = i_q0_A1;
                                watchdog = -1;
                                return k;
                            }
                        }
                    }
                    #endregion
                }
                else if (q.isInitialState)
                {
                    // we are still in the initial state
                    // find the first position i that matches with some character in the start set
                    i = IndexOfStartset(input, i);

                    if (i == -1)
                    {
                        // no match was found
                        i_q0 = i_q0_A1;
                        watchdog = -1;
                        return k;
                    }

                    i_q0_A1 = i;
                    // the start state must be updated
                    // to reflect the kind of the previous character
                    // when anchors are not used, q will remain the same state
                    q = _A1q0[(int)GetCharKindId(input, i - 1)];
                }

                // make the transition based on input[i]
                q = Delta(input, i, q);

                if (q.IsNullable(input, i + 1))
                {
                    i_q0 = i_q0_A1;
                    watchdog = GetWatchdog(q.Node);
                    return i;
                }
                else if (q.IsNothing)
                {
                    //q is a deadend state so any further search is meaningless
                    i_q0 = i_q0_A1;
                    return k;
                }
                // continue from the next character
                i += 1;
            }

            i_q0 = i_q0_A1;
            return k;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CharKindId GetCharKindId(string input, int i)
        {
            if (A.info.ContainsSomeAnchor)
            {
                if (i == -1)
                    return CharKindId.Start;
                else if (i == input.Length)
                    return CharKindId.End;
                else
                {
                    if (builder.newLinePredicate.Equals(builder.wordLetterPredicate))
                    {
                        // both predicates being the same means that they are both False
                        // the regex does then not use any anchors that depend on \n
                        return CharKindId.None;
                    }
                    else
                    {
                        char nextChar = input[i];

                        if (nextChar == '\n')
                        {
                            if (builder.newLinePredicate.Equals(builder.solver.False))
                                return 0;
                            else
                            {
                                if (i == input.Length - 1)
                                    return CharKindId.NewLineZ;
                                else
                                    return CharKindId.Newline;
                            }
                        }
                        else
                        {
                            if (builder.wordLetterPredicate.Equals(builder.solver.False))
                                return 0;
                            else
                            {
                                S nextCharAtom = atoms[nextChar < dt.precomputed.Length ? dt.precomputed[nextChar] : dt.bst.Find(nextChar)];
                                if (builder.solver.IsSatisfiable(builder.solver.MkAnd(builder.wordLetterPredicate, nextCharAtom)))
                                    return CharKindId.WordLetter;
                                else
                                    return CharKindId.None;
                                //TBD: alternative, should test if this makes any difference in efficiency
                                // if (System.Text.RegularExpressions.RegexCharClass.IsWordChar(nextChar))
                                //     return CharKindId.WordLetter;
                                // else
                                //     return CharKindId.None;
                            }
                        }
                    }
                }
            }
            else
            {
                // the previous character kind is irrelevant when anchors are not used
                return CharKindId.None;
            }
        }

        #endregion

#if UNSAFE

        #region unsafe version of Matches for string input

        /// <summary>
        /// Generate all earliest maximal matches. We know that k is at least 2. Unsafe version of Matches.
        /// <param name="input">pointer to input string</param>
        /// <param name="limit">upper bound on the number of found matches, nonpositive value (default is 0) means no bound</param>
        /// </summary>
        unsafe public List<Match> FindMatch_(string input, int limit = 0, int startat = 0, int endat = -1)
        {
            int k = ((endat < 0 | endat >= input.Length) ? input.Length : endat + 1);
            //stores the accumulated matches
            List<Match> matches = new List<Match>();

            //find the first accepting state
            //initial start position in the input is i = 0
            int i = startat;

            //after a match is found the match_start_boundary becomes 
            //the first postion after the last match
            //enforced when inlcude_overlaps == false
            int match_start_boundary = startat;

            //TBD: dont enforce match_start_boundary when match overlaps are allowed
            bool A_has_nonempty_prefix = (this.A_prefix != string.Empty);
            fixed (char* inputp = input)
                if (A_has_nonempty_prefix)
                {
                while (true)
                {
                    int i_q0_A1;
                        i = FindFinalStatePositionOpt_(input, i, out i_q0_A1);

                        if (i == k)
                    {
                            //end of input has been reached without reaching a final state, so no more matches
                            break;
                        }

                        int i_start = FindStartPosition_(inputp, input.Length, i, i_q0_A1);

                        int i_end = FindEndPosition_(inputp, k, i_start);

                        var newmatch = new Match(i_start, i_end + 1 - i_start);
                        matches.Add(newmatch);
                        if (limit > 0 && matches.Count == limit)
                            break;

                        //continue matching from the position following last match
                        i = i_end + 1;
                        match_start_boundary = i;
                    }
                    }
                    else
                    {
                    while (true)
                    {
                        int i_q0_A1;
                        i = FindFinalStatePosition_(inputp, k, i, out i_q0_A1);

                    if (i == k)
                    {
                        //end of input has been reached without reaching a final state, so no more matches
                        break;
                    }

                    int i_start = FindStartPosition_(inputp, input.Length, i, i_q0_A1);

                    int i_end = FindEndPosition_(inputp, k, i_start);

                    var newmatch = new Match(i_start, i_end + 1 - i_start);
                    matches.Add(newmatch);
                    if (limit > 0 && matches.Count == limit)
                        break;

                    //continue matching from the position following last match
                    i = i_end + 1;
                    match_start_boundary = i;
                }
                }

            return matches;
        }

        /// <summary>
        /// Return the position of the last character that leads to a final state in A1
        /// </summary>
        /// <param name="inputp">given input string</param>
        /// <param name="k">length of input</param>
        /// <param name="i">start position</param>
        /// <param name="i_q0">last position the initial state of A1 was visited</param>
        /// <returns></returns>
        unsafe private int FindFinalStatePosition_(char* inputp, int k, int i, out int i_q0)
        {
            int q = q0_A1;
            int i_q0_A1 = i;
            while (i < k)
            {
                if (q == q0_A1)
                {
                    if (this.A_StartSet_Vec != null && A_StartSet_Vec.Length == 1)
                    {
                        i = VectorizedIndexOf.UnsafeIndexOf1(inputp, k, i, this.A_StartSet_singleton, A_StartSet_Vec[0]);
                    }
                    else
                    {
                        i = IndexOfStartset_(inputp, k, i);
                    }

                    if (i == -1)
                    {
                        i_q0 = i_q0_A1;
                        return k;
                    }
                    i_q0_A1 = i;
                }

                //TBD: anchors
                SymbolicRegexNode<S> regex;
                int c = inputp[i];
                int p;

                if (c == 10)
                {
                    p = DeltaBorder(BorderSymbol.EOL, q, out regex);
                    if (regex.isNullable)
                    {
                        //match has been found due to endline anchor
                        //so the match actually ends at the prior character 
                        //unless the prior character does not exist
                        i = (i > 0 ? i - 1 : 0);
                        break;
                    }
                    p = Delta(10, p, out regex);
                    if (regex.isNullable)
                    {
                        //match has been found due to newline itself
                        //this can happen if anchor is not used
                        //but the newline character is used in the pattern
                        break;
                    }
                    p = DeltaBorder(BorderSymbol.BOL, q, out regex);
                    if (regex.isNullable)
                    {
                        //match has been found due to startline anchor
                        //highly unusual case that should not really happen
                        //in this case newline is part of the match
                        break;
                    }
                    if (regex == this.builder.nothing)
                    {
                        //p is a deadend state so any further search is meaningless
                        i_q0 = i_q0_A1;
                        return k;
                    }
                }
                else
                {
                    p = Delta(c, q, out regex);

                    if (regex.isNullable)
                    {
                        //p is a final state so match has been found
                        break;
                    }
                    else if (regex == this.builder.nothing)
                    {
                        //p is a deadend state so any further search is meaningless
                        i_q0 = i_q0_A1;
                        return k;
                    }
                }

                //continue from the target state
                q = p;
                i += 1;
            }
            i_q0 = i_q0_A1;
            return i;
        }

        /// <summary>
        /// FindFinalState optimized for the case when A starts with a fixed prefix and does not ignore case
        /// </summary>
        unsafe private int FindFinalStatePositionOpt_(string input, int i, out int i_q0)
        {
            int q = q0_A1;
            int i_q0_A1 = i;
            var A_prefix_length = this.A_prefix.Length;
            //it is important to use Ordinal/OrdinalIgnoreCase to avoid culture dependent semantics of IndexOf
            StringComparison comparison = (this.A_fixedPrefix_ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            int k = input.Length;
            fixed (char* inputp = input)
                while (i < k)
                {
                    SymbolicRegexNode<S> regex = null;

        #region prefix optimization
                    //stay in the initial state if the prefix does not match
                    //thus advance the current position to the 
                    //first position where the prefix does match
                    if (q == q0_A1)
                    {
                        i_q0_A1 = i;

                        if (this.A_fixedPrefix_ignoreCase)
                            i = input.IndexOf(A_prefix, i, comparison);
                        else
                            i = VectorizedIndexOf.UnsafeIndexOf(inputp, k, i, A_prefix);

                        if (i == -1)
                        {
                            //if a matching position does not exist then IndexOf returns -1
                            //so set i = k to match the while loop behavior
                            i = k;
                            break;
                        }
                        else
                        {
                            //compute the end state for the A prefix
                            //skip directly to the resulting state
                            // --- i.e. does the loop ---
                            //for (int j = 0; j < prefix.Length; j++)
                            //    q = Delta(prefix[j], q, out regex);
                            // ---
                            q = this.A1_skipState;
                            regex = this.A1_skipStateRegex;

                            //skip the prefix
                            i = i + A_prefix_length;
                            if (regex.isNullable)
                            {
                                i_q0 = i_q0_A1;
                                //return the last position of the match
                                return i - 1;
                            }
                            if (i == k)
                            {
                                i_q0 = i_q0_A1;
                                return k;
                            }
                        }
                    }
        #endregion

                    int c = inputp[i];
                    int p;

                    if (c == 10)
                    {
                        p = DeltaBorder(BorderSymbol.EOL, q, out regex);
                        if (regex.isNullable)
                        {
                            //match has been found due to endline anchor
                            //so the match actually ends at the prior character 
                            //unless the prior character does not exist
                            i = (i > 0 ? i - 1 : 0);
                            break;
                        }
                        p = Delta(10, p, out regex);
                        if (regex.isNullable)
                        {
                            //match has been found due to newline itself
                            //this can happen if anchor is not used
                            //but the newline character is used in the pattern
                            break;
                        }
                        p = DeltaBorder(BorderSymbol.BOL, q, out regex);
                        if (regex.isNullable)
                        {
                            //match has been found due to startline anchor
                            //highly unusual case that should not really happen
                            //in this case newline is part of the match
                            break;
                        }
                        if (regex == this.builder.nothing)
                        {
                            //p is a deadend state so any further search is meaningless
                            i_q0 = i_q0_A1;
                            return k;
                        }
                    }
                    else
                    {
                        p = Delta(c, q, out regex);

                        if (regex.isNullable)
                        {
                            //p is a final state so match has been found
                            break;
                        }
                        else if (regex == this.builder.nothing)
                        {
                            //p is a deadend state so any further search is meaningless
                            i_q0 = i_q0_A1;
                            return k;
                        }
                    }

                    //continue from the target state
                    q = p;
                    i += 1;
                }
            i_q0 = i_q0_A1;
            return i;
        }

        /// <summary>
        /// Walk back in reverse using Ar to find the start position of match, start position is known to exist.
        /// </summary>
        /// <param name="input">the input array</param>
        /// <param name="i">position to start walking back from, i points at the last character of the match</param>
        /// <param name="match_start_boundary">do not pass this boundary when walking back</param>
        /// <returns></returns>
        unsafe private int FindStartPosition_(char* input, int input_length, int i, int match_start_boundary)
        {
            int q = q0_Ar;
            SymbolicRegexNode<S> regex = null;
            //A_r may have a fixed sequence
            if (this.Ar_prefix_array.Length > 0)
            {
                //skip back the prefix portion of Ar
                q = this.Ar_skipState;
                regex = this.Ar_skipStateRegex;
                i = i - this.Ar_prefix_array.Length;
            }
            if (i == -1)
            {
                //we reached the beginning of the input, thus the state q must be accepting
                if (!regex.isNullable)
                    throw new AutomataException(AutomataExceptionKind.InternalError);
                return 0;
            }

            int last_start = -1;
            if (regex != null && regex.isNullable)
            {
                //the whole prefix of Ar was in reverse a prefix of A
                last_start = i + 1;
            }

            //walk back to the accepting state of Ar
            int p;
            int c;

            if (i == input_length - 1)
                // at the end of the input
                q = DeltaBorder(BorderSymbol.End, q, out _);
            else if (i > 0 && input[i + 1] == '\n')
                // at the end of a line
                q = DeltaBorder(BorderSymbol.EOL, q, out _);

            while (i >= match_start_boundary)
            {
                //observe that the input is reversed 
                //so input[k-1] is the first character 
                //and input[0] is the last character
                //TBD: anchors
                c = input[i];

                if (c == 10)
                {
                    //going backwards, first consume StartLine because reversal keeps the anchors in place
                    p = DeltaBorder(BorderSymbol.BOL, q, out regex);
                    if (regex.IsNullable)
                        last_start = i + 1;
                    p = Delta(10, p, out _);
                    p = DeltaBorder(BorderSymbol.BOL, p, out regex);
                }
                else
                    p = Delta(c, q, out regex);

                if (regex.isNullable)
                {
                    //earliest start point so far
                    //this must happen at some point 
                    //or else A1 would not have reached a 
                    //final state after match_start_boundary
                    last_start = i;
                    //TBD: under some conditions we can break here
                    //break;
                }
                else if (regex == this.builder.nothing)
                {
                    //the previous i_start was in fact the earliest
                    break;
                }
                q = p;
                i -= 1;
            }
            if (last_start == -1)
                throw new AutomataException(AutomataExceptionKind.InternalError);
            return last_start;
        }

        /// <summary>
        /// Find match end position using A, end position is known to exist.
        /// </summary>
        /// <param name="input">input array</param>
        /// <param name="k">length of input</param>
        /// <param name="i">start position</param>
        /// <returns></returns>
        unsafe private int FindEndPosition_(char* input, int k, int i)
        {
            int i_end = k;
            int q = q0_A;
            SymbolicRegexNode<S> regex;
            if (i == 0)
                // start of input
                q = DeltaBorder(BorderSymbol.Beg, q, out _);
            else if (input[i - 1] == '\n')
                // start of a line
                q = DeltaBorder(BorderSymbol.BOL, q, out _);

            while (i < k)
            {
                int c = input[i];
                int p;

                if (c == 10)
                {
                    p = DeltaBorder(BorderSymbol.EOL, q, out regex);
                    if (regex.IsNullable)
                        //nullable due to $ anchor
                        //end position is therefore the prior character if it exists
                        i_end = (i > 0 ? i - 1 : 0);
                    p = Delta(10, p, out regex);
                    p = DeltaBorder(BorderSymbol.BOL, p, out regex);
                }
                else
                    p = Delta(c, q, out regex);


                if (regex.isNullable)
                {
                    //accepting state has been reached
                    //record the position 
                    i_end = i;
                }
                else if (regex == builder.nothing)
                {
                    //nonaccepting sink state (deadend) has been reached in A
                    //so the match ended when the last i_end was updated
                    break;
                }
                q = p;
                i += 1;
            }
            if (i == k)
            {
                DeltaBorder(BorderSymbol.End, q, out regex);
                if (regex.IsNullable)
                    //match occurred due to end anchor
                    //this must be the case here
                    i_end = k - 1;
            }
            if (i_end == k)
                throw new AutomataException(AutomataExceptionKind.InternalError);
            return i_end;
        }

        #endregion

#endif

        #region Specialized IndexOf
        /// <summary>
        ///  Find first occurrence of startset element in input starting from index i.
        /// </summary>
        /// <param name="input">input string to search in</param>
        /// <param name="i">the start index in input to search from</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexOfStartset(string input, int i)
        {
            int k = input.Length;
            while (i < k)
            {
                var input_i = input[i];
                if (input_i < A_StartSet.precomputed.Length ? A_StartSet.precomputed[input_i] : A_StartSet.bst.Find(input_i) == 1)
                    break;
                else
                    i += 1;
            }
            if (i == k)
                return -1;
            else
                return i;
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

                if (c < A_StartSet.precomputed.Length ? A_StartSet.precomputed[c] : A_StartSet.bst.Find(c) == 1)
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

#if UNSAFE
        /// <summary>
        ///  Find first occurrence of startset element in input starting from index i.
        /// </summary>
        /// <param name="input">input string to search in</param>
        /// <param name="k">length of the input</param>
        /// <param name="i">the start index in input to search from</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe int IndexOfStartset_(char* input, int k, int i)
        {
            while (i < k)
            {
                var input_i = input[i];
                if (input_i < A_StartSet.precomputed.Length ? A_StartSet.precomputed[input_i] : A_StartSet.bst.Find(input_i) == 1)
                    break;
                else
                    i += 1;
            }
            if (i == k)
                return -1;
            else
                return i;
        }

        /// <summary>
        ///  Find first occurrence of s in input starting from index i.
        ///  This method is called when A has nonemmpty prefix and ingorecase is false
        /// </summary>
        /// <param name="input">input string to search in</param>
        /// <param name="k">length of input string</param>
        /// <param name="i">the start index in input</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe int IndexOfStartPrefix_(char* input, int k, int i)
        {
            int l = this.A_prefix.Length;
            int k1 = k - l + 1;
            var vec = A_StartSet_Vec[0];
            fixed (char* p = this.A_prefix)
            {
                while (i < k1)
                {
                    i = VectorizedIndexOf.UnsafeIndexOf1(input, k, i, p[0], vec);

                    if (i == -1)
                        return -1;
                    int j = 1;
                    while (j < l && input[i + j] == p[j])
                        j += 1;
                    if (j == l)
                        return i;

                    i += 1;
                }
            }
            return -1;
        }
#endif

        #endregion

        //#region Matches that uses UTF-8 encoded byte array as input

        ///// <summary>
        ///// Generate all earliest maximal matches.
        ///// <paramref name="input">pointer to input string</paramref>
        ///// </summary>
        //internal List<Match> MatchesUTF8(byte[] input)
        //{
        //    int k = input.Length;

        //    //stores the accumulated matches
        //    List<Match> matches = new List<Match>();

        //    //find the first accepting state
        //    //initial start position in the input is i = 0
        //    int i = 0;

        //    //after a match is found the match_start_boundary becomes
        //    //the first postion after the last match
        //    //enforced when inlcude_overlaps == false
        //    int match_start_boundary = 0;

        //    int surrogate_codepoint = 0;

        //    //TBD: dont enforce match_start_boundary when match overlaps are allowed
        //    bool A_has_nonempty_prefix = (this.A_prefix != string.Empty);
        //    while (true)
        //    {
        //        int i_q0_A1;
        //        //TBD: optimize for the case when A starts with a fixed prefix
        //        i = FindFinalStatePositionUTF8(input, i, ref surrogate_codepoint, out i_q0_A1);

        //        if (i == k)
        //        {
        //            //end of input has been reached without reaching a final state, so no more matches
        //            break;
        //        }

        //        int i_start = FindStartPositionUTF8(input, i, ref surrogate_codepoint, i_q0_A1);

        //        int i_end = FindEndPositionUTF8(input, i_start, ref surrogate_codepoint);

        //        var newmatch = new Match(i_start, i_end + 1 - i_start);
        //        matches.Add(newmatch);

        //        //continue matching from the position following last match
        //        i = i_end + 1;
        //        match_start_boundary = i;
        //    }

        //    return matches;
        //}

        ///// <summary>
        ///// Find match end position using A, end position is known to exist.
        ///// </summary>
        ///// <param name="input">input array</param>
        ///// <param name="i">start position</param>
        ///// <param name="surrogate_codepoint">surrogate codepoint</param>
        ///// <returns></returns>
        //private int FindEndPositionUTF8(byte[] input, int i, ref int surrogate_codepoint)
        //{
        //    int k = input.Length;
        //    int i_end = k;
        //    int q = q0_A;
        //    int step = 0;
        //    int codepoint = 0;
        //    SymbolicRegexNode<S> regex;
        //    if (i == 0)
        //        // start of input
        //        q = DeltaBorder(BorderSymbol.Beg, q, out _);
        //    else if (input[i - 1] == '\n')
        //        // start of a line
        //        q = DeltaBorder(BorderSymbol.BOL, q, out _);

        //    while (i < k)
        //    {
        //        ushort c;
        //        #region c = current UTF16 character
        //        if (surrogate_codepoint == 0)
        //        {
        //            c = input[i];
        //            if (c > 0x7F)
        //            {
        //                int x;
        //                UTF8Encoding.DecodeNextNonASCII(input, i, out x, out codepoint);
        //                if (codepoint > 0xFFFF)
        //                {
        //                    surrogate_codepoint = codepoint;
        //                    c = UTF8Encoding.HighSurrogate(codepoint);
        //                    //do not increment i yet because L is pending
        //                    step = 0;
        //                }
        //                else
        //                {
        //                    c = (ushort)codepoint;
        //                    //step is either 2 or 3, i.e. either 2 or 3 UTF-8-byte encoding
        //                    step = x;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            c = UTF8Encoding.LowSurrogate(surrogate_codepoint);
        //            //reset the surrogate_codepoint
        //            surrogate_codepoint = 0;
        //            //increment i by 4 since low surrogate has now been read
        //            step = 4;
        //        }
        //        #endregion

        //        int p;


        //        if (c == 10)
        //        {
        //            p = DeltaBorder(BorderSymbol.EOL, q, out regex);
        //            if (regex.IsNullable)
        //                //nullable due to $ anchor
        //                //end position is therefore the prior character if it exists
        //                i_end = (i > 0 ? i - 1 : 0);
        //            p = Delta(10, p, out _);
        //            p = DeltaBorder(BorderSymbol.BOL, p, out regex);
        //        }
        //        else
        //            p = Delta(c, q, out regex);

        //        if (regex.IsNullable)
        //        {
        //            //accepting state has been reached
        //            //record the position
        //            i_end = i;
        //        }
        //        else if (regex == builder.nothing)
        //        {
        //            //nonaccepting sink state (deadend) has been reached in A
        //            //so the match ended when the last i_end was updated
        //            break;
        //        }
        //        q = p;
        //        if (c > 0x7F)
        //            i += step;
        //        else
        //            i += 1;
        //    }
        //    if (i == k)
        //    {
        //        DeltaBorder(BorderSymbol.End, q, out regex);
        //        if (regex.IsNullable)
        //            //match occurred due to end anchor
        //            //this must be the case here
        //            //TBD: adjust offset according to uft8 if nonascii
        //            i_end = k - 1;
        //    }
        //    if (i_end == k)
        //        throw new AutomataException(AutomataExceptionKind.InternalError);
        //    return i_end;
        //}

        ///// <summary>
        ///// Walk back in reverse using Ar to find the start position of match, start position is known to exist.
        ///// </summary>
        ///// <param name="input">the input array</param>
        ///// <param name="i">position to start walking back from, i points at the last character of the match</param>
        ///// <param name="match_start_boundary">do not pass this boundary when walking back</param>
        ///// <param name="surrogate_codepoint">surrogate codepoint</param>
        ///// <returns></returns>
        //private int FindStartPositionUTF8(byte[] input, int i, ref int surrogate_codepoint, int match_start_boundary)
        //{
        //    int q = q0_Ar;
        //    SymbolicRegexNode<S> regex = null;
        //    //A_r may have a fixed sequence
        //    if (this.Ar_prefix_array.Length > 0)
        //    {
        //        //skip back the prefix portion of Ar
        //        q = this.Ar_skipState;
        //        regex = this.Ar_skipStateRegex;
        //        i = i - this.Ar_prefix_array.Length;
        //    }
        //    if (i == -1)
        //    {
        //        //we reached the beginning of the input, thus the state q must be accepting
        //        if (!regex.IsNullable)
        //            throw new AutomataException(AutomataExceptionKind.InternalError);
        //        return 0;
        //    }

        //    int last_start = -1;
        //    if (regex != null && regex.IsNullable)
        //    {
        //        //the whole prefix of Ar was in reverse a prefix of A
        //        last_start = i + 1;
        //    }

        //    //walk back to the accepting state of Ar
        //    int p;
        //    ushort c;
        //    int codepoint;

        //    // TBD: calculation of next character for nonascii is not 1 but 2 or 3 bytes off
        //    if (i == input.Length - 1)
        //        // at the end of the input
        //        q = DeltaBorder(BorderSymbol.End, q, out _);
        //    else if (i > 0 && input[i + 1] == '\n')
        //        // at the end of a line
        //        q = DeltaBorder(BorderSymbol.EOL, q, out _);

        //    while (i >= match_start_boundary)
        //    {
        //        //observe that the input is reversed
        //        //so input[k-1] is the first character
        //        //and input[0] is the last character
        //        //but encoding is not reversed
        //        //TBD: anchors

        //        #region c = current UTF16 character
        //        if (surrogate_codepoint == 0)
        //        {
        //            //not in the middel of surrogate codepoint
        //            c = input[i];
        //            if (c > 0x7F)
        //            {
        //                int _;
        //                UTF8Encoding.DecodeNextNonASCII(input, i, out _, out codepoint);
        //                if (codepoint > 0xFFFF)
        //                {
        //                    //given codepoint = ((H - 0xD800) * 0x400) + (L - 0xDC00) + 0x10000
        //                    surrogate_codepoint = codepoint;
        //                    //compute c = L (going backwards)
        //                    c = (ushort)(((surrogate_codepoint - 0x10000) & 0x3FF) | 0xDC00);
        //                }
        //                else
        //                {
        //                    c = (ushort)codepoint;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            //given surrogate_codepoint = ((H - 0xD800) * 0x400) + (L - 0xDC00) + 0x10000
        //            //compute c = H (going backwards)
        //            c = (ushort)(((surrogate_codepoint - 0x10000) >> 10) | 0xD800);
        //            //reset the surrogate codepoint
        //            surrogate_codepoint = 0;
        //        }
        //        #endregion

        //        if (c == 10)
        //        {
        //            //going backwards, first consume StartLine because reversal keeps the anchors in place
        //            p = DeltaBorder(BorderSymbol.BOL, q, out regex);
        //            if (regex.IsNullable)
        //                last_start = i + 1;
        //            p = Delta(10, p, out _);
        //            p = DeltaBorder(BorderSymbol.BOL, p, out regex);
        //        }
        //        else
        //            p = Delta(c, q, out regex);

        //        if (regex.IsNullable)
        //        {
        //            //earliest start point so far
        //            //this must happen at some point
        //            //or else A1 would not have reached a
        //            //final state after match_start_boundary
        //            last_start = i;
        //            //TBD: under some conditions we can break here
        //            //break;
        //        }
        //        else if (regex == this.builder.nothing)
        //        {
        //            //the previous i_start was in fact the earliest
        //            surrogate_codepoint = 0;
        //            break;
        //        }
        //        if (surrogate_codepoint == 0)
        //        {
        //            i = i - 1;
        //            //step back to the previous input, /while input[i] is not a start-byte take a step back
        //            //check (0x7F < b && b < 0xC0) imples that 0111.1111 < b < 1100.0000
        //            //so b cannot be ascii 0xxx.xxxx or startbyte 110x.xxxx or 1110.xxxx or 1111.0xxx
        //            while ((i >= match_start_boundary) && (0x7F < input[i] && input[i] < 0xC0))
        //                i = i - 1;
        //        }
        //        q = p;
        //    }
        //    if (last_start == -1)
        //        throw new AutomataException(AutomataExceptionKind.InternalError);
        //    return last_start;
        //}

        ///// <summary>
        ///// Return the position of the last character that leads to a final state in A1
        ///// </summary>
        ///// <param name="input">given input array</param>
        ///// <param name="i">start position</param>
        ///// <param name="i_q0">last position the initial state of A1 was visited</param>
        ///// <param name="surrogate_codepoint">surrogate codepoint</param>
        ///// <returns></returns>
        //private int FindFinalStatePositionUTF8(byte[] input, int i, ref int surrogate_codepoint, out int i_q0)
        //{
        //    int k = input.Length;
        //    int q = q0_A1;
        //    int i_q0_A1 = i;
        //    int step = 0;
        //    int codepoint;
        //    SymbolicRegexNode<S> regex;
        //    bool prefix_optimize = (!this.A_fixedPrefix_ignoreCase) && this.A_prefixUTF8.Length > 1;
        //    while (i < k)
        //    {
        //        if (q == q0_A1)
        //        {
        //            if (prefix_optimize)
        //            {
        //                #region prefix optimization when A has a fixed prefix and is case-sensitive
        //                //stay in the initial state if the prefix does not match
        //                //thus advance the current position to the
        //                //first position where the prefix does match
        //                i_q0_A1 = i;

        //                i = VectorizedIndexOf.IndexOfByteSeq(input, i, this.A_prefixUTF8, this.A_prefixUTF8_first_byte);

        //                if (i == -1)
        //                {
        //                    //if a matching position does not exist then IndexOf returns -1
        //                    //so set i = k to match the while loop behavior
        //                    i = k;
        //                    break;
        //                }
        //                else
        //                {
        //                    //compute the end state for the A prefix
        //                    //skip directly to the resulting state
        //                    // --- i.e. do the loop ---
        //                    //for (int j = 0; j < prefix.Length; j++)
        //                    //    q = Delta(prefix[j], q, out regex);
        //                    // ---
        //                    q = this.A1_skipState;
        //                    regex = this.A1_skipStateRegex;

        //                    //skip the prefix
        //                    i = i + this.A_prefixUTF8.Length;
        //                    if (regex.IsNullable)
        //                    {
        //                        i_q0 = i_q0_A1;
        //                        //return the last position of the match
        //                        //make sure to step back to the start byte
        //                        i = i - 1;
        //                        //while input[i] is not a start-byte take a step back
        //                        while (0x7F < input[i] && input[i] < 0xC0)
        //                            i = i - 1;
        //                    }
        //                    if (i == k)
        //                    {
        //                        i_q0 = i_q0_A1;
        //                        return k;
        //                    }
        //                }
        //                #endregion
        //            }
        //            else
        //            {
        //                i = (this.A_prefixUTF8.Length == 0 ?
        //                    IndexOfStartsetUTF8(input, i, ref surrogate_codepoint) :
        //                    VectorizedIndexOf.IndexOfByte(input, i, this.A_prefixUTF8[0], this.A_prefixUTF8_first_byte));

        //                if (i == -1)
        //                {
        //                    i_q0 = i_q0_A1;
        //                    return k;
        //                }
        //                i_q0_A1 = i;
        //            }
        //        }

        //        ushort c;

        //        #region c = current UTF16 character
        //        if (surrogate_codepoint == 0)
        //        {
        //            c = input[i];
        //            if (c > 0x7F)
        //            {
        //                int x;
        //                UTF8Encoding.DecodeNextNonASCII(input, i, out x, out codepoint);
        //                if (codepoint > 0xFFFF)
        //                {
        //                    //given codepoint = ((H - 0xD800) * 0x400) + (L - 0xDC00) + 0x10000
        //                    surrogate_codepoint = codepoint;
        //                    //compute c = H
        //                    c = (ushort)(((codepoint - 0x10000) >> 10) | 0xD800);
        //                    //do not increment i yet because L is pending
        //                    step = 0;
        //                }
        //                else
        //                {
        //                    c = (ushort)codepoint;
        //                    //step is either 2 or 3, i.e. either 2 or 3 UTF-8-byte encoding
        //                    step = x;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            //given surrogate_codepoint = ((H - 0xD800) * 0x400) + (L - 0xDC00) + 0x10000
        //            //compute c = L
        //            c = (ushort)(((surrogate_codepoint - 0x10000) & 0x3FF) | 0xDC00);
        //            //reset the surrogate_codepoint
        //            surrogate_codepoint = 0;
        //            //increment i by 4 since low surrogate has now been read
        //            step = 4;
        //        }
        //        #endregion


        //        int p;


        //        if (c == 10)
        //        {
        //            p = DeltaBorder(BorderSymbol.EOL, q, out regex);
        //            if (regex.IsNullable)
        //                break;
        //            p = Delta(10, p, out regex);
        //            p = DeltaBorder(BorderSymbol.BOL, p, out regex);
        //        }
        //        else
        //            p = Delta(c, q, out regex);

        //        if (regex.IsNullable)
        //        {
        //            //p is a final state so match has been found
        //            break;
        //        }
        //        else if (regex == this.builder.nothing)
        //        {
        //            //p is a deadend state so any further search is meaningless
        //            i_q0 = i_q0_A1;
        //            return k;
        //        }

        //        //continue from the target state
        //        q = p;
        //        if (c > 0x7F)
        //            i += step;
        //        else
        //            i += 1;
        //    }
        //    i_q0 = i_q0_A1;
        //    return i;
        //}
        //#endregion

    }
}
