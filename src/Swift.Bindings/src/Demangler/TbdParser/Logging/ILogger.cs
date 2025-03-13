// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace TbdParsing.Logging
{
    /// <summary>
    /// Defines logging levels for the TBD parser
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        None = 99
    }

    /// <summary>
    /// Interface for logging messages during TBD parsing
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// The minimum log level that will be output
        /// </summary>
        LogLevel MinimumLevel { get; set; }

        /// <summary>
        /// Log a debug message
        /// </summary>
        void Debug(string message);

        /// <summary>
        /// Log an informational message
        /// </summary>
        void Info(string message);

        /// <summary>
        /// Log a warning message
        /// </summary>
        void Warning(string message);

        /// <summary>
        /// Log an error message
        /// </summary>
        void Error(string message);

        /// <summary>
        /// Log an error message with exception details
        /// </summary>
        void Error(string message, Exception ex);
    }
}
