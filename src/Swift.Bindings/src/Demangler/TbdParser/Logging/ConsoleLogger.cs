// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace TbdParsing.Logging
{
    /// <summary>
    /// Simple console logger implementation
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        public void Debug(string message)
        {
            if (MinimumLevel <= LogLevel.Debug)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"[DEBUG] {message}");
                Console.ResetColor();
            }
        }

        public void Info(string message)
        {
            if (MinimumLevel <= LogLevel.Info)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"[INFO] {message}");
                Console.ResetColor();
            }
        }

        public void Warning(string message)
        {
            if (MinimumLevel <= LogLevel.Warning)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARN] {message}");
                Console.ResetColor();
            }
        }

        public void Error(string message)
        {
            if (MinimumLevel <= LogLevel.Error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {message}");
                Console.ResetColor();
            }
        }

        public void Error(string message, Exception ex)
        {
            if (MinimumLevel <= LogLevel.Error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {message}");
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }
        }
    }
}
