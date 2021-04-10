// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.SourceGeneration;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.Json
{
    /// <summary>
    /// Contains the extensions methods for using JSON as the content-type in HttpClient.
    /// </summary>
    public static partial class HttpClientJsonExtensions
    {
        public static Task<object?> GetFromJsonAsync(
            this HttpClient client,
            string? requestUri,
            [DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] Type type,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            Task<HttpResponseMessage> taskResponse = client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return GetFromJsonAsyncCore(taskResponse, type, options, cancellationToken);
        }

        public static Task<object?> GetFromJsonAsync(
            this HttpClient client,
            Uri? requestUri,
            [DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] Type type,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            Task<HttpResponseMessage> taskResponse = client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return GetFromJsonAsyncCore(taskResponse, type, options, cancellationToken);
        }

        public static Task<TValue?> GetFromJsonAsync<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] TValue>(
            this HttpClient client,
            string? requestUri,
            JsonSerializerContext context,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Task<HttpResponseMessage> taskResponse = client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return GetFromJsonAsyncCore<TValue>(taskResponse, context, cancellationToken);
        }

        public static Task<TValue?> GetFromJsonAsync<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] TValue>(
            this HttpClient client,
            string? requestUri,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            Task<HttpResponseMessage> taskResponse = client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return GetFromJsonAsyncCore<TValue>(taskResponse, options, cancellationToken);
        }

        public static Task<TValue?> GetFromJsonAsync<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] TValue>(
            this HttpClient client,
            Uri? requestUri,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            Task<HttpResponseMessage> taskResponse = client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return GetFromJsonAsyncCore<TValue>(taskResponse, options, cancellationToken);
        }

        public static Task<object?> GetFromJsonAsync(
            this HttpClient client,
            string? requestUri,
            [DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] Type type,
            CancellationToken cancellationToken = default)
            => client.GetFromJsonAsync(requestUri, type, options: null, cancellationToken);

        public static Task<object?> GetFromJsonAsync(
            this HttpClient client,
            Uri? requestUri,
            [DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] Type type,
            CancellationToken cancellationToken = default)
            => client.GetFromJsonAsync(requestUri, type, options: null, cancellationToken);

        public static Task<TValue?> GetFromJsonAsync<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] TValue>(
            this HttpClient client,
            string? requestUri,
            CancellationToken cancellationToken = default)
            => client.GetFromJsonAsync<TValue>(requestUri, options: null, cancellationToken);

        public static Task<TValue?> GetFromJsonAsync<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] TValue>(
            this HttpClient client,
            Uri? requestUri,
            CancellationToken cancellationToken = default)
            => client.GetFromJsonAsync<TValue>(requestUri, options: null, cancellationToken);

        private static async Task<object?> GetFromJsonAsyncCore(
            Task<HttpResponseMessage> taskResponse,
            [DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] Type type,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await taskResponse.ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                // Nullable forgiving reason:
                // GetAsync will usually return Content as not-null.
                // If Content happens to be null, the extension will throw.
                return await response.Content!.ReadFromJsonAsync(type, options, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<T?> GetFromJsonAsyncCore<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] T>(
            Task<HttpResponseMessage> taskResponse,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await taskResponse.ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                // Nullable forgiving reason:
                // GetAsync will usually return Content as not-null.
                // If Content happens to be null, the extension will throw.
                return await response.Content!.ReadFromJsonAsync<T>(options, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<T?> GetFromJsonAsyncCore<[DynamicallyAccessedMembers(Helper.MembersAccessedOnRead)] T>(
            Task<HttpResponseMessage> taskResponse,
            JsonSerializerContext context,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await taskResponse.ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                // Nullable forgiving reason:
                // GetAsync will usually return Content as not-null.
                // If Content happens to be null, the extension will throw.
                return await response.Content!.ReadFromJsonAsync<T>(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
