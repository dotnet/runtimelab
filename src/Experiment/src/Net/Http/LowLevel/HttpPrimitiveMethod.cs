// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Text;

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// A HTTP Method.
    /// </summary>
    /// <remarks>
    /// This will become <see cref="HttpVersion"/>.
    /// </remarks>
    public sealed class HttpPrimitiveMethod
    {
        internal readonly byte[] _http1Encoded;
        internal readonly bool _hasRequestContent;
        private readonly string _methodName;

        /// <summary>
        /// The GET method.
        /// </summary>
        public static HttpPrimitiveMethod Get { get; } = new HttpPrimitiveMethod("GET", hasRequestContent: false);

        /// <summary>
        /// The HEAD method.
        /// </summary>
        public static HttpPrimitiveMethod Head { get; } = new HttpPrimitiveMethod("HEAD", hasRequestContent: false);

        /// <summary>
        /// The POST method.
        /// </summary>
        public static HttpPrimitiveMethod Post { get; } = new HttpPrimitiveMethod("POST");

        /// <summary>
        /// The PUT method.
        /// </summary>
        public static HttpPrimitiveMethod Put { get; } = new HttpPrimitiveMethod("PUT");

        /// <summary>
        /// The DELETE method.
        /// </summary>
        public static HttpPrimitiveMethod Delete { get; } = new HttpPrimitiveMethod("DELETE", hasRequestContent: false);

        /// <summary>
        /// The CONNECT method.
        /// </summary>
        public static HttpPrimitiveMethod Connect { get; } = new HttpPrimitiveMethod("CONNECT");

        /// <summary>
        /// The OPTIONS method.
        /// </summary>
        public static HttpPrimitiveMethod Options { get; } = new HttpPrimitiveMethod("OPTIONS");

        /// <summary>
        /// The TRACE method.
        /// </summary>
        public static HttpPrimitiveMethod Trace { get; } = new HttpPrimitiveMethod("TRACE", hasRequestContent: false);

        /// <summary>
        /// Instantiates a new <see cref="HttpPrimitiveMethod"/>.
        /// </summary>
        /// <param name="methodName">The name of the method.</param>
        public HttpPrimitiveMethod(string methodName)
            : this(methodName, hasRequestContent: true)
        {
        }

        internal HttpPrimitiveMethod(string methodName, bool hasRequestContent)
        {
            _methodName = methodName;
            _http1Encoded = Encoding.UTF8.GetBytes(methodName);
            _hasRequestContent = hasRequestContent;
        }

        public override string ToString() =>
            _methodName;

        public static HttpPrimitiveMethod? Lookup(string methodName) => methodName switch
        {
            "GET" => Get,
            "HEAD" => Head,
            "POST" => Post,
            "PUT" => Put,
            "DELETE" => Delete,
            "CONNECT" => Connect,
            "OPTIONS" => Options,
            "TRACE" => Trace,
            _ => null
        };
    }
}
