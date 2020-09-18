using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Microsoft.SRM
{
    [Serializable]
    internal class Regex
    {
        private static readonly CharSetSolver solver;
        private static readonly RegexToAutomatonConverter<BDD> converter;
        static Regex()
        {
            solver = new CharSetSolver();
            converter = new RegexToAutomatonConverter<BDD>(solver);
        }

        private IMatcher matcher;

        public Regex(string pattern) : this(pattern, RegexOptions.None) { }

        public Regex(string pattern, RegexOptions options)
        {
            var root = converter.ConvertToSymbolicRegex(pattern, options, keepAnchors: true);
            var partition = root.ComputeMinterms();
            if (partition.Length > 64)
            {
                //more than 64 bits needed to represent a set
                matcher = new SymbolicRegexBV(root, solver, converter.srBuilder, partition, options);
            }
            else
            {
                //enough to use 64 bits
                matcher = new SymbolicRegexUInt64(root, solver, converter.srBuilder, partition, options);
            }
        }

        /// <summary>
        /// Returns true iff the input string matches. 
        /// <param name="input">given iput string</param>
        /// <param name="startat">start position in the input</param>
        /// <param name="endat">end position in the input, -1 means that the value is unspecified and taken to be input.Length-1</param>
        /// </summary>
        public bool IsMatch(string input, int startat = 0, int endat = -1)
            => matcher.IsMatch(input, startat, endat);

        /// <summary>
        /// Returns all matches as pairs (startindex, length) in the input string.
        /// </summary>
        /// <param name="input">given iput string</param>
        /// <param name="limit">as soon as this many matches have been found the search terminates, 0 or negative value means that there is no bound, default is 0</param>
        /// <param name="startat">start position in the input, default is 0</param>
        /// <param name="endat">end position in the input, -1 means that the value is unspecified and taken to be input.Length-1</param>
        public List<Match> Matches(string input, int limit = 0, int startat = 0, int endat = -1)
            => matcher.Matches(input, limit, startat, endat);
        
        /// <summary>
        /// Serialize this symbolic regex matcher to the given file.
        /// If formatter is null then an instance of 
        /// System.Runtime.Serialization.Formatters.Binary.BinaryFormatter is used.
        /// </summary>
        /// <param name="file">file where the serialization is stored</param>
        /// <param name="formatter">given formatter</param>
        public void Serialize(string file, IFormatter formatter = null)
        {
            var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
            Serialize(stream, formatter);
            stream.Close();
        }

        /// <summary>
        /// Serialize this symbolic regex matcher to the given file.
        /// If formatter is null then an instance of 
        /// System.Runtime.Serialization.Formatters.Binary.BinaryFormatter is used.
        /// </summary>
        /// <param name="stream">stream where the serialization is stored</param>
        /// <param name="formatter">given formatter</param>
        public void Serialize(Stream stream, IFormatter formatter = null)
        {
            if (formatter == null)
                formatter = new BinaryFormatter();
            formatter.Serialize(stream, this);
        }

        /// <summary>
        /// Deserialize the matcher of a symblic regex from the given file using the given formatter. 
        /// If formatter is null then an instance of 
        /// System.Runtime.Serialization.Formatters.Binary.BinaryFormatter is used.
        /// </summary>
        /// <param name="file">source file of the serialized matcher</param>
        /// <param name="formatter">given formatter</param>
        /// <returns></returns>
        public static Regex Deserialize(string file, IFormatter formatter = null)
        {
            Stream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            Regex matcher = Deserialize(stream, formatter);
            stream.Close();
            return matcher;
        }

        /// <summary>
        /// Deserialize the matcher of a symblic regex from the given stream using the given formatter. 
        /// If formatter is null then an instance of 
        /// System.Runtime.Serialization.Formatters.Binary.BinaryFormatter is used.
        /// </summary>
        /// <param name="stream">source stream of the serialized matcher</param>
        /// <param name="formatter">given formatter</param>
        /// <returns></returns>
        public static Regex Deserialize(Stream stream, IFormatter formatter = null)
        {
            if (formatter == null)
                formatter = new BinaryFormatter();
            Regex matcher = (Regex)formatter.Deserialize(stream);
            return matcher;
        }
    }
}
