using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SRM
{
    internal class IgnoreCaseTransformer
    {
        BDD IgnoreCaseRel;
        BDD domain;
        CharSetSolver solver;

        public IgnoreCaseTransformer(CharSetSolver charSetSolver)
        {
            this.solver = charSetSolver;
            IgnoreCaseRel = charSetSolver.Deserialize(Microsoft.SRM.Generated.IgnoreCaseRelation.ignorecase);
            domain = IgnoreCaseRel >> 16;
        }

        /// <summary>
        /// For all letters in the bdd add their lower and upper case equivalents.
        /// </summary>
        public BDD Apply(BDD bdd)
        {
            if ((domain & bdd).IsEmpty)
                return bdd;
            else
            {
                var ignorecase = (bdd & IgnoreCaseRel) >> 16;
                var res = ignorecase | bdd;
                return res;
            }
        }

        public bool IsInDomain(char c)
        {
            BDD c_bdd = solver.MkCharConstraint(c);
            if ((c_bdd & domain).IsEmpty)
                return false;
            else
                return true;
        }
    }
}
