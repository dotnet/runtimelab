// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Text.RegularExpressions.SRM.Unicode
{
    internal class IgnoreCaseTransformer
    {
        private BDD IgnoreCaseRel;
        private BDD domain;
        private CharSetSolver solver;

        public IgnoreCaseTransformer(CharSetSolver charSetSolver)
        {
            this.solver = charSetSolver;
            //IgnoreCaseRel = charSetSolver.Deserialize(System.Text.RegularExpressions.SRM.Unicode.IgnoreCaseRelation.ignorecase);
            string str = System.Text.RegularExpressions.SRM.Unicode.IgnoreCaseRelation.s_IgnoreCaseBDD_repr;
            IgnoreCaseRel = BDD.Deserialize(str, charSetSolver);
            var str2 = IgnoreCaseRel.SerializeToString();
            domain = solver.ShiftRight(IgnoreCaseRel, 16);
        }

        /// <summary>
        /// For all letters in the bdd add their lower and upper case equivalents.
        /// </summary>
        public BDD Apply(BDD bdd)
        {
            if (solver.MkAnd(domain, bdd).IsEmpty)
                return bdd;
            else
            {
                var ignorecase = solver.ShiftRight(solver.MkAnd(bdd, IgnoreCaseRel), 16);
                var res = solver.MkOr(ignorecase, bdd);
                return res;
            }
        }

        public bool IsInDomain(char c)
        {
            BDD c_bdd = solver.MkCharConstraint(c);
            if (solver.MkAnd(c_bdd, domain).IsEmpty)
                return false;
            else
                return true;
        }
    }
}
