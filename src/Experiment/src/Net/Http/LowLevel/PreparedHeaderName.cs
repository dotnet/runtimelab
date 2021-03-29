// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Net.Http.LowLevel.Headers;

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// A prepared header name.
    /// </summary>
    public class PreparedHeaderName
    {
        internal readonly byte[] _http1Encoded, _http2Encoded;
        internal readonly uint _http2StaticIndex;

        /// <summary>
        /// The name of the header.
        /// </summary>
        public string Name { get; }

        internal PreparedHeaderName(string name, uint http2StaticIndex = 0)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException($"{nameof(name)} must not be null or empty.", nameof(name));

            Name = name;
            _http1Encoded = Encoding.ASCII.GetBytes(name + ": ");
            _http2Encoded = Encoding.ASCII.GetBytes(name.ToLowerInvariant());
            _http2StaticIndex = http2StaticIndex;
        }

        /// <summary>
        /// Instantiates a new <see cref="PreparedHeaderName"/>.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        public PreparedHeaderName(string name)
            : this(name, 0)
        {
        }

        /// <inheritdoc/>
        public override string ToString() => Name;

        internal static PreparedHeaderName PseudoAuthority { get; } = new PreparedHeaderName(":authority", http2StaticIndex: 1);
        internal static MethodHeader PseudoMethod { get; } = new MethodHeader();
        internal static PathHeader PseudoPath { get; } = new PathHeader();
        internal static SchemeHeader PseudoScheme { get; } = new SchemeHeader();
        internal static StatusHeader PseudoStatus { get; } = new StatusHeader();

        /// <summary>
        /// The Accept-Charset header.
        /// </summary>
        public static PreparedHeaderName AcceptCharset { get; } = new PreparedHeaderName("Accept-Charset", http2StaticIndex: 15);

        /// <summary>
        /// The Accept-Encoding header.
        /// </summary>
        public static AcceptEncodingHeader AcceptEncoding { get; } = new AcceptEncodingHeader();

        /// <summary>
        /// The Accept-Language header.
        /// </summary>
        public static PreparedHeaderName AcceptLanguage { get; } = new PreparedHeaderName("Accept-Language", http2StaticIndex: 17);

        /// <summary>
        /// The Accept-Ranges header.
        /// </summary>
        public static PreparedHeaderName AcceptRanges { get; } = new PreparedHeaderName("Accept-Ranges", http2StaticIndex: 18);

        /// <summary>
        /// The Accept header.
        /// </summary>
        public static PreparedHeaderName Accept { get; } = new PreparedHeaderName("Accept", http2StaticIndex: 19);

        /// <summary>
        /// The Access-Control-Allow-Origin header.
        /// </summary>
        public static PreparedHeaderName AccessControlAllowOrigin { get; } = new PreparedHeaderName("Access-Control-Allow-Origin", http2StaticIndex: 20);

        /// <summary>
        /// The Age header.
        /// </summary>
        public static PreparedHeaderName Age { get; } = new PreparedHeaderName("Age", http2StaticIndex: 21);

        /// <summary>
        /// The Allow header.
        /// </summary>
        public static PreparedHeaderName Allow { get; } = new PreparedHeaderName("Allow", http2StaticIndex: 22);

        /// <summary>
        /// The Authorization header.
        /// </summary>
        public static PreparedHeaderName Authorization { get; } = new PreparedHeaderName("Authorization", http2StaticIndex: 23);

        /// <summary>
        /// The Cache-Control header.
        /// </summary>
        public static PreparedHeaderName CacheControl { get; } = new PreparedHeaderName("Cache-Control", http2StaticIndex: 24);

        /// <summary>
        /// The Content-Disposition header.
        /// </summary>
        public static PreparedHeaderName ContentDisposition { get; } = new PreparedHeaderName("Content-Disposition", http2StaticIndex: 25);

        /// <summary>
        /// The Content-Encoding header.
        /// </summary>
        public static PreparedHeaderName ContentEncoding { get; } = new PreparedHeaderName("Content-Encoding", http2StaticIndex: 26);

        /// <summary>
        /// The Content-Language header.
        /// </summary>
        public static PreparedHeaderName ContentLanguage { get; } = new PreparedHeaderName("Content-Language", http2StaticIndex: 27);

        /// <summary>
        /// The Content-Length header.
        /// </summary>
        public static PreparedHeaderName ContentLength { get; } = new PreparedHeaderName("Content-Length", http2StaticIndex: 28);

        /// <summary>
        /// The Content-Location header.
        /// </summary>
        public static PreparedHeaderName ContentLocation { get; } = new PreparedHeaderName("Content-Location", http2StaticIndex: 29);

        /// <summary>
        /// The Content-Range header.
        /// </summary>
        public static PreparedHeaderName ContentRange { get; } = new PreparedHeaderName("Content-Range", http2StaticIndex: 30);

        /// <summary>
        /// The Content-Type header.
        /// </summary>
        public static PreparedHeaderName ContentType { get; } = new PreparedHeaderName("Content-Type", http2StaticIndex: 31);

        /// <summary>
        /// The Cookie header.
        /// </summary>
        public static PreparedHeaderName Cookie { get; } = new PreparedHeaderName("Cookie", http2StaticIndex: 32);

        /// <summary>
        /// The Date header.
        /// </summary>
        public static PreparedHeaderName Date { get; } = new PreparedHeaderName("Date", http2StaticIndex: 33);

        /// <summary>
        /// The ETag header.
        /// </summary>
        public static PreparedHeaderName ETag { get; } = new PreparedHeaderName("ETag", http2StaticIndex: 34);

        /// <summary>
        /// The Expect header.
        /// </summary>
        public static PreparedHeaderName Expect { get; } = new PreparedHeaderName("Expect", http2StaticIndex: 35);

        /// <summary>
        /// The Expires header.
        /// </summary>
        public static PreparedHeaderName Expires { get; } = new PreparedHeaderName("Expires", http2StaticIndex: 36);

        /// <summary>
        /// The From header.
        /// </summary>
        public static PreparedHeaderName From { get; } = new PreparedHeaderName("From", http2StaticIndex: 37);

        /// <summary>
        /// The Host header.
        /// </summary>
        public static PreparedHeaderName Host { get; } = new PreparedHeaderName("Host", http2StaticIndex: 38);

        /// <summary>
        /// The If-Match header.
        /// </summary>
        public static PreparedHeaderName IfMatch { get; } = new PreparedHeaderName("If-Match", http2StaticIndex: 39);

        /// <summary>
        /// The If-Modified-Since header.
        /// </summary>
        public static PreparedHeaderName IfModifiedSince { get; } = new PreparedHeaderName("If-Modified-Since", http2StaticIndex: 40);

        /// <summary>
        /// The If-None-Match header.
        /// </summary>
        public static PreparedHeaderName IfNoneMatch { get; } = new PreparedHeaderName("If-None-Match", http2StaticIndex: 41);

        /// <summary>
        /// The If-Range header.
        /// </summary>
        public static PreparedHeaderName IfRange { get; } = new PreparedHeaderName("If-Range", http2StaticIndex: 42);

        /// <summary>
        /// The If-Unmodified-Since header.
        /// </summary>
        public static PreparedHeaderName IfUnmodifiedSince { get; } = new PreparedHeaderName("If-Unmodified-Since", http2StaticIndex: 43);

        /// <summary>
        /// The Last-Modified header.
        /// </summary>
        public static PreparedHeaderName LastModified { get; } = new PreparedHeaderName("Last-Modified", http2StaticIndex: 44);

        /// <summary>
        /// The Link header.
        /// </summary>
        public static PreparedHeaderName Link { get; } = new PreparedHeaderName("Link", http2StaticIndex: 45);

        /// <summary>
        /// The Location header.
        /// </summary>
        public static PreparedHeaderName Location { get; } = new PreparedHeaderName("Location", http2StaticIndex: 46);

        /// <summary>
        /// The Max-Forwards header.
        /// </summary>
        public static PreparedHeaderName MaxForwards { get; } = new PreparedHeaderName("Max-Forwards", http2StaticIndex: 47);

        /// <summary>
        /// The Proxy-Authenticate header.
        /// </summary>
        public static PreparedHeaderName ProxyAuthenticate { get; } = new PreparedHeaderName("Proxy-Authenticate", http2StaticIndex: 48);

        /// <summary>
        /// The Proxy-Authorization header.
        /// </summary>
        public static PreparedHeaderName ProxyAuthorization { get; } = new PreparedHeaderName("Proxy-Authorization", http2StaticIndex: 49);

        /// <summary>
        /// The Range header.
        /// </summary>
        public static PreparedHeaderName Range { get; } = new PreparedHeaderName("Range", http2StaticIndex: 50);

        /// <summary>
        /// The Referer header.
        /// </summary>
        public static PreparedHeaderName Referer { get; } = new PreparedHeaderName("Referer", http2StaticIndex: 51);

        /// <summary>
        /// The Refresh header.
        /// </summary>
        public static PreparedHeaderName Refresh { get; } = new PreparedHeaderName("Refresh", http2StaticIndex: 52);

        /// <summary>
        /// The Retry header.
        /// </summary>
        public static PreparedHeaderName RetryAfter { get; } = new PreparedHeaderName("Retry-After", http2StaticIndex: 53);

        /// <summary>
        /// The Server header.
        /// </summary>
        public static PreparedHeaderName Server { get; } = new PreparedHeaderName("Server", http2StaticIndex: 54);

        /// <summary>
        /// The Set-Cookie header.
        /// </summary>
        public static PreparedHeaderName SetCookie { get; } = new PreparedHeaderName("Set-Cookie", http2StaticIndex: 55);

        /// <summary>
        /// The Strict-Transport-Security header.
        /// </summary>
        public static PreparedHeaderName StrictTransportSecurity { get; } = new PreparedHeaderName("Strict-Transport-Security", http2StaticIndex: 56);

        /// <summary>
        /// The Transfer-Encoding header.
        /// </summary>
        public static PreparedHeaderName TransferEncoding { get; } = new PreparedHeaderName("Transfer-Encoding", http2StaticIndex: 57);

        /// <summary>
        /// The User-Agent header.
        /// </summary>
        public static PreparedHeaderName UserAgent { get; } = new PreparedHeaderName("User-Agent", http2StaticIndex: 58);

        /// <summary>
        /// The Vary header.
        /// </summary>
        public static PreparedHeaderName Vary { get; } = new PreparedHeaderName("Vary", http2StaticIndex: 59);

        /// <summary>
        /// The Via header.
        /// </summary>
        public static PreparedHeaderName Via { get; } = new PreparedHeaderName("Via", http2StaticIndex: 60);

        /// <summary>
        /// The WWW-Authenticate header.
        /// </summary>
        public static PreparedHeaderName WWWAuthenticate { get; } = new PreparedHeaderName("WWW-Authenticate", http2StaticIndex: 61);
    }
}
