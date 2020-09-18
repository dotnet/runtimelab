using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Microsoft.SRM
{
    /// <summary>
    /// Wraps an instance of SymbolicRegex&lt;BV&gt;, the number of needed partition blocks is &gt; 64
    /// </summary>
    [Serializable]
    internal class SymbolicRegexBV : SymbolicRegex<BV>
    {
        private SymbolicRegexBV(SymbolicRegexBuilder<BV> builder, SymbolicRegexNode<BDD> sr,
                                CharSetSolver solver, SymbolicRegexBuilder<BDD> srBuilder, BDD[] minterms, RegexOptions options, int StateLimit, int startSetSizeLimit)
            : base(srBuilder.Transform(sr, builder, builder.solver.ConvertFromCharSet),
                  solver, minterms, options, StateLimit, startSetSizeLimit)
        {
        }

        /// <summary>
        /// Is called with minterms.Length at least 65
        /// </summary>
        internal SymbolicRegexBV(SymbolicRegexNode<BDD> sr,
                                 CharSetSolver solver, SymbolicRegexBuilder<BDD> srBuilder, BDD[] minterms, RegexOptions options, int StateLimit = 1000, int startSetSizeLimit = 1)
            : this(new SymbolicRegexBuilder<BV>(BVAlgebra.Create(solver, minterms)), sr,
                  solver, srBuilder, minterms, options, StateLimit, startSetSizeLimit)
        {
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
                                CharSetSolver solver, SymbolicRegexBuilder<BDD> srBuilder, BDD[] minterms, RegexOptions options, int StateLimit, int startSetSizeLimit)
            : base(srBuilder.Transform(sr, builder, builder.solver.ConvertFromCharSet),
                  solver, minterms, options, StateLimit, startSetSizeLimit)
        {
        }

        /// <summary>
        /// Is called with minterms.Length at most 64
        /// </summary>
        internal SymbolicRegexUInt64(SymbolicRegexNode<BDD> sr,
                                 CharSetSolver solver, SymbolicRegexBuilder<BDD> srBuilder, BDD[] minterms, RegexOptions options, int StateLimit = 1000, int startSetSizeLimit = 1)
            : this(new SymbolicRegexBuilder<ulong>(BV64Algebra.Create(solver, minterms)), sr,
                  solver, srBuilder, minterms, options, StateLimit, startSetSizeLimit)
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
        S[] atoms;

        /// <summary>
        /// Maps each character into a partition id in the range 0..K-1.
        /// </summary>
        [NonSerialized]
        DecisionTree dt;

        /// <summary>
        /// Used only by IsMatch and if A2 is used.
        /// </summary>
        [NonSerialized]
        int q0_A2 = 0;

        /// <summary>
        /// Initial state of A1 (0 is not used).
        /// </summary>
        [NonSerialized]
        int q0_A1 = 1;

        /// <summary>
        /// Initial state of Ar (0 is not used).
        /// </summary>
        [NonSerialized]
        int q0_Ar = 2;

        /// <summary>
        /// Initial state of A (0 is not used).
        /// </summary>
        [NonSerialized]
        int q0_A = 3;

        /// <summary>
        /// Next available state id.
        /// </summary>
        [NonSerialized]
        int nextStateId = 4;

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

        /// <summary>
        /// First byte of A_prefixUTF8 in vector
        /// </summary>
        [NonSerialized]
        Vector<byte> A_prefixUTF8_first_byte;

        /// <summary>
        /// Original regex.
        /// </summary>
        [NonSerialized]
        internal SymbolicRegexNode<S> A;
        [NonSerialized]
        bool A_allLoopsAreLazy = false;
        [NonSerialized]
        bool A_containsLazyLoop = false;

        /// <summary>
        /// The RegexOptions this regex was created with
        /// </summary>
        public RegexOptions Options { get; set; }

        /// <summary>
        /// Main pattern of the matcher
        /// </summary>
        public SymbolicRegexNode<S> Pattern
        {
            get
            {
                return this.A;
            }
        }

        /// <summary>
        /// Reverse pattern of the matcher
        /// </summary>
        public SymbolicRegexNode<S> ReversePattern
        {
            get
            {
                return this.Ar;
            }
        }

        /// <summary>
        /// Dot star in front of the pattern of the matcher
        /// </summary>
        public SymbolicRegexNode<S> DotStarPattern
        {
            get
            {
                return this.A1;
            }
        }

        /// <summary>
        /// Set of elements that matter as first element of A. 
        /// </summary>
        internal BooleanDecisionTree A_StartSet;

        /// <summary>
        /// predicate over characters that make some progress
        /// </summary>
        S A_startset;

        /// <summary>
        /// Number of elements in A_StartSet
        /// </summary>
        int A_StartSet_Size;

        /// <summary>
        /// if nonempty then A has that fixed prefix
        /// </summary>
        string A_prefix;

        /// <summary>
        /// if nonempty then A has that fixed prefix
        /// </summary>>
        [NonSerialized]
        byte[] A_prefixUTF8;

        /// <summary>
        /// predicate array corresponding to fixed prefix of A
        /// </summary>
        S[] A_prefix_array;

        /// <summary>
        /// if true then the fixed prefix of A is idependent of case
        /// </summary>
        [NonSerialized]
        bool A_fixedPrefix_ignoreCase;

        /// <summary>
        /// precomputed state of A1 that is reached after the fixed prefix of A
        /// </summary>
        [NonSerialized]
        int A1_skipState;

        /// <summary>
        /// precomputed regex of A1 that is reached after the fixed prefix of A
        /// </summary>
        [NonSerialized]
        SymbolicRegexNode<S> A1_skipStateRegex;

        /// <summary>
        /// Reverse(A).
        /// </summary>
        [NonSerialized]
        SymbolicRegexNode<S> Ar;

        /// <summary>
        /// if nonempty then Ar has that fixed prefix of predicates
        /// </summary>
        S[] Ar_prefix_array;

        string Ar_prefix;

        /// <summary>
        /// precomputed state that is reached after the fixed prefix of Ar
        /// </summary>
        [NonSerialized]
        int Ar_skipState;

        /// <summary>
        /// precomputed regex that is reached after the fixed prefix of Ar
        /// </summary>
        [NonSerialized]
        SymbolicRegexNode<S> Ar_skipStateRegex;

        /// <summary>
        /// .*A
        /// </summary>
        [NonSerialized]
        SymbolicRegexNode<S> A1;

        /// <summary>
        /// Variant of A1 for matching.
        /// In A2 anchors have been removed. 
        /// Used only by IsMatch and when A contains anchors.
        /// </summary>
        [NonSerialized]
        SymbolicRegexNode<S> A2 = null;

        /// <summary>
        /// Initialized to atoms.Length.
        /// </summary>
        [NonSerialized]
        int K;

        /// <summary>
        /// Maps regexes to state ids
        /// </summary>
        [NonSerialized]
        Dictionary<SymbolicRegexNode<S>, int> regex2state = new Dictionary<SymbolicRegexNode<S>, int>();

        /// <summary>
        /// Maps states >= StateLimit to regexes.
        /// </summary>
        [NonSerialized]
        Dictionary<int, SymbolicRegexNode<S>> state2regexExtra = new Dictionary<int, SymbolicRegexNode<S>>();

        /// <summary>
        /// Maps states 1..(StateLimit-1) to regexes. 
        /// State 0 is not used but is reserved for denoting UNDEFINED value.
        /// Length of state2regex is StateLimit. Entry 0 is not used.
        /// </summary>
        [NonSerialized]
        SymbolicRegexNode<S>[] state2regex;

        /// <summary>
        /// Overflow from delta. Transitions with source state over the limit.
        /// Each entry (q, [p_0...p_n]) has n = atoms.Length-1 and represents the transitions q --atoms[i]--> p_i.
        /// All defined states are strictly positive, p_i==0 means that q --atoms[i]--> p_i is still undefined.
        /// </summary>
        [NonSerialized]
        Dictionary<int, int[]> deltaExtra = new Dictionary<int, int[]>();

        /// <summary>
        /// Bound on the maximum nr of states stored in array.
        /// </summary>
        internal readonly int StateLimit;

        /// <summary>
        /// Bound on the maximum nr of chars that trigger vectorized IndexOf.
        /// </summary>
        internal readonly int StartSetSizeLimit;

        /// <summary>
        /// Holds all transitions for states 1..MaxNrOfStates-1.
        /// each transition q ---atoms[i]---> p is represented by entry p = delta[(q * K) + i]. 
        /// Length of delta is K*StateLimit.
        /// </summary>
        [NonSerialized]
        int[] delta;

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

                info.AddValue("StateLimit", StateLimit);
                info.AddValue("StartSetSizeLimit", StartSetSizeLimit);

                ulong A_startset_ulong = (ulong)(object)A_startset;

                var simpl_precomputed = Array.ConvertAll(simpl_bvalg.dtree.precomputed, atomId => simpl_bvalg.IsSatisfiable(simpl_bvalg.MkAnd(simpl_bvalg.atoms[atomId], A_startset_ulong)));
                BooleanDecisionTree simpl_A_StartSet;
                if (simpl_bvalg.IsSatisfiable(simpl_bvalg.MkAnd(simpl_bvalg.atoms[0], A_startset_ulong)))
                    simpl_A_StartSet = new BooleanDecisionTree(simpl_precomputed, new DecisionTree.BST(1, null, null));
                else
                    simpl_A_StartSet = new BooleanDecisionTree(simpl_precomputed, new DecisionTree.BST(0, null, null));
                var simpl_A_StartSet_Size = simpl_bvalg.ComputeDomainSize(A_startset_ulong);

                info.AddValue("A_StartSet", simpl_A_StartSet);
                info.AddValue("A_startset", A_startset_ulong);
                info.AddValue("A_StartSet_Size", simpl_A_StartSet_Size);

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
            this.Options = (RegexOptions)info.GetValue("Options", typeof(RegexOptions));

            var solver = (ICharAlgebra<S>)info.GetValue("solver", typeof(ICharAlgebra<S>));
            this.builder = new SymbolicRegexBuilder<S>(solver);

            this.atoms = builder.solver.GetPartition();
            this.dt = ((BVAlgebraBase)builder.solver).dtree;

            A = builder.Deserialize(info.GetString("A"));
            this.StateLimit = info.GetInt32("StateLimit");
            this.StartSetSizeLimit = info.GetInt32("StartSetSizeLimit");

            InitializeRegexes();

            this.A_startset = A.GetStartSet(builder.solver);
            this.A_StartSet_Size = (int)builder.solver.ComputeDomainSize(A_startset);

            this.A_StartSet = (BooleanDecisionTree)info.GetValue("A_StartSet", typeof(BooleanDecisionTree));
            this.A_startset = (S)info.GetValue("A_startset", typeof(S));
            this.A_StartSet_Size = info.GetInt32("A_StartSet_Size");

            SymbolicRegexNode<S> tmp = A;
            this.A_prefix_array = info.GetValue("A_prefix_array", typeof(S[])) as S[];
            this.A_prefix = StringUtility.DeserializeStringFromCharCodeSequence(info.GetString("A_prefix"));
            this.A_prefixUTF8 = System.Text.UnicodeEncoding.UTF8.GetBytes(this.A_prefix);

            this.A_fixedPrefix_ignoreCase = info.GetBoolean("A_fixedPrefix_ignoreCase");
            this.A1_skipState = DeltaPlus(A_prefix, q0_A1, out tmp);
            this.A1_skipStateRegex = tmp;

            this.Ar_prefix_array = (S[])info.GetValue("Ar_prefix_array", typeof(S[]));
            this.Ar_prefix = StringUtility.DeserializeStringFromCharCodeSequence(info.GetString("Ar_prefix"));
            this.Ar_skipState = DeltaPlus(Ar_prefix, q0_Ar, out tmp);
            this.Ar_skipStateRegex = tmp;

            InitializeVectors();
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
        internal SymbolicRegex(SymbolicRegexNode<S> sr, CharSetSolver css, BDD[] minterms, RegexOptions options, int StateLimit = 1000, int startSetSizeLimit = 128)
        {
            this.Options = options;
            this.StartSetSizeLimit = startSetSizeLimit;
            this.builder = sr.builder;
            this.StateLimit = StateLimit;
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

            A_startset = A.GetStartSet(builder.solver);
            this.A_StartSet_Size = (int)builder.solver.ComputeDomainSize(A_startset);
            this.A_StartSet = BooleanDecisionTree.Create(css, builder.solver.ConvertToCharSet(css, A_startset));

            SymbolicRegexNode<S> tmp = A;
            this.A_prefix_array = A.GetPrefix();
            this.A_prefix = A.GetFixedPrefix(css, out this.A_fixedPrefix_ignoreCase);
            this.A_prefixUTF8 = System.Text.UnicodeEncoding.UTF8.GetBytes(this.A_prefix);

            this.A1_skipState = DeltaPlus(A_prefix, q0_A1, out tmp);
            this.A1_skipStateRegex = tmp;

            this.Ar_prefix_array = Ar.GetPrefix();
            this.Ar_prefix = new string(Array.ConvertAll(this.Ar_prefix_array, x => (char)css.GetMin(builder.solver.ConvertToCharSet(css, x))));
            this.Ar_skipState = DeltaPlus(Ar_prefix, q0_Ar, out tmp);
            this.Ar_skipStateRegex = tmp;

            InitializeVectors();
        }

        private void InitializeRegexes()
        {
            this.A_allLoopsAreLazy = this.A.CheckIfAllLoopsAreLazy();
            this.A_containsLazyLoop = this.A.CheckIfContainsLazyLoop();
            this.Ar = this.A.Reverse();
            this.A1 = this.builder.MkConcat(this.builder.dotStar, this.A);
            this.regex2state[A1] = this.q0_A1;
            this.regex2state[Ar] = this.q0_Ar;
            this.regex2state[A] = this.q0_A;
            this.K = this.atoms.Length;
            this.delta = new int[this.K * this.StateLimit];
            this.state2regex = new SymbolicRegexNode<S>[this.StateLimit];
            if (this.q0_A1 < this.StateLimit)
            {
                this.state2regex[this.q0_A1] = this.A1;
            }
            else
            {
                this.state2regexExtra[this.q0_A1] = this.A1;
                this.deltaExtra[this.q0_A1] = new int[this.K];
            }

            if (this.q0_Ar < this.StateLimit)
            {
                this.state2regex[this.q0_Ar] = this.Ar;
            }
            else
            {
                this.state2regexExtra[this.q0_Ar] = this.Ar;
                this.deltaExtra[this.q0_Ar] = new int[this.K];
            }

            if (this.q0_A < this.StateLimit)
            {
                this.state2regex[this.q0_A] = this.A;
            }
            else
            {
                this.state2regexExtra[this.q0_A] = this.A;
                this.deltaExtra[this.q0_A] = new int[this.K];
            }
        }

        private void InitializeVectors()
        {
#if UNSAFE
            if (A_StartSet_Size <= StartSetSizeLimit)
            {
                char[] startchars = new List<char>(builder.solver.GenerateAllCharacters(A_startset)).ToArray();
                A_StartSet_Vec = Array.ConvertAll(startchars, c => new Vector<ushort>(c));
                A_StartSet_singleton = (ushort)startchars[0];
            }
#endif

            if (this.A_prefix != string.Empty)
            {
                this.A_prefixUTF8_first_byte = new Vector<byte>(this.A_prefixUTF8[0]);
            }
        }

        /// <summary>
        /// Return the state after the given input.
        /// </summary>
        /// <param name="input">given input</param>
        /// <param name="q">given start state</param>
        /// <param name="regex">regex of returned state</param>
        int DeltaPlus(string input, int q, out SymbolicRegexNode<S> regex)
        {
            if (string.IsNullOrEmpty(input))
            {
                regex = (q < StateLimit ? state2regex[q] : state2regexExtra[q]);
                return q;
            }
            else
            {
                char c = input[0];
                q = Delta(c, q, out regex);


                for (int i = 1; i < input.Length; i++)
                {
                    c = input[i];

                    int p = 0;

#if INLINE
                    #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                    #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                    #endregion
                }
                else
                {
                    #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                    #endregion
                }
                    #endregion
#else
                    p = Delta(c, q, out regex);
#endif

                    q = p;
                }
                return q;
            }
        }

        /// <summary>
        /// Compute the target state for source state q and input character c.
        /// All uses of Delta must be inlined for efficiency. 
        /// This is the purpose of the MethodImpl(MethodImplOptions.AggressiveInlining) attribute.
        /// </summary>
        /// <param name="c">input character</param>
        /// <param name="q">state id of source regex</param>
        /// <param name="regex">target regex</param>
        /// <returns>state id of target regex</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int Delta(int c, int q, out SymbolicRegexNode<S> regex)
        {
            int p;
            #region copy&paste region of the definition of Delta being inlined
            int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
            S atom = atoms[atom_id];
            if (q < StateLimit)
            {
                #region use delta
                int offset = (q * K) + atom_id;
                p = delta[offset];
                if (p == 0)
                {
                    CreateNewTransition(q, atom, offset, out p, out regex);
                }
                else
                {
                    regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                }
                #endregion
            }
            else
            {
                #region use deltaExtra
                int[] q_trans = deltaExtra[q];
                p = q_trans[atom_id];
                if (p == 0)
                {
                    CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                }
                else
                {
                    regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                }
                #endregion
            }
            #endregion
            return p;
        }

        /// <summary>
        /// Critical region for threadsafe applications for defining a new transition from q when q is larger that StateLimit
        /// </summary>
        /// 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateNewTransitionExtra(int q, int atom_id, S atom, int[] q_trans, out int p, out SymbolicRegexNode<S> regex)
        {
            lock (this)
            {
                //check if meanwhile q_trans[atom_id] has become defined possibly by another thread
                int p1 = q_trans[atom_id];
                if (p1 != 0)
                {
                    p = p1;
                    if (p1 < StateLimit)
                        regex = state2regex[p1];
                    else
                        regex = state2regexExtra[p1];
                }
                else
                {
                    //p is still undefined
                    var q_regex = state2regexExtra[q];
                    var deriv = q_regex.MkDerivative(atom);
                    if (!regex2state.TryGetValue(deriv, out p))
                    {
                        p = nextStateId++;
                        regex2state[deriv] = p;
                        // we know at this point that p >= MaxNrOfStates
                        state2regexExtra[p] = deriv;
                        deltaExtra[p] = new int[K];
                    }
                    q_trans[atom_id] = p;
                    regex = deriv;
                }
            }
        }

        /// <summary>
        /// Critical region for threadsafe applications for defining a new transition
        /// </summary>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateNewTransition(int q, S atom, int offset, out int p, out SymbolicRegexNode<S> regex)
        {
            lock (this)
            {
                //check if meanwhile delta[offset] has become defined possibly by another thread
                int p1 = delta[offset];
                if (p1 != 0)
                {
                    p = p1;
                    if (p1 < StateLimit)
                        regex = state2regex[p1];
                    else
                        regex = state2regexExtra[p1];
                }
                else
                {
                    var q_regex = state2regex[q];
                    var deriv = q_regex.MkDerivative(atom);
                    if (!regex2state.TryGetValue(deriv, out p))
                    {
                        p = nextStateId++;
                        regex2state[deriv] = p;
                        if (p < StateLimit)
                            state2regex[p] = deriv;
                        else
                            state2regexExtra[p] = deriv;
                        if (p >= StateLimit)
                            deltaExtra[p] = new int[K];
                    }
                    delta[offset] = p;
                    regex = deriv;
                }
            }
        }

        /// <summary>
        /// Generate all matches.
        /// <param name="input">input string</param>
        /// <param name="limit">upper bound on the number of found matches, nonpositive value (default is 0) means no bound</param>
        /// <param name="startat">the position to start search in the input string</param>
        /// <param name="endat">end position in the input, negative value means unspecified and taken to be input.Length-1</param>
        /// </summary>
        public List<Match> Matches(string input, int limit = 0, int startat = 0, int endat = -1)
        {
#if UNSAFE
            if ((Options & RegexOptions.Vectorize) != RegexOptions.None)
            {
                return Matches_(input, limit, startat, endat);
            }
#endif
            return MatchesSafe(input, limit, startat, endat);
        }

        #region safe version of Matches and IsMatch for string input

        /// <summary>
        /// Generate all matches.
        /// <param name="input">input string</param>
        /// <param name="limit">upper bound on the number of found matches, nonpositive value (default is 0) means no bound</param>
        /// <param name="startat">the position to start search in the input string</param>
        /// <param name="endat">end position in the input, negative value means unspecified and taken to be input.Length-1</param>
        /// </summary>
        public List<Match> MatchesSafe(string input, int limit = 0, int startat = 0, int endat = -1)
        {
            if (A.isNullable)
                throw new AutomataException(AutomataExceptionKind.MustNotAcceptEmptyString);
            else if (string.IsNullOrEmpty(input) || startat >= input.Length || startat < 0)
                throw new AutomataException(AutomataExceptionKind.InvalidArgument);

            int k = ((endat < 0 | endat >= input.Length) ? input.Length : endat + 1);

            //stores the accumulated matches
            List<Match> matches = new List<Match>();

            //find the first accepting state
            //initial start position in the input is i = 0
            int i = startat;

            //after a match is found the match_start_boundary becomes 
            //the first position after the last match
            int match_start_boundary = i;

            bool A_has_nonempty_prefix = (!this.A_prefix.Equals(string.Empty));

            bool AisLazy = A_allLoopsAreLazy;
            bool AisSingleSeq = A.IsSequenceOfSingletons;

            while (true)
            {
                int i_q0_A1;
                int watchdog;
                //optimize for the case when A starts with a fixed prefix
                i = (A_has_nonempty_prefix ?
                        FindFinalStatePositionOpt(input, k, i, out i_q0_A1, out watchdog) :
                        FindFinalStatePosition(input, k, i, out i_q0_A1, out watchdog));

                if (i == k)
                {
                    //end of input has been reached without reaching a final state, so no more matches
                    break;
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
                    //If A is lazy then there is no need to maximize length of end-position
                    if (AisLazy)
                    {
                        if (AisSingleSeq)
                            i_start = i - A.sequenceOfSingletons_count + 1;
                        else
                            i_start = FindStartPosition(input, i, i_q0_A1);
                        i_end = i;
                    }
                    else
                    {
                        i_start = FindStartPosition(input, i, i_q0_A1);
                        i_end = FindEndPosition(input, i_start);
                    }
                }

                var newmatch = new Match(i_start, i_end + 1 - i_start);
                matches.Add(newmatch);
                if (limit > 0 && matches.Count == limit)
                    break;

                //continue matching from the position following last match
                i = i_end + 1;
                match_start_boundary = i;
            }

            return matches;
        }

        /// <summary>
        /// It is known here that regex is nullable
        /// </summary>
        /// <param name="regex"></param>
        /// <returns></returns>
        int GetWatchdog(SymbolicRegexNode<S> regex)
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
        /// Returns true iff the input string matches A.
        /// <param name="input">input string</param>
        /// <param name="startat">the position to start search in the input string</param>
        /// <param name="endat">end position in the input, negative value means 
        /// unspecified and taken to be input.Length-1</param>
        /// </summary>
        public bool IsMatch(string input, int startat = 0, int endat = -1)
        {
            if (input == null || startat >= input.Length || startat < 0)
                throw new AutomataException(AutomataExceptionKind.InvalidArgument);

            int k = ((endat < 0 | endat >= input.Length) ? input.Length : endat + 1);

            if (this.A.containsAnchors)
            {
                #region original regex contains anchors
                //TBD prefix optimization may still be important here 
                //but the prefix needs to be computed based on A, but with start anchors removed or treated specially
                if (A2 == null)
                {
                    #region initialize A2 to A.RemoveAnchors()
                    this.A2 = A.ReplaceAnchors();
                    int qA2;
                    if (!regex2state.TryGetValue(this.A2, out qA2))
                    {
                        //the regex does not yet exist
                        qA2 = this.nextStateId++;
                        this.regex2state[this.A2] = qA2;
                    }
                    this.q0_A2 = qA2;
                    if (qA2 >= this.StateLimit)
                    {
                        this.deltaExtra[qA2] = new int[this.K];
                        this.state2regexExtra[qA2] = this.A2;
                    }
                    else
                    {
                        this.state2regex[qA2] = this.A2;
                    }



                    #endregion
                }

                int q = this.q0_A2;
                SymbolicRegexNode<S> regex = this.A2;
                int i = startat;

                while (i < k)
                {
                    int c = input[i];
                    int p;

#if INLINE
                    #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                    #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                    #endregion
                }
                else
                {
                    #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                    #endregion
                }
                    #endregion
#else
                    p = Delta(c, q, out regex);
#endif

                    if (regex == this.builder.dotStar)
                    {
                        //the input is accepted no matter how the input continues
                        return true;
                    }
                    if (regex == this.builder.nothing)
                    {
                        //the input is rejected no matter how the input continues
                        return false;
                    }

                    //continue from the target state
                    q = p;
                    i += 1;
                }
                return regex.IsNullable;
                #endregion
            }
            else
            {
                #region original regex contains no anchors
                int i;
                int i_q0;
                int watchdog;
                if (this.A_prefix != string.Empty)
                {
                    i = FindFinalStatePositionOpt(input, k, startat, out i_q0, out watchdog);
                }
                else
                {
                    i = FindFinalStatePosition(input, k, startat, out i_q0, out watchdog);
                }
                if (i == k)
                {
                    //the search for final state exceeded the input, so final state was not found
                    return false;
                }
                else
                {
                    //since A has no anchors the pattern is really .*A.*
                    //thus if input[0...i] is in L(.*A) then input is in L(.*A.*)
                    return true;
                }
                #endregion
            }
        }

        /// <summary>
        /// Find match end position using A, end position is known to exist.
        /// </summary>
        /// <param name="input">input array</param>
        /// <param name="i">start position</param>
        /// <returns></returns>
        private int FindEndPosition(string input, int i)
        {
            int k = input.Length;
            int i_end = k;
            int q = q0_A;
            while (i < k)
            {
                SymbolicRegexNode<S> regex;
                int c = input[i];
                int p;

                //TBD: anchors

#if INLINE
                #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                else
                {
                #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                #endregion
#else
                p = Delta(c, q, out regex);
#endif


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
            if (i_end == k)
                throw new AutomataException(AutomataExceptionKind.InternalError);
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
            while (i >= match_start_boundary)
            {
                //observe that the input is reversed 
                //so input[k-1] is the first character 
                //and input[0] is the last character
                //TBD: anchors
                c = input[i];

#if INLINE
                #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                else
                {
                #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                #endregion
#else
                p = Delta(c, q, out regex);
#endif

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
        /// Return the position of the last character that leads to a final state in A1
        /// </summary>
        /// <param name="input">given input string</param>
        /// <param name="i">start position</param>
        /// <param name="i_q0">last position the initial state of A1 was visited</param>
        /// <param name="k">input length or bounded input length</param>
        private int FindFinalStatePosition(string input, int k, int i, out int i_q0, out int watchdog)
        {
            int q = q0_A1;
            int i_q0_A1 = i;

            //TBD: anchors
            SymbolicRegexNode<S> regex = null;

            while (i < k)
            {
                if (q == q0_A1)
                {
                    i = IndexOfStartset(input, i);

                    if (i == -1)
                    {
                        i_q0 = i_q0_A1;
                        watchdog = -1;
                        return k;
                    }
                    i_q0_A1 = i;
                }

                int c = input[i];
                int p;

#if INLINE
                #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                else
                {
                #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                #endregion
#else
                p = Delta(c, q, out regex);
#endif

                if (regex.isNullable)
                {
                    //p is a final state so match has been found
                    break;
                }
                else if (regex == this.builder.nothing)
                {
                    //p is a deadend state so any further search is meaningless
                    i_q0 = i_q0_A1;
                    watchdog = -1;
                    return k;
                }

                //continue from the target state
                q = p;
                i += 1;
            }
            i_q0 = i_q0_A1;
            watchdog = (regex == null ? -1  : this.GetWatchdog(regex));
            return i;
        }

        /// <summary>
        /// FindFinalState optimized for the case when A starts with a fixed prefix
        /// </summary>
        /// <param name="input">given input string</param>
        /// <param name="i">start position</param>
        /// <param name="i_q0">last position the initial state of A1 was visited</param>
        /// <param name="k">input length or bounded input length</param>
        private int FindFinalStatePositionOpt(string input, int k, int i, out int i_q0, out int watchdog)
        {
            int q = q0_A1;
            int i_q0_A1 = i;
            var prefix = this.A_prefix;
            //it is important to use Ordinal/OrdinalIgnoreCase to avoid culture dependent semantics of IndexOf
            StringComparison comparison = (this.A_fixedPrefix_ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            SymbolicRegexNode<S> regex = null;
            while (i < k)
            {
                // ++++ the following prefix optimization can be commented out without affecting correctness ++++
                // but this optimization has a huge perfomance boost when fixed prefix exists .... in the order of 10x
                //
                #region prefix optimization 
                //stay in the initial state if the prefix does not match
                //thus advance the current position to the 
                //first position where the prefix does match
                if (q == q0_A1)
                {
                    i_q0_A1 = i;

                    //i = IndexOf(input, prefix, i, this.A_fixedPrefix_ignoreCase);
                    i = input.IndexOf(prefix, i, comparison);

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
                        i = i + prefix.Length;
                        if (regex.isNullable)
                        {
                            i_q0 = i_q0_A1;
                            watchdog = GetWatchdog(regex);
                            //return the last position of the match
                            return i - 1;
                        }
                        if (i == k)
                        {
                            i_q0 = i_q0_A1;
                            watchdog = -1;
                            return k;
                        }
                    }
                }
                #endregion

                //TBD: anchors
                int c = input[i];
                int p;

                #region if original regex contains anchors and c is \n then insert startline and endline characters
                if (A.containsAnchors && c == '\n')
                {
                    //TBD: anchors
                }
                #endregion

#if INLINE
                #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                else
                {
                #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                #endregion
#else
                p = Delta(c, q, out regex);
#endif

                if (regex.isNullable)
                {
                    //p is a final state so match has been found
                    break;
                }
                else if (regex == this.builder.nothing)
                {
                    i_q0 = i_q0_A1;
                    //p is a deadend state so any further search is meaningless
                    watchdog = -1;
                    return k;
                }

                //continue from the target state
                q = p;
                i += 1;
            }
            i_q0 = i_q0_A1;
            watchdog = (regex == null ? -1 : this.GetWatchdog(regex));
            return i;
        }

        #endregion

#if UNSAFE

        #region unsafe version of Matches for string input

        /// <summary>
        /// Generate all earliest maximal matches. We know that k is at least 2. Unsafe version of Matches.
        /// <param name="input">pointer to input string</param>
        /// <param name="limit">upper bound on the number of found matches, nonpositive value (default is 0) means no bound</param>
        /// </summary>
        unsafe public List<Match> Matches_(string input, int limit = 0, int startat = 0, int endat = -1)
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

                        int i_start = FindStartPosition_(inputp, i, i_q0_A1);

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

                    int i_start = FindStartPosition_(inputp, i, i_q0_A1);

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

#if INLINE
                #region copy&paste region of the definition of Delta being inlined
                    int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                    S atom = atoms[atom_id];
                    if (q < StateLimit)
                    {
                #region use delta
                        int offset = (q * K) + atom_id;
                        p = delta[offset];
                        if (p == 0)
                        {
                            CreateNewTransition(q, atom, offset, out p, out regex);
                        }
                        else
                        {
                            regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                        }
                #endregion
                    }
                    else
                    {
                #region use deltaExtra
                        int[] q_trans = deltaExtra[q];
                        p = q_trans[atom_id];
                        if (p == 0)
                        {
                            CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                        }
                        else
                        {
                            regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                        }
                #endregion
                    }
                #endregion
#else
                p = Delta(c, q, out regex);
#endif

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

                    //TBD: anchors
                    int c = inputp[i];
                    int p;

#if INLINE
                    #region copy&paste region of the definition of Delta being inlined
                    int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                    S atom = atoms[atom_id];
                    if (q < StateLimit)
                    {
                    #region use delta
                        int offset = (q * K) + atom_id;
                        p = delta[offset];
                        if (p == 0)
                        {
                            CreateNewTransition(q, atom, offset, out p, out regex);
                        }
                        else
                        {
                            regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                        }
                    #endregion
                    }
                    else
                    {
                    #region use deltaExtra
                        int[] q_trans = deltaExtra[q];
                        p = q_trans[atom_id];
                        if (p == 0)
                        {
                            CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                        }
                        else
                        {
                            regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                        }
                    #endregion
                    }
                    #endregion
#else
                    p = Delta(c, q, out regex);
#endif

                    if (regex.isNullable)
                    {
                        //p is a final state so match has been found
                        break;
                    }
                    else if (regex == this.builder.nothing)
                    {
                        i_q0 = i_q0_A1;
                        //p is a deadend state so any further search is meaningless
                        return k;
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
        unsafe private int FindStartPosition_(char* input, int i, int match_start_boundary)
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
            while (i >= match_start_boundary)
            {
                //observe that the input is reversed 
                //so input[k-1] is the first character 
                //and input[0] is the last character
                //TBD: anchors
                c = input[i];

#if INLINE
                #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                else
                {
                #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                #endregion
#else
                p = Delta(c, q, out regex);
#endif

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
            while (i < k)
            {
                SymbolicRegexNode<S> regex;
                int c = input[i];
                int p;

                //TBD: anchors

#if INLINE
                #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                else
                {
                #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                #endregion
#else
                p = Delta(c, q, out regex);
#endif


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
        int IndexOfStartset(string input, int i)
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
        /// <param name="input">input string to search in</param>
        /// <param name="i">the start index in input to search from</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int IndexOfStartsetUTF8(byte[] input, int i, ref int surrogate_codepoint)
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
        int IndexOf(byte[] input, byte[] value, int i)
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
        int IndexOf(byte[] input, bool[] pred, int i)
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

        #region Matches that uses UTF-8 encoded byte array as input

        /// <summary>
        /// Generate all earliest maximal matches.
        /// <paramref name="input">pointer to input string</paramref>
        /// </summary>
        internal List<Match> MatchesUTF8(byte[] input)
        {
            int k = input.Length;

            //stores the accumulated matches
            List<Match> matches = new List<Match>();

            //find the first accepting state
            //initial start position in the input is i = 0
            int i = 0;

            //after a match is found the match_start_boundary becomes 
            //the first postion after the last match
            //enforced when inlcude_overlaps == false
            int match_start_boundary = 0;

            int surrogate_codepoint = 0;

            //TBD: dont enforce match_start_boundary when match overlaps are allowed
            bool A_has_nonempty_prefix = (this.A_prefix != string.Empty);
            while (true)
            {
                int i_q0_A1;
                //TBD: optimize for the case when A starts with a fixed prefix
                i = FindFinalStatePositionUTF8(input, i, ref surrogate_codepoint, out i_q0_A1);

                if (i == k)
                {
                    //end of input has been reached without reaching a final state, so no more matches
                    break;
                }

                int i_start = FindStartPositionUTF8(input, i, ref surrogate_codepoint, i_q0_A1);

                int i_end = FindEndPositionUTF8(input, i_start, ref surrogate_codepoint);

                var newmatch = new Match(i_start, i_end + 1 - i_start);
                matches.Add(newmatch);

                //continue matching from the position following last match
                i = i_end + 1;
                match_start_boundary = i;
            }

            return matches;
        }

        /// <summary>
        /// Find match end position using A, end position is known to exist.
        /// </summary>
        /// <param name="input">input array</param>
        /// <param name="i">start position</param>
        /// <param name="surrogate_codepoint">surrogate codepoint</param>
        /// <returns></returns>
        private int FindEndPositionUTF8(byte[] input, int i, ref int surrogate_codepoint)
        {
            int k = input.Length;
            int i_end = k;
            int q = q0_A;
            int step = 0;
            int codepoint = 0;
            while (i < k)
            {
                SymbolicRegexNode<S> regex;

                ushort c;
                #region c = current UTF16 character
                if (surrogate_codepoint == 0)
                {
                    c = input[i];
                    if (c > 0x7F)
                    {
                        int x;
                        UTF8Encoding.DecodeNextNonASCII(input, i, out x, out codepoint);
                        if (codepoint > 0xFFFF)
                        {
                            surrogate_codepoint = codepoint;
                            c = UTF8Encoding.HighSurrogate(codepoint);
                            //do not increment i yet because L is pending
                            step = 0;
                        }
                        else
                        {
                            c = (ushort)codepoint;
                            //step is either 2 or 3, i.e. either 2 or 3 UTF-8-byte encoding
                            step = x;
                        }
                    }
                }
                else
                {
                    c = UTF8Encoding.LowSurrogate(surrogate_codepoint);
                    //reset the surrogate_codepoint
                    surrogate_codepoint = 0;
                    //increment i by 4 since low surrogate has now been read
                    step = 4;
                }
                #endregion

                int p;

                //TBD: anchors

#if INLINE
                #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                else
                {
                #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                #endregion
#else
                p = Delta(c, q, out regex);
#endif


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
                if (c > 0x7F)
                    i += step;
                else
                    i += 1;
            }
            if (i_end == k)
                throw new AutomataException(AutomataExceptionKind.InternalError);
            return i_end;
        }

        /// <summary>
        /// Walk back in reverse using Ar to find the start position of match, start position is known to exist.
        /// </summary>
        /// <param name="input">the input array</param>
        /// <param name="i">position to start walking back from, i points at the last character of the match</param>
        /// <param name="match_start_boundary">do not pass this boundary when walking back</param>
        /// <param name="surrogate_codepoint">surrogate codepoint</param>
        /// <returns></returns>
        private int FindStartPositionUTF8(byte[] input, int i, ref int surrogate_codepoint, int match_start_boundary)
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
            ushort c;
            int codepoint;
            while (i >= match_start_boundary)
            {
                //observe that the input is reversed 
                //so input[k-1] is the first character 
                //and input[0] is the last character
                //but encoding is not reversed
                //TBD: anchors

                #region c = current UTF16 character
                if (surrogate_codepoint == 0)
                {
                    //not in the middel of surrogate codepoint 
                    c = input[i];
                    if (c > 0x7F)
                    {
                        int _;
                        UTF8Encoding.DecodeNextNonASCII(input, i, out _, out codepoint);
                        if (codepoint > 0xFFFF)
                        {
                            //given codepoint = ((H - 0xD800) * 0x400) + (L - 0xDC00) + 0x10000
                            surrogate_codepoint = codepoint;
                            //compute c = L (going backwards) 
                            c = (ushort)(((surrogate_codepoint - 0x10000) & 0x3FF) | 0xDC00);
                        }
                        else
                        {
                            c = (ushort)codepoint;
                        }
                    }
                }
                else
                {
                    //given surrogate_codepoint = ((H - 0xD800) * 0x400) + (L - 0xDC00) + 0x10000
                    //compute c = H (going backwards)
                    c = (ushort)(((surrogate_codepoint - 0x10000) >> 10) | 0xD800);
                    //reset the surrogate codepoint 
                    surrogate_codepoint = 0;
                }
                #endregion

#if INLINE
                #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                else
                {
                #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                #endregion
#else
                p = Delta(c, q, out regex);
#endif

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
                    surrogate_codepoint = 0;
                    break;
                }
                if (surrogate_codepoint == 0)
                {
                    i = i - 1;
                    //step back to the previous input, /while input[i] is not a start-byte take a step back
                    //check (0x7F < b && b < 0xC0) imples that 0111.1111 < b < 1100.0000
                    //so b cannot be ascii 0xxx.xxxx or startbyte 110x.xxxx or 1110.xxxx or 1111.0xxx
                    while ((i >= match_start_boundary) && (0x7F < input[i] && input[i] < 0xC0))
                        i = i - 1;
                }
                q = p;
            }
            if (last_start == -1)
                throw new AutomataException(AutomataExceptionKind.InternalError);
            return last_start;
        }

        /// <summary>
        /// Return the position of the last character that leads to a final state in A1
        /// </summary>
        /// <param name="input">given input array</param>
        /// <param name="i">start position</param>
        /// <param name="i_q0">last position the initial state of A1 was visited</param>
        /// <param name="surrogate_codepoint">surrogate codepoint</param>
        /// <returns></returns>
        private int FindFinalStatePositionUTF8(byte[] input, int i, ref int surrogate_codepoint, out int i_q0)
        {
            int k = input.Length;
            int q = q0_A1;
            int i_q0_A1 = i;
            int step = 0;
            int codepoint;
            SymbolicRegexNode<S> regex;
            bool prefix_optimize = (!this.A_fixedPrefix_ignoreCase) && this.A_prefixUTF8.Length > 1;
            while (i < k)
            {
                if (q == q0_A1)
                {
                    if (prefix_optimize)
                    {
                        #region prefix optimization when A has a fixed prefix and is case-sensitive
                        //stay in the initial state if the prefix does not match
                        //thus advance the current position to the 
                        //first position where the prefix does match
                        i_q0_A1 = i;

                        i = VectorizedIndexOf.IndexOfByteSeq(input, i, this.A_prefixUTF8, this.A_prefixUTF8_first_byte);

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
                            // --- i.e. do the loop ---
                            //for (int j = 0; j < prefix.Length; j++)
                            //    q = Delta(prefix[j], q, out regex);
                            // ---
                            q = this.A1_skipState;
                            regex = this.A1_skipStateRegex;

                            //skip the prefix
                            i = i + this.A_prefixUTF8.Length;
                            if (regex.isNullable)
                            {
                                i_q0 = i_q0_A1;
                                //return the last position of the match
                                //make sure to step back to the start byte
                                i = i - 1;
                                //while input[i] is not a start-byte take a step back
                                while (0x7F < input[i] && input[i] < 0xC0)
                                    i = i - 1;
                            }
                            if (i == k)
                            {
                                i_q0 = i_q0_A1;
                                return k;
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        i = (this.A_prefixUTF8.Length == 0 ?
                            IndexOfStartsetUTF8(input, i, ref surrogate_codepoint) :
                            VectorizedIndexOf.IndexOfByte(input, i, this.A_prefixUTF8[0], this.A_prefixUTF8_first_byte));

                        if (i == -1)
                        {
                            i_q0 = i_q0_A1;
                            return k;
                        }
                        i_q0_A1 = i;
                    }
                }

                ushort c;

                #region c = current UTF16 character
                if (surrogate_codepoint == 0)
                {
                    c = input[i];
                    if (c > 0x7F)
                    {
                        int x;
                        UTF8Encoding.DecodeNextNonASCII(input, i, out x, out codepoint);
                        if (codepoint > 0xFFFF)
                        {
                            //given codepoint = ((H - 0xD800) * 0x400) + (L - 0xDC00) + 0x10000
                            surrogate_codepoint = codepoint;
                            //compute c = H 
                            c = (ushort)(((codepoint - 0x10000) >> 10) | 0xD800);
                            //do not increment i yet because L is pending
                            step = 0;
                        }
                        else
                        {
                            c = (ushort)codepoint;
                            //step is either 2 or 3, i.e. either 2 or 3 UTF-8-byte encoding
                            step = x;
                        }
                    }
                }
                else
                {
                    //given surrogate_codepoint = ((H - 0xD800) * 0x400) + (L - 0xDC00) + 0x10000
                    //compute c = L 
                    c = (ushort)(((surrogate_codepoint - 0x10000) & 0x3FF) | 0xDC00);
                    //reset the surrogate_codepoint
                    surrogate_codepoint = 0;
                    //increment i by 4 since low surrogate has now been read
                    step = 4;
                }
                #endregion


                //TBD: anchors
                int p;

#if INLINE
                #region copy&paste region of the definition of Delta being inlined
                int atom_id = (dt.precomputed.Length > c ? dt.precomputed[c] : dt.bst.Find(c));
                S atom = atoms[atom_id];
                if (q < StateLimit)
                {
                #region use delta
                    int offset = (q * K) + atom_id;
                    p = delta[offset];
                    if (p == 0)
                    {
                        CreateNewTransition(q, atom, offset, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                else
                {
                #region use deltaExtra
                    int[] q_trans = deltaExtra[q];
                    p = q_trans[atom_id];
                    if (p == 0)
                    {
                        CreateNewTransitionExtra(q, atom_id, atom, q_trans, out p, out regex);
                    }
                    else
                    {
                        regex = (p < StateLimit ? state2regex[p] : state2regexExtra[p]);
                    }
                #endregion
                }
                #endregion
#else
                p = Delta(c, q, out regex);
#endif

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

                //continue from the target state
                q = p;
                if (c > 0x7F)
                    i += step;
                else
                    i += 1;
            }
            i_q0 = i_q0_A1;
            return i;
        }
        #endregion
    }
}
