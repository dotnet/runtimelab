// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Extends ICharAlgebra with character predicate solving and predicate pretty printing.
    /// </summary>
    /// <typeparam name="PRED">predicates</typeparam>
    internal interface ICharAlgebra<PRED> : IBooleanAlgebra<PRED>
    {
        /// <summary>
        /// Make a constraint describing the set of all characters between a (inclusive) and b (inclusive).
        /// Add both uppercase and lowercase elelements if caseInsensitive is true.
        /// </summary>
        PRED MkRangeConstraint(char lower, char upper, bool caseInsensitive = false);

        /// <summary>
        /// Make a constraint describing a singleton set containing the character c, or
        /// a set containing also the upper and lowercase versions of c if caseInsensitive is true.
        /// </summary>
        /// <param name="caseInsensitive">if true include both the uppercase and the lowercase versions of the given character</param>
        /// <param name="c">the given character</param>
        PRED MkCharConstraint(char c, bool caseInsensitive = false);

        /// <summary>
        /// Make a term that encodes the given character set.
        /// </summary>
        PRED ConvertFromCharSet(BDDAlgebra bddAlg, BDD set);

        /// <summary>
        /// Compute the number of elements in the set
        /// </summary>
        ulong ComputeDomainSize(PRED set);

        /// <summary>
        /// Enumerate all characters in the set
        /// </summary>
        /// <param name="set">given set</param>
        IEnumerable<char> GenerateAllCharacters(PRED set);

        /// <summary>
        /// Convert a predicate into a set of characters.
        /// </summary>
        BDD ConvertToCharSet(ICharAlgebra<BDD> bddalg, PRED pred);

        /// <summary>
        /// Gets the underlying character set solver.
        /// </summary>
        CharSetSolver CharSetProvider { get; }

        /// <summary>
        /// Returns a partition of the full domain.
        /// </summary>
        PRED[] GetPartition();

        /// <summary>
        /// Pretty print the character predicate
        /// </summary>
        string PrettyPrint(PRED pred);
    }
}
