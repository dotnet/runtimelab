using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests
{
    internal static class TestExtensions
    {
        public static async Task<TestHeadersSink> ReadAllHeadersAsync(this ValueHttpRequest request)
        {
            var sink = new TestHeadersSink();

            if (await request.ReadToHeadersAsync().ConfigureAwait(false))
            {
                await request.ReadHeadersAsync(sink, state: null).ConfigureAwait(false);
            }

            return sink;
        }

        public static async Task<TestHeadersSink> ReadAllTrailingHeadersAsync(this ValueHttpRequest request)
        {
            var sink = new TestHeadersSink();

            if (await request.ReadToTrailingHeadersAsync().ConfigureAwait(false))
            {
                await request.ReadHeadersAsync(sink, state: null).ConfigureAwait(false);
            }

            return sink;
        }

        public static async Task<byte[]> ReadAllContentAsync(this ValueHttpRequest request)
        {
            var memoryStream = new MemoryStream();

            var contentStream = new HttpContentStream(request, ownsRequest: false);
            await using (contentStream.ConfigureAwait(false))
            {
                await contentStream.CopyToAsync(memoryStream).ConfigureAwait(false);
            }

            return memoryStream.ToArray();
        }

        public static async Task<string> ReadAllContentAsStringAsync(this ValueHttpRequest request)
            => Encoding.UTF8.GetString(await ReadAllContentAsync(request).ConfigureAwait(false));

        public static void WriteHeaders(this ValueHttpRequest request, TestHeadersSink headers)
        {
            foreach (KeyValuePair<string, List<string>> header in headers)
            {
                foreach (string headerValue in header.Value)
                {
                    request.WriteHeader(header.Key, headerValue);
                }
            }
        }

        public static void WriteTrailingHeaders(this ValueHttpRequest request, TestHeadersSink headers)
        {
            foreach (KeyValuePair<string, List<string>> header in headers)
            {
                foreach (string headerValue in header.Value)
                {
                    request.WriteTrailingHeader(header.Key, headerValue);
                }
            }
        }

        public static ValueTask WriteContentAsync(this ValueHttpRequest request, string content) =>
            request.WriteContentAsync(Encoding.UTF8.GetBytes(content));

        public static async Task WriteContentAsync(this ValueHttpRequest request, List<string> content)
        {
            foreach (string chunk in content)
            {
                await request.WriteContentAsync(chunk).ConfigureAwait(false);
            }
        }
    }
}
