// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Generic Boolean Algebra solver.
    /// Provides operations for conjunction, disjunction, and negation.
    /// Allows to decide if a predicate is satisfiable and if two predicates are equivalent.
    /// </summary>
    /// <typeparam name="S">predicates</typeparam>
    internal interface IBooleanAlgebra<S>
    {
        /// <summary>
        /// Top element of the Boolean algebra, corresponds to the value true.
        /// </summary>
        S True { get; }

        /// <summary>
        /// Bottom element of the Boolean algebra, corresponds to the value false.
        /// </summary>
        S False { get; }

        /// <summary>
        /// Make a conjunction of predicate1 and predicate2.
        /// </summary>
        S MkAnd(S predicate1, S predicate2);

        /// <summary>
        /// Make a conjunction of all the predicates in the enumeration.
        /// Returns True if the enumeration is empty.
        /// </summary>
        S MkAnd(IEnumerable<S> predicates);

        /// <summary>
        /// Make a conjunction of all the predicates.
        /// Returns True if the enumeration is empty.
        /// </summary>
        S MkAnd(params S[] predicates);

        /// <summary>
        /// Make a disjunction of predicate1 and predicate2.
        /// </summary>
        S MkOr(S predicate1, S predicate2);

        /// <summary>
        /// Make a disjunction of all the predicates in the enumeration.
        /// Must return False if the enumeration is empty.
        /// </summary>
        S MkOr(IEnumerable<S> predicates);

        /// <summary>
        /// Negate the predicate.
        /// </summary>
        S MkNot(S predicate);

        /// <summary>
        /// Returns true iff the predicate is satisfiable.
        /// </summary>
        bool IsSatisfiable(S predicate);

        /// <summary>
        /// Returns true iff predicate1 is equivalent to predicate2.
        /// </summary>
        bool AreEquivalent(S predicate1, S predicate2);

        /// <summary>
        /// True means then if two predicates are equivalent then their hashcodes are equal.
        /// This is a weak form of extensionality.
        /// </summary>
        bool HashCodesRespectEquivalence { get; }

        /// <summary>
        /// True means that if two predicates are equivalent then they are identical.
        /// </summary>
        bool IsExtensional { get; }

        /// <summary>
        /// Given an array of constraints {c_1, c_2, ..., c_n} where n&gt;=0.
        /// Enumerate all satisfiable Boolean combinations Tuple({b_1, b_2, ..., b_n}, c)
        /// where c is satisfisable and equivalent to c'_1 &amp; c'_2 &amp; ... &amp; c'_n,
        /// where c'_i = c_i if b_i = true and c'_i is Not(c_i) otherwise.
        /// If n=0 return Tuple({},True)
        /// </summary>
        /// <param name="constraints">array of constraints</param>
        /// <returns>Booolean combinations that are satisfiable</returns>
        IEnumerable<Tuple<bool[], S>> GenerateMinterms(params S[] constraints);

        /// <summary>
        /// Serialize the predicate as a nonempty string only using characters in the Base64 subset [0-9a-zA-Z/+.]
        /// </summary>
        /// <param name="s">given predicate</param>
        string SerializePredicate(S s);

        /// <summary>
        /// Deserialize the predicate from a string constructed with Serialize
        /// </summary>
        /// <param name="s">given serialized predicate</param>
        S DeserializePredicate(string s);

        void Serialize(StringBuilder sb);
    }
}
