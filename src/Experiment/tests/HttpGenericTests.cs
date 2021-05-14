using System.Net.Http.LowLevel.Tests.Connections;
using System.Net.Http.LowLevel.Tests.Servers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Security;
using System.Threading.Tasks;
using Xunit;

namespace System.Net.Http.LowLevel.Tests
{
    public abstract class HttpGenericTests : TestsBase
    {
        [Theory]
        [MemberData(nameof(NonChunkedData))]
        public async Task Send_NonChunkedRequest_Success(int testIdx, TestHeadersSink requestHeaders, List<string> requestContent)
        {
            await RunSingleStreamTest(
                async (clientRequest, serverUri) =>
                {
                    await ClientSendHelperAsync(clientRequest, serverUri, testIdx, requestHeaders, requestContent, requestTrailingHeaders: null);
                },
                async serverStream =>
                {
                    HttpTestFullRequest request = await serverStream.ReceiveAndSendAsync();
                    Assert.True(request.Headers.Contains(requestHeaders));
                    Assert.Equal(string.Join("", requestContent), request.Content);
                });
        }

        [Theory]
        [MemberData(nameof(NonChunkedData))]
        public async Task Receive_NonChunkedResponse_Success(int testIdx, TestHeadersSink responseHeaders, List<string> responseContent)
        {
            _ = testIdx; // only used to assist debugging.

            await RunSingleStreamTest(
                async (clientRequest, serverUri) =>
                {
                    clientRequest.ConfigureRequest(hasContentLength: true, hasTrailingHeaders: false);
                    clientRequest.WriteRequestStart(HttpMethod.Get, serverUri);
                    await clientRequest.CompleteRequestAsync();

                    TestHeadersSink actualResponseHeaders = await clientRequest.ReadAllHeadersAsync();
                    Assert.True(actualResponseHeaders.Contains(responseHeaders));

                    string actualResponseContent = await clientRequest.ReadAllContentAsStringAsync();
                    Assert.Equal(string.Join("", responseContent), actualResponseContent);
                },
                async serverStream =>
                {
                    await serverStream.ReceiveAndSendAsync(headers: responseHeaders, content: string.Join("", responseContent));
                });
        }

        [Theory]
        [MemberData(nameof(ChunkedData))]
        public async Task Send_ChunkedRequest_Success(int testIdx, TestHeadersSink requestHeaders, List<string> requestContent, TestHeadersSink requestTrailingHeaders)
        {
            await RunSingleStreamTest(
                async (client, serverUri) =>
                {
                    await ClientSendHelperAsync(client, serverUri, testIdx, requestHeaders, requestContent, requestTrailingHeaders);
                },
                async server =>
                {
                    HttpTestFullRequest request = await server.ReceiveAndSendAsync();

                    if (request.Version.Major == 1)
                    {
                        Assert.Equal("chunked", request.Headers.GetSingleValue("transfer-encoding"));
                    }

                    Assert.True(request.Headers.Contains(requestHeaders));
                    Assert.Equal(string.Join("", requestContent), request.Content);
                    Assert.True(request.TrailingHeaders.Contains(requestTrailingHeaders));
                });
        }

        [Theory]
        [MemberData(nameof(ChunkedData))]
        public async Task Receive_ChunkedResponse_Success(int testIdx, TestHeadersSink responseHeaders, List<string> responseContent, TestHeadersSink responseTrailingHeaders)
        {
            _ = testIdx; // only used to assist debugging.

            await RunSingleStreamTest(
                async (client, serverUri) =>
                {
                    client.ConfigureRequest(hasContentLength: true, hasTrailingHeaders: false);
                    client.WriteRequestStart(HttpMethod.Get, serverUri);
                    client.WriteHeader("TE", "trailers");
                    await client.CompleteRequestAsync();

                    Assert.True(await client.ReadToFinalResponseAsync());

                    Version version = client.Version!;
                    Assert.NotNull(version);

                    TestHeadersSink headers = await client.ReadAllHeadersAsync();

                    if (version.Major == Version.Major)
                    {
                        Assert.Equal("chunked", headers.GetSingleValue("transfer-encoding"));
                    }

                    Assert.True(headers.Contains(responseHeaders));

                    string content = await client.ReadAllContentAsStringAsync();
                    Assert.Equal(string.Join("", responseContent), content);

                    TestHeadersSink trailers = await client.ReadAllTrailingHeadersAsync();
                    Assert.True(trailers.Contains(responseTrailingHeaders));
                },
                async server =>
                {
                    await server.ReceiveAndSendChunkedAsync(headers: responseHeaders, content: responseContent, trailingHeaders: responseTrailingHeaders);
                });
        }

