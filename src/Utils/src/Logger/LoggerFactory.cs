// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Utils.Logging
{
    /// <summary>
    /// Factory for creating logger instances with appropriate verbosity levels.
    /// </summary>
    public static class LoggerFactory
    {
        /// <summary>
        /// Creates a logger with the specified verbosity level.
        /// </summary>
        /// <param name="verbosityLevel">
        /// Numeric verbosity level where:
        /// 0 = No logging
        /// 1 = Errors only
        /// 2 = Warnings and errors
        /// 3 = Info, warnings, and errors (default)
        /// 4 = Debug, info, warnings, and errors
        /// </param>
        /// <returns>A configured ILogger instance</returns>
        public static ILogger Create(int verbosityLevel = 3)
        {
            if (verbosityLevel == 0)
            {
                return new NullLogger();
            }

            // Convert numeric level to LogLevel enum
            LogLevel logLevel = verbosityLevel switch
            {
                1 => LogLevel.Error,
                2 => LogLevel.Warning,
                3 => LogLevel.Info,
                _ => LogLevel.Debug // 4 or higher
            };

            return new ConsoleLogger { MinimumLevel = logLevel };
        }

        /// <summary>
        /// Creates a logger with the specified minimum log level.
        /// </summary>
        /// <param name="level">The minimum log level to report</param>
        /// <returns>A configured ILogger instance</returns>
        public static ILogger Create(LogLevel level)
        {
            return new ConsoleLogger { MinimumLevel = level };
        }

        /// <summary>
        /// Creates a null logger that discards all log messages.
        /// </summary>
        /// <returns>A NullLogger instance</returns>
        public static ILogger CreateNull()
        {
            return new NullLogger();
        }
    }
}
