using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.Serialization;
using System.IO;

namespace Microsoft.SRM
{
    /// <summary>
    /// Provides IsMatch and Matches methods.
    /// </summary>
    internal interface IMatcher
    {
        /// <summary>
        /// Returns true iff the input string matches. 
        /// <param name="input">given iput string</param>
        /// <param name="startat">start position in the input, default is 0</param>
        /// <param name="endat">end position in the input, -1 means that the value is unspecified and taken to be input.Length-1</param>
        /// </summary>
        bool IsMatch(string input, int startat = 0, int endat = -1);

        /// <summary>
        /// Returns all matches as pairs (startindex, length) in the input string.
        /// </summary>
        /// <param name="input">given iput string</param>
        /// <param name="limit">as soon as this many matches have been found the search terminates, 0 or negative value means that there is no bound, default is 0</param>
        /// <param name="startat">start position in the input, default is 0</param>
        /// <param name="endat">end position in the input, -1 means that the value is unspecified and taken to be input.Length-1</param>
        List<Match> Matches(string input, int limit = 0, int startat = 0, int endat = -1);
    }
}
