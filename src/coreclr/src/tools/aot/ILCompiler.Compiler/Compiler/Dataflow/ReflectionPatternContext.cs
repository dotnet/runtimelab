// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Internal.TypeSystem;

namespace ILCompiler.Dataflow
{
    /// <summary>
    /// Helper struct to pass around context information about reflection pattern
    /// as a single parameter (and have a way to extend this in the future if we need to easily).
    /// Also implements a simple validation mechanism to check that the code does report patter recognition
    /// results for all methods it works on.
    /// The promise of the pattern recorder is that for a given reflection method, it will either not talk
    /// about it ever, or it will always report recognized/unrecognized.
    /// </summary>
    struct ReflectionPatternContext : IDisposable
    {
        readonly Logger _logger;
        readonly int? _ilOffset;

#if DEBUG
        bool _patternAnalysisAttempted;
        bool _patternReported;
#endif

        public TypeSystemEntity Source { get; private set; }
        public Origin MemberWithRequirements { get; private set; }
        public bool ReportingEnabled { get; private set; }

        public ReflectionPatternContext(
            Logger logger,
            bool reportingEnabled,
            TypeSystemEntity source,
            Origin memberWithRequirements,
            int? ilOffset = null)
        {
            _logger = logger;
            ReportingEnabled = reportingEnabled;
            Source = source;
            MemberWithRequirements = memberWithRequirements;
            _ilOffset = ilOffset;

#if DEBUG
            _patternAnalysisAttempted = false;
            _patternReported = false;
#endif
        }

#pragma warning disable CA1822
        [Conditional("DEBUG")]
        public void AnalyzingPattern()
        {
#if DEBUG
            _patternAnalysisAttempted = true;
#endif
        }

        [Conditional("DEBUG")]
        public void RecordHandledPattern()
        {
#if DEBUG
            _patternReported = true;
#endif
        }
#pragma warning restore CA1822

        public void RecordRecognizedPattern(Action mark)
        {
#if DEBUG
            if (!_patternAnalysisAttempted)
                throw new InvalidOperationException($"Internal error: To correctly report all patterns, when starting to analyze a pattern the AnalyzingPattern must be called first. {Source} -> {MemberWithRequirements}");

            _patternReported = true;
#endif

            mark();
        }

        public void RecordUnrecognizedPattern(int messageCode, string message)
        {
#if DEBUG
            if (!_patternAnalysisAttempted)
                throw new InvalidOperationException($"Internal error: To correctly report all patterns, when starting to analyze a pattern the AnalyzingPattern must be called first. {Source} -> {MemberWithRequirements}");

            _patternReported = true;
#endif

            if (ReportingEnabled)
            {
                _logger.LogWarning(message, messageCode, Source, _ilOffset, MessageSubCategory.TrimAnalysis);
            }
        }

        public void Dispose()
        {
#if DEBUG
            if (_patternAnalysisAttempted && !_patternReported)
                throw new InvalidOperationException($"Internal error: A reflection pattern was analyzed, but no result was reported. {Source} -> {MemberWithRequirements}");
#endif
        }
    }
}