        [Fact]
        public async Task Send_MultipleRequests_Sequential_Success()
        {
            await RunMultiStreamTest(
                async (client, uri) =>
                {
                    foreach (var (testIdx, headers, content, trailingHeaders, isChunked) in InterleavedData())
                    {
                        await using ValueHttpRequest request = await client.CreateNewRequestAsync(Version, HttpVersionPolicy.RequestVersionExact) ??
                                                               throw new Exception("Can't create request");
                        await ClientSendHelperAsync(request, uri, testIdx, headers, content, trailingHeaders, isChunked);
                    }
                },
                async server =>
                {
                    foreach (var (testIdx, headers, content, trailingHeaders, _) in InterleavedData())
                    {
                        await using HttpTestStream serverStream = await server.AcceptStreamAsync();

                        HttpTestFullRequest request = await serverStream.ReceiveAndSendAsync(200,
                            new TestHeadersSink {{"Test-Index", new List<string> {testIdx.ToString(CultureInfo.InvariantCulture)}}});
                        Assert.True(request.Headers.Contains(headers));
                        Assert.Equal(string.Join("", content), request.Content);

                        if (trailingHeaders is not null)
                        {
                            Assert.True(request.TrailingHeaders.Contains(trailingHeaders));
                        }
                    }
                }, millisecondsTimeout: DefaultTestTimeout * 10);
        }

        private async Task ClientSendHelperAsync(ValueHttpRequest client, Uri serverUri, int testIdx, TestHeadersSink requestHeaders, List<string> requestContent, TestHeadersSink? requestTrailingHeaders, bool isChunked = false)
        {
            long contentLength = requestContent.Sum(x => (long)x.Length);
            client.ConfigureRequest(hasContentLength: !isChunked, hasTrailingHeaders: requestTrailingHeaders != null);
            client.WriteRequestStart(HttpMethod.Post, serverUri);
            client.WriteHeader("Content-Length", contentLength.ToString(CultureInfo.InvariantCulture));
            client.WriteHeader("Test-Index", testIdx.ToString(CultureInfo.InvariantCulture));
            client.WriteHeaders(requestHeaders);

            foreach (string content in requestContent)
            {
                await client.WriteContentAsync(content);
            }

            if (requestTrailingHeaders != null)
            {
                client.WriteTrailingHeaders(requestTrailingHeaders);
            }

            await client.CompleteRequestAsync();
            Assert.True(await client.ReadToFinalResponseAsync());
            await client.DrainAsync();
        }

        public static IEnumerable<(int testidx, TestHeadersSink headers, List<string> content, TestHeadersSink? trailingHeaders, bool isChunked)> InterleavedData()
        {
            static IEnumerable<T> Combine<T>(IEnumerable<T> first, IEnumerable<T> second)
            {
                IEnumerator<T> firstEnumerator = first.GetEnumerator();
                IEnumerator<T> secondEnumerator = second.GetEnumerator();

                var hasFirst = firstEnumerator.MoveNext();
                var hasSecond = secondEnumerator.MoveNext();
                while (hasFirst || hasSecond)
                {
                    if (hasFirst)
                    {
                        yield return firstEnumerator.Current;
                        hasFirst = firstEnumerator.MoveNext();
                    }

                    if (hasSecond)
                    {
                        yield return secondEnumerator.Current;
                        hasSecond = secondEnumerator.MoveNext();
                    }
                }
            }

            var chunked = ChunkedData().Select(x => (headers: (TestHeadersSink)x[1], content: (List<string>)x[2],
                trailing: (TestHeadersSink?)x[3], isChunked: true));
            var nonChunked = NonChunkedData().Select(x => (headers: (TestHeadersSink)x[1],
                content: (List<string>)x[2], trailing: (TestHeadersSink?)null, isChunked: false));

            return Combine(nonChunked, chunked)
                .Select((x, index) => (index, x.headers, x.content, x.trailing, x.isChunked));
        }

