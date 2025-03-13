// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace TbdParsing.Models
{
    /// <summary>
    /// Exception thrown when parsing a TBD file fails
    /// </summary>
    public class ParsingException : Exception
    {
        /// <summary>
        /// Creates a new instance of ParsingException
        /// </summary>
        /// <param name="message">The error message</param>
        public ParsingException(string message) : base(message) { }

        /// <summary>
        /// Creates a new instance of ParsingException with an inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        public ParsingException(string message, Exception innerException) : base(message, innerException) { }
    }
}
