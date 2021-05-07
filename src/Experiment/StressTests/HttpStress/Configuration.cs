// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;


namespace HttpStress
{
    [Flags]
    public enum RunMode { server = 1, client = 2, both = server | client };

    public class Configuration
    {
        public string ServerUri { get; init; } = "";
        public RunMode RunMode { get; init; }
        public bool ListOperations { get; init; }

        public Version HttpVersion { get; init; } = new ();
        public bool UseWinHttpHandler { get; init; }
        public int ConcurrentRequests { get; init; }
        public int RandomSeed { get; init; }
        public int MaxContentLength { get; init; }
        public int MaxRequestUriSize { get; init; }
        public int MaxRequestHeaderCount { get; init; }
        public int MaxRequestHeaderTotalSize { get; init; }
        public int MaxParameters { get; init; }
        public int[]? OpIndices { get; init; }
        public int[]? ExcludedOpIndices { get; init; }
        public TimeSpan DisplayInterval { get; init; }
        public TimeSpan DefaultTimeout { get; init; }
        public TimeSpan? ConnectionLifetime { get; init; }
        public TimeSpan? MaximumExecutionTime { get; init; }
        public double CancellationProbability { get; init; }

        public bool UseLlHttp { get; init; }

        public bool UseHttpSys { get; init; }
        public bool LogAspNet { get; init; }
        public bool Trace { get; init; }
        public int? ServerMaxConcurrentStreams { get; init; }
        public int? ServerMaxFrameSize { get; init; }
        public int? ServerInitialConnectionWindowSize { get; init; }
        public int? ServerMaxRequestHeaderFieldSize { get; init; }
    }

}