        public static IEnumerable<object[]> NonChunkedData()
        {
            int testIdx = 0;

            foreach (TestHeadersSink headers in HeadersData())
            {
                foreach (List<string> contents in ContentData())
                {
                    ++testIdx;

                    yield return new object[] { testIdx, headers, contents };
                }
            }
        }

        public static IEnumerable<object[]> ChunkedData()
        {
            int testIdx = 0;

            foreach (TestHeadersSink headers in HeadersData())
            {
                foreach (List<string> contents in ContentData())
                {
                    foreach (TestHeadersSink trailers in HeadersData())
                    {
                        var trailersToSend = new TestHeadersSink();
                        foreach (var kvp in trailers)
                        {
                            trailersToSend.Add(kvp.Key + "-trailer", kvp.Value);
                        }

                        ++testIdx;

                        yield return new object[] { testIdx, headers, contents, trailersToSend };
                    }
                }
            }
        }

        private static IEnumerable<TestHeadersSink> HeadersData() => new[]
        {
            new TestHeadersSink
            {
            },
            new TestHeadersSink
            {
                { "foo", "1234" }
            },
            new TestHeadersSink
            {
                { "foo", "5678" },
                { "bar", "9012" }
            },
            new TestHeadersSink
            {
                { "foo", "3456" },
                { "bar", "7890" },
                { "quz", "1234" }
            },
            new TestHeadersSink
            {
                { "accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9" },
                { "accept-encoding", "gzip, deflate, br" },
                { "accept-language", "en-US,en;q=0.9" },
                { "cookie", "cookie: aaaa=000000000000000000000000000000000000; bbb=111111111111111111111111111; ccccc=22222222222222222222222222; dddddd=333333333333333333333333333333333333333333333333333333333333333333333; eeee=444444444444444444444444444444444444444444444444444444444444444444444444444" },
                { "sec-fetch-dest", "document" },
                { "sec-fetch-mode", "navigate" },
                { "sec-fetch-site", "none" },
                { "sec-fetch-user", "?1" },
                { "upgrade-insecure-requests", "1" },
                { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.198 Safari/537.36 Edg/86.0.622.69" }
            },
            new TestHeadersSink
            {
                { "Accept-Ranges", "bytes" },
                { "Cache-Control", "private" },
                { "Content-Security-Policy", "upgrade-insecure-requests; frame-ancestors 'self' https://stackexchange.com" },
                { "Content-Type", "text/html; charset=utf-8" },
                { "Date", "Mon, 16 Nov 2020 23:35:36 GMT" },
                { "Feature-Policy", "microphone 'none'; speaker 'none'" },
                { "Server", "Microsoft-IIS/10.0" },
                { "Strict-Transport-Security", "max-age=15552000" },
                { "Vary", "Accept-Encoding,Fastly-SSL" },
                { "Via", "1.1 varnish" },
                { "x-account-id", "12345" },
                { "x-aspnet-duration-ms", "44" },
                { "x-cache", "MISS" },
                { "x-cache-hits", "0" },
                { "x-dns-prefetch-control", "off" },
                { "x-flags", "QA" },
                { "x-frame-options", "SAMEORIGIN" },
                { "x-http-count", "2" },
                { "x-http-duration-ms", "8" },
                { "x-is-crawler", "0" },
                { "x-page-view", "1" },
                { "x-providence-cookie", "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" },
                { "x-redis-count", "22" },
                { "x-redis-duration-ms", "2" },
                { "x-request-guid", "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" },
                { "x-route-name", "Home/Index" },
                { "x-served-by", "cache-sea4460-SEA" },
                { "x-sql-count", "12" },
                { "x-sql-duration-ms", "12" },
                { "x-timer", "S1605569737.604081,VS0,VE106" }
            }
        };

        private static IEnumerable<List<string>> ContentData() => new[]
        {
            new List<string> { },
            new List<string> { "foo" },
            new List<string> { "foo", "barbar" },
            new List<string> { "foo", "barbar", "bazbazbaz" }
        };

        internal virtual bool UseSockets => false;
        internal virtual bool UseSsl => false;
        internal virtual bool Trickle => false;
        internal virtual bool TrickleForceAsync => false;
        internal abstract HttpPrimitiveVersion Version { get; }

        protected HttpGenericTests()
        {
            if (UseSockets) DefaultTestTimeout *= 10;
            if (UseSsl) DefaultTestTimeout *= 2;
            if (Trickle) DefaultTestTimeout *= 2;
        }

        internal virtual ConnectionFactory CreateConnectionFactory()
        {
            ConnectionFactory factory = UseSockets ? new SocketConnectionFactory() : new MemoryConnectionFactory();
            if (UseSsl)
            {
                factory = new SslConnectionFactory(factory);

                // implements IEnhancedStream, needed because SslStream does not yet have support.
                factory = new WriteBufferingConnectionFactory(factory);
            }
            if (Trickle) factory = new TricklingConnectionFactory(factory) { ForceAsync = TrickleForceAsync };
            return factory;
        }

        internal virtual IConnectionProperties? CreateConnectProperties()
        {
            if (!UseSsl)
            {
                return null;
            }

            var properties = new ConnectionProperties();
            properties.Add(SslConnectionFactory.SslClientAuthenticationOptionsPropertyKey, new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = delegate { return true; },
                TargetHost = "localhost"
            });
            return properties;
        }

