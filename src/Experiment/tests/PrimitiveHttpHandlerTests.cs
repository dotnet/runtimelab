using System.Collections.Generic;
using System.Net.Http.LowLevel.Tests.Connections;
using System.Net.Http.LowLevel.Tests.Servers;
using System.Threading.Tasks;
using Xunit;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;

namespace System.Net.Http.LowLevel.Tests
{
    public class PrimitiveHttpHandlerTest : HttpGenericTests
    {

        private HttpPrimitiveVersion _version = HttpPrimitiveVersion.Version11;

        public static IEnumerable<object[]> HttpStatusCodes()
        {
            return Enum.GetValues<HttpStatusCode>().Select(status => new object[] {status});
        }


        [Theory]
        [MemberData(nameof(HttpStatusCodes))]
        public async Task ResponseProperty_StatusCode(HttpStatusCode statusCode)
        {
            await RunClientTest(
                async (client, serverUri) =>
                {
                    HttpResponseMessage response =
                        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, serverUri));

                    Assert.Equal(statusCode, response.StatusCode);
                },
                async server => { await server.ReceiveAndSendSingleRequestAsync((int) statusCode); });
        }

        [Fact]
        public async Task ResponseProperty_RequestMessage()
        {
            await RunClientTest(
                async (client, serverUri) =>
                {
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, serverUri);

                    HttpResponseMessage response = await client.SendAsync(requestMessage);

                    Assert.Equal(requestMessage, response.RequestMessage);
                },
                async server => { await server.ReceiveAndSendSingleRequestAsync(); });
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, "OK")]
        [InlineData(HttpStatusCode.ProxyAuthenticationRequired, "Proxy Authentication Required")]
        public async Task ResponseProperty_ReasonPhrase(HttpStatusCode statusCode, string reasonPhrase)
        {
            await RunClientTest(
                async (client, serverUri) =>
                {
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, serverUri);

                    HttpResponseMessage response = await client.SendAsync(requestMessage);

                    Assert.Equal(reasonPhrase, response.ReasonPhrase);
                },
                async server => { await server.ReceiveAndSendSingleRequestAsync((int) statusCode); });
        }

        [Theory]
        [InlineData(HttpCompletionOption.ResponseHeadersRead)]
        [InlineData(HttpCompletionOption.ResponseContentRead)]
        public async Task ResponseProperty_Headers(HttpCompletionOption httpCompletion)
        {
            await RunClientTest(
                async (client, serverUri) =>
                {
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, serverUri);

                    HttpResponseMessage response = await client.SendAsync(requestMessage, httpCompletion);

                    // Headers.
                    Assert.Equal(2, response.Headers.Count());
                    Assert.Equal(new[] {"CUSTOM_VALUE"}, response.Headers.GetValues("X-CUSTOM-HEADER"));
                    Assert.True(response.Headers.TransferEncodingChunked); // This one is added by the test server.
                    // Content headers.
                    Assert.Equal(3, response.Content.Headers.Count());
                    Assert.Equal(new[] {"de", "en"}, response.Content.Headers.ContentLanguage);
                    Assert.Equal(new DateTimeOffset(2015, 10, 21, 7, 28, 0, TimeSpan.Zero),
                        response.Content.Headers.Expires);
                    Assert.Equal(12, response.Content.Headers.ContentLength);

                    // Trailing headers.
                    if (httpCompletion == HttpCompletionOption.ResponseHeadersRead)
                    {
                        // Headers are available when the content is read.
                        Assert.Empty(response.TrailingHeaders);
                        _ = response.Content.ReadAsStringAsync();
                    }

                    Assert.Equal(2, response.TrailingHeaders.Count());
                    Assert.Equal(new[] {"TRAILING VALUE"}, response.TrailingHeaders.GetValues("TRAILING-HEADER"));
                    Assert.Equal(new[] {"TRAILING VALUE2"}, response.TrailingHeaders.GetValues("TRAILING-HEADER2"));
                },
                async server =>
                {
                    await server.ReceiveAndSendSingleRequestAsync(200,
                        new TestHeadersSink
                        {
                            {"Content-Language", "de, en"},
                            {"X-CUSTOM-HEADER", "CUSTOM_VALUE"},
                            {"Expires", "Wed, 21 Oct 2015 07:28:00 GMT"}
                        },
                        "Some content", new TestHeadersSink
                        {
                            {"TRAILING-HEADER", "TRAILING VALUE"},
                            {"TRAILING-HEADER2", "TRAILING VALUE2"},
                        }
                    );
                });
        }

        [Fact]
        public async Task ResponseProperty_Content_String()
        {
            string testContent = "Some content";
            await RunClientTest(
                async (client, serverUri) =>
                {
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, serverUri);

                    HttpResponseMessage response = await client.SendAsync(requestMessage);
                    Assert.Equal(testContent, await response.Content.ReadAsStringAsync());
                },
                async server =>
                {
                    await server.ReceiveAndSendSingleRequestAsync(200,
                        null,
                        testContent
                    );
                });
        }

        [Fact]
        public async Task ResponseProperty_Content_Stream()
        {
            string testContent = "Some content";
            await RunClientTest(
                async (client, serverUri) =>
                {
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, serverUri);

                    HttpResponseMessage response = await client.SendAsync(requestMessage);
                    Assert.Equal(testContent,
                        await new StreamReader(await response.Content.ReadAsStreamAsync()).ReadToEndAsync());
                },
                async server =>
                {
                    await server.ReceiveAndSendSingleRequestAsync(200,
                        null,
                        testContent
                    );
                });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RequestProperty_Content(bool chunked)
        {
            string testContent = "Some content";

            await RunClientTest(
                async (client, serverUri) =>
                {
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, serverUri)
                    {
                        Content = new StringContent(testContent),
                        Headers = {TransferEncodingChunked = chunked}
                    };

                    requestMessage.Content.Headers.ContentLength = chunked ? null : testContent.Length;

                    using HttpResponseMessage response = await client.SendAsync(requestMessage);

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                },
                async server =>
                {
                    await using HttpTestStream stream = await server.AcceptStreamAsync();
                    HttpTestFullRequest request = await stream.ReceiveAndSendAsync();

                    Assert.Equal(testContent, request.Content);
                });
        }

        [Fact]
        public async Task RequestProperty_Headers()
        {
            string testContent = "Some content";

            await RunClientTest(
                async (client, serverUri) =>
                {
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, serverUri)
                    {
                        Content = new StringContent(testContent)
                        {
                            Headers =
                            {
                                {"X-CONTENT-CUSTOM-HEADER", "CONTENT_CUSTOM_VALUE"},
                                {"Expires", "Wed, 21 Oct 2015 07:28:00 GMT"},
                                {"Content-Length", testContent.Length.ToString()}
                            }
                        },
                        Headers = {{"X-CUSTOM-HEADER", "CUSTOM_VALUE"}}
                    };
                    requestMessage.Headers.TransferEncodingChunked = false;
                    requestMessage.Headers.AcceptLanguage.ParseAdd("en,de");


                    using HttpResponseMessage response = await client.SendAsync(requestMessage);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                },
                async server =>
                {
                    await using HttpTestStream stream = await server.AcceptStreamAsync();
                    HttpTestFullRequest request = await stream.ReceiveAndSendAsync();
                    Assert.Equal(7, request.Headers.Count);
                    Assert.NotNull(request.Headers["host"]);
                    Assert.Equal(new[] {"text/plain; charset=utf-8"}, request.Headers["content-type"]);
                    Assert.Equal(new[] {"Wed, 21 Oct 2015 07:28:00 GMT"}, request.Headers["expires"]);
                    Assert.Equal(new[] {"12"}, request.Headers["Content-Length"]);
                    Assert.Equal(new[] {"CUSTOM_VALUE"}, request.Headers["X-CUSTOM-HEADER"]);
                    Assert.Equal(new[] {"en, de"}, request.Headers["accept-language"]);
                    Assert.Equal(new[] {"CONTENT_CUSTOM_VALUE"}, request.Headers["X-CONTENT-CUSTOM-HEADER"]);
                    Assert.Equal(testContent, request.Content);
                });
        }

        private static IEnumerable<HttpMethod[]> GetAllHttpMethods()
        {
            return new[]
            {
                new[] {HttpMethod.Delete},
                new[] {HttpMethod.Get},
                new[] {HttpMethod.Head},
                new[] {HttpMethod.Options},
                new[] {HttpMethod.Patch},
                new[] {HttpMethod.Post},
                new[] {HttpMethod.Put},
                new[] {HttpMethod.Trace}
            };
        }

        [Theory]
        [MemberData(nameof(GetAllHttpMethods))]
        public async Task RequestProperty_Method(HttpMethod method)
        {
            await RunClientTest(
                async (client, serverUri) =>
                {
                    HttpRequestMessage requestMessage = new(method, serverUri);

                    using HttpResponseMessage response = await client.SendAsync(requestMessage);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                },
                async server =>
                {
                    await using HttpTestStream stream = await server.AcceptStreamAsync();
                    HttpTestFullRequest request = await stream.ReceiveAndSendAsync();
                    Assert.Equal(request.Method, method.Method);
                });
        }
        
        private static IEnumerable<Version[]> GetAllHttpVersions()
        {
            return new[]
            {
                new[] {new Version(1,0) },
                new[] {new Version(1,1) }
            };
        }
        
        [Theory]
        [MemberData(nameof(GetAllHttpVersions))]
        public async Task RequestProperty_Version(Version version)
        {
            if (version == new Version(1,0))
                _version = HttpPrimitiveVersion.Version10;
            
            await RunClientTest(
                async (client, serverUri) =>
                {
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, serverUri)
                    {
                        Version = version
                    };
                    using HttpResponseMessage response = await client.SendAsync(requestMessage);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                },
                async server =>
                {
                    await using HttpTestStream stream = await server.AcceptStreamAsync();
                    HttpTestFullRequest request = await stream.ReceiveAndSendAsync();
                    Assert.Equal(request.Version, version);
                });
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/path")]
        [InlineData("/path?query&param")]
        public async Task RequestProperty_Host(string path)
        {
            await RunClientTest(
                async (client, serverUri) =>
                {
                    Uri uri = new (serverUri, path);
                    HttpRequestMessage requestMessage = new(HttpMethod.Get,  uri)
                    {
                        
                    };
                    using HttpResponseMessage response = await client.SendAsync(requestMessage);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                },
                async server =>
                {
                    await using HttpTestStream stream = await server.AcceptStreamAsync();
                    HttpTestFullRequest request = await stream.ReceiveAndSendAsync();
                    Assert.Equal(request.PathAndQuery, path);
                });
        }

        [Fact]
        public async Task Send_Sequential()
        {
            const int runs = 5;
            await RunClientTest(
                async (client, serverUri) =>
                {
                    for (int i = 0; i < runs; i++)
                    {
                        HttpRequestMessage requestMessage = new(HttpMethod.Get, serverUri)
                        {

                        }; 
                        using HttpResponseMessage response = await client.SendAsync(requestMessage);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    }
                },
                async server => 
                {
                    for (int i = 0; i < runs; i++)
                    {
                        await using HttpTestStream stream = await server.AcceptStreamAsync();
                        HttpTestFullRequest request = await stream.ReceiveAndSendAsync();
                    }
                });
        }
        
        [Fact]
        public async Task Send_Sequential_Read_Partial()
        {
            const int runs = 5;
            await RunClientTest(
                async (client, serverUri) =>
                {
                    for (int i = 0; i < runs; i++)
                    {
                        HttpRequestMessage requestMessage = new(HttpMethod.Get, serverUri);
                        using HttpResponseMessage response = await client.SendAsync(requestMessage);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        
                        await using Stream stream = await response.Content.ReadAsStreamAsync();
                        stream.ReadByte();
                    }
                },
                async server => 
                {
                    for (int i = 0; i < runs; i++)
                    {
                        await using HttpTestStream stream = await server.AcceptStreamAsync();
                        HttpTestFullRequest request = await stream.ReceiveAndSendChunkedAsync(200, null, "Some content".Select(x=>x.ToString()).ToArray() );
                    }
                });
        }
        
        internal virtual async Task RunClientTest(Func<HttpClient, Uri, Task> clientFunc,
            Func<HttpTestConnection, Task> serverFunc, int? millisecondsTimeout = null)
        {
            await RunMultiStreamTest(
                async (connection, serverUri) =>
                {
                    PrimitiveHttpHandler handler = new PrimitiveHttpHandler(connection);
                    HttpClient client = new HttpClient(handler);

                    await clientFunc(client, serverUri);
                },
                serverFunc, millisecondsTimeout);
        }

        internal override HttpPrimitiveVersion Version => _version;

        internal override async Task<HttpTestServer> CreateTestServerAsync(ConnectionFactory connectionFactory)
        {
            EndPoint? endPoint = UseSockets ? new IPEndPoint(IPAddress.Loopback, 0) : null;
            return new Http1TestServer(await connectionFactory.ListenAsync(endPoint, CreateListenerProperties())
                .ConfigureAwait(false));
        }

        // Enable wireshark debugging.
        internal override bool UseSockets => true;

        internal override async Task<HttpConnection> CreateTestClientAsync(ConnectionFactory connectionFactory,
            EndPoint endPoint)
        {
            Connection connection = await connectionFactory.ConnectAsync(endPoint, options: CreateConnectProperties());
            Stream stream = new ConnectionOwningStream(connection);
            return new Http1Connection(stream, Version);
        }
    }
}