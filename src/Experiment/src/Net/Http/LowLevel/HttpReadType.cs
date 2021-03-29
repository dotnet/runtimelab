// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// Indicates the type of element read from a <see cref="ValueHttpRequest"/>.
    /// </summary>
    public enum HttpReadType
    {
        /// <summary>
        /// Default/uninitialized value.
        /// <see cref="ValueHttpRequest.ReadAsync(System.Threading.CancellationToken)"/> should be called to read the first element.
        /// </summary>
        None,

        /// <summary>
        /// HTTP response.
        /// <see cref="ValueHttpRequest.StatusCode"/> and <see cref="ValueHttpRequest.Version"/> are now valid.
        /// <see cref="InformationalResponse"/> can be received zero or more times.
        /// </summary>
        InformationalResponse,

        /// <summary>
        /// HTTP response.
        /// <see cref="ValueHttpRequest.StatusCode"/> and <see cref="ValueHttpRequest.Version"/> are now valid.
        /// <see cref="FinalResponse"/> will only be returned for a final response, not informational responses.
        /// </summary>
        FinalResponse,

        /// <summary>
        /// HTTP response headers.
        /// <see cref="ValueHttpRequest.ReadHeadersAsync(IHttpHeadersSink, object?, System.Threading.CancellationToken)"/> should be called to read headers.
        /// </summary>
        Headers,

        /// <summary>
        /// HTTP response content.
        /// <see cref="ValueHttpRequest.ReadContentAsync(Memory{byte}, System.Threading.CancellationToken)"/> should be called, until it returns 0, to read content.
        /// <see cref="Content"/> can be received more than once and it is possible for other elements to be intermixed between them.
        /// </summary>
        Content,

        /// <summary>
        /// HTTP trailing headers.
        /// <see cref="ValueHttpRequest.ReadHeadersAsync(IHttpHeadersSink, object?, System.Threading.CancellationToken)"/> should be called to read headers.
        /// </summary>
        TrailingHeaders,

        /// <summary>
        /// The <see cref="ValueHttpRequest"/> has been fully ready and may be disposed.
        /// </summary>
        EndOfStream,

        /// <summary>
        /// The ALTSVC extension frame in HTTP/2.
        /// </summary>
        AltSvc
    }
}