        internal virtual IConnectionProperties? CreateListenerProperties()
        {
            if (!UseSsl)
            {
                return null;
            }

            var properties = new ConnectionProperties();
            properties.Add(SslConnectionFactory.SslServerAuthenticationOptionsPropertyKey, new SslServerAuthenticationOptions
            {
                ServerCertificate = TestCertificates.GetSelfSigned13ServerCertificate()
            });
            return properties;
        }

        internal abstract Task<HttpTestServer> CreateTestServerAsync(ConnectionFactory connectionFactory);
        internal abstract Task<HttpConnection> CreateTestClientAsync(ConnectionFactory connectionFactory, EndPoint endPoint);

        internal virtual async Task RunMultiStreamTest(Func<HttpConnection, Uri, Task> clientFunc, Func<HttpTestConnection, Task> serverFunc, int? millisecondsTimeout = null)
        {
            ConnectionFactory connectionFactory = CreateConnectionFactory();
            await using (connectionFactory.ConfigureAwait(false))
            {
                var server = await CreateTestServerAsync(connectionFactory);
                await using (server.ConfigureAwait(false))
                {
                    await RunClientServer(RunClientAsync, RunServerAsync, millisecondsTimeout).ConfigureAwait(false);

                    async Task RunClientAsync()
                    {
                        HttpConnection httpConnection = await CreateTestClientAsync(connectionFactory, server.EndPoint!).ConfigureAwait(false);
                        await using (httpConnection.ConfigureAwait(false))
                        {
                            await clientFunc(httpConnection, server.Uri).ConfigureAwait(false);
                        }
                    }

                    async Task RunServerAsync()
                    {
                        HttpTestConnection connection = await server.AcceptAsync().ConfigureAwait(false);
                        await using (connection.ConfigureAwait(false))
                        {
                            await serverFunc(connection).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        internal async Task RunSingleStreamTest(Func<ValueHttpRequest, Uri, Task> clientFunc, Func<HttpTestStream, Task> serverFunc, int? millisecondsTimeout = null)
        {
            await RunMultiStreamTest(
                async (client, serverUri) =>
                {
                    ValueHttpRequest? request = await client.CreateNewRequestAsync(Version, HttpVersionPolicy.RequestVersionExact).ConfigureAwait(false);
                    Assert.NotNull(request);

                    await using (request.ConfigureAwait(false))
                    {
                        await clientFunc(request.Value, serverUri).ConfigureAwait(false);
                        await request.Value.DrainAsync().ConfigureAwait(false);
                    }
                },
                async server =>
                {
                    HttpTestStream request = await server.AcceptStreamAsync().ConfigureAwait(false);
                    await using (request.ConfigureAwait(false))
                    {
                        await serverFunc(request).ConfigureAwait(false);
                    }
                }, millisecondsTimeout).ConfigureAwait(false);
        }
    }
}
