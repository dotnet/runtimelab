// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace TbdParsing.Logging
{
    /// <summary>
    /// A logger implementation that does nothing (null object pattern)
    /// </summary>
    public class NullLogger : ILogger
    {
        public static NullLogger Instance { get; } = new NullLogger();

        public LogLevel MinimumLevel { get; set; } = LogLevel.None;

        public void Debug(string message) { }

        public void Info(string message) { }

        public void Warning(string message) { }

        public void Error(string message) { }

        public void Error(string message, Exception ex) { }
    }
}
