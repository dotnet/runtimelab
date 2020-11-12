// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

using Internal.TypeSystem;

namespace ILCompiler
{
    // Poor man's logger. We can do better than this.

    public class Logger
    {
        public static Logger Null = new Logger(TextWriter.Null, false);

        public TextWriter Writer { get; }

        public bool IsVerbose { get; }

        public Logger(TextWriter writer, bool isVerbose)
        {
            Writer = TextWriter.Synchronized(writer);
            IsVerbose = isVerbose;
        }

        public void LogWarning(string text, int code, TypeSystemEntity origin, int? ilOffset = null, string subcategory = MessageSubCategory.None)
        {
            // Temporary implementation. We'll want to mirror warning infrastructure in the IL linker.
            if (IsVerbose)
            {
                // Assume the suppression is related for now to reduce noise.
                bool suppressed = origin is MethodDesc method && method.HasCustomAttribute("System.Diagnostics.CodeAnalysis", "UnconditionalSuppressMessageAttribute");
                if (!suppressed)
                {
                    Writer.WriteLine($"warning IL{code:D4}: {origin}: {text}");
                }
            }
                
        }
    }

    public static class MessageSubCategory
    {
        public const string None = "";
        public const string TrimAnalysis = "Trim analysis";
    }
}
