// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.Json
{
    public static partial class HttpContentJsonExtensions
    {
        public static Task<object?> ReadFromJsonAsync(this HttpContent content, [DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] Type type, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            Encoding? sourceEncoding = JsonContent.GetEncoding(content.Headers.ContentType?.CharSet);

            return ReadFromJsonAsyncCore(content, type, sourceEncoding, options, cancellationToken);
        }

        public static Task<T?> ReadFromJsonAsync<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] T>(this HttpContent content, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            Encoding? sourceEncoding = JsonContent.GetEncoding(content.Headers.ContentType?.CharSet);

            return ReadFromJsonAsyncCore<T>(content, sourceEncoding, options, cancellationToken);
        }

        internal static Task<T?> ReadFromJsonAsync<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] T>(
            this HttpContent content,
            JsonSerializerContext context,
            CancellationToken cancellationToken = default)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            Encoding? sourceEncoding = JsonContent.GetEncoding(content.Headers.ContentType?.CharSet);

            return ReadFromJsonAsyncCore<T>(content, sourceEncoding, context, cancellationToken);
        }

        private static async Task<object?> ReadFromJsonAsyncCore(
            HttpContent content,
            [DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] Type type,
            Encoding? sourceEncoding,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken)
        {
            Stream contentStream = await ReadHttpContentStreamAsync(content, cancellationToken).ConfigureAwait(false);

            // Wrap content stream into a transcoding stream that buffers the data transcoded from the sourceEncoding to utf-8.
            if (sourceEncoding != null && sourceEncoding != Encoding.UTF8)
            {
                contentStream = GetTranscodingStream(contentStream, sourceEncoding);
            }

            using (contentStream)
            {
                return await JsonSerializer.DeserializeAsync(contentStream, type, options ?? JsonContent.s_defaultSerializerOptions, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<T?> ReadFromJsonAsyncCore<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] T>(
            HttpContent content,
            Encoding? sourceEncoding,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken)
        {
            Stream contentStream = await ReadHttpContentStreamAsync(content, cancellationToken).ConfigureAwait(false);

            // Wrap content stream into a transcoding stream that buffers the data transcoded from the sourceEncoding to utf-8.
            if (sourceEncoding != null && sourceEncoding != Encoding.UTF8)
            {
                contentStream = GetTranscodingStream(contentStream, sourceEncoding);
            }

            using (contentStream)
            {
                return await JsonSerializer.DeserializeAsync<T>(contentStream, options ?? JsonContent.s_defaultSerializerOptions, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<T?> ReadFromJsonAsyncCore<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] T>(
            HttpContent content,
            Encoding? sourceEncoding,
            JsonSerializerContext context,
            CancellationToken cancellationToken)
        {
            Stream contentStream = await ReadHttpContentStreamAsync(content, cancellationToken).ConfigureAwait(false);

            // Wrap content stream into a transcoding stream that buffers the data transcoded from the sourceEncoding to utf-8.
            if (sourceEncoding != null && sourceEncoding != Encoding.UTF8)
            {
                contentStream = GetTranscodingStream(contentStream, sourceEncoding);
            }

            using (contentStream)
            {
                Debug.Assert(context != null);
                return await JsonSerializer.DeserializeAsync<T>(contentStream, context, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
